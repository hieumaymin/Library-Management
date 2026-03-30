using Microsoft.EntityFrameworkCore;
using MyLibraryDemo.Data;
using MyLibraryDemo.Data.Models;

namespace MyLibraryDemo.Services
{
    public class BorrowResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Loan? Loan { get; set; }

        public static BorrowResult Ok(Loan loan) => new() { Success = true, Loan = loan };
        public static BorrowResult Fail(string message) => new() { Success = false, ErrorMessage = message };
    }

    public class ReturnResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? WarningMessage { get; set; }
        public Loan? Loan { get; set; }

        public static ReturnResult Ok(Loan loan, string? warning = null)
            => new() { Success = true, Loan = loan, WarningMessage = warning };
        public static ReturnResult Fail(string message) => new() { Success = false, ErrorMessage = message };
    }

    public interface IBorrowService
    {
        Task<BorrowResult> BorrowBookAsync(int readerId, int bookId);
        Task<ReturnResult> ReturnBookAsync(int loanId, bool isLost = false, string? notes = null);
    }

    public class BorrowService : IBorrowService
    {
        private readonly LibraryDbContext _context;
        private const int DefaultLoanDays = 14;

        public BorrowService(LibraryDbContext context)
        {
            _context = context;
        }

        public async Task<BorrowResult> BorrowBookAsync(int readerId, int bookId)
        {
            // ── FIX B2: Explicit transaction per flow.md step 13 ──────────────────
            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                // Step 1-2: Load reader
                var reader = await _context.Readers
                    .Include(r => r.LibraryCard)
                    .FirstOrDefaultAsync(r => r.Id == readerId);

                if (reader == null)
                    return BorrowResult.Fail("Không tìm thấy độc giả.");

                // Step 3-4: Check library card (business_rules #1, #2, flow step 3-4)
                if (reader.LibraryCard == null)
                    return BorrowResult.Fail("Độc giả chưa có thẻ thư viện. Vui lòng cấp thẻ trước khi mượn sách.");

                if (!reader.LibraryCard.IsActive)
                    return BorrowResult.Fail("Thẻ thư viện đã bị vô hiệu hóa. Vui lòng liên hệ nhân viên.");

                if (reader.LibraryCard.IsExpired)
                    return BorrowResult.Fail($"Thẻ thư viện đã hết hạn ngày {reader.LibraryCard.ExpiryDate:dd/MM/yyyy}. Vui lòng gia hạn thẻ.");

                // Step 9: Check borrow limit (business_rules #3)
                var currentBorrowCount = await _context.Loans
                    .CountAsync(l => l.ReaderId == readerId && !l.ReturnDate.HasValue && !l.IsLost);

                if (currentBorrowCount >= reader.MaxBooksAllowed)
                    return BorrowResult.Fail($"Độc giả đã đạt hạn mức {reader.MaxBooksAllowed} cuốn. Vui lòng trả sách trước khi mượn thêm.");

                // Step 6: Load book
                var book = await _context.Books.FindAsync(bookId);
                if (book == null)
                    return BorrowResult.Fail("Không tìm thấy sách.");

                // Step 7a: Check book not Lost (business_rules #2)
                if (book.Status == BookStatus.Lost)
                    return BorrowResult.Fail("Sách đã bị mất, không thể cho mượn.");

                // ── FIX B1: Check Damaged per flow.md step 7 ─────────────────────
                if (book.Status == BookStatus.Damaged)
                    return BorrowResult.Fail("Sách đang trong tình trạng hư hỏng, không thể cho mượn.");

                // Step 8: Check book quantity (business_rules #2)
                if (book.Quantity <= 0)
                    return BorrowResult.Fail("Sách đã hết, không còn bản nào có sẵn.");

                // Step 10: Check duplicate borrow (business_rules #4)
                var alreadyBorrowing = await _context.Loans
                    .AnyAsync(l => l.ReaderId == readerId && l.BookId == bookId
                                && !l.ReturnDate.HasValue && !l.IsLost);

                if (alreadyBorrowing)
                    return BorrowResult.Fail("Độc giả đang mượn cuốn sách này rồi. Không thể mượn cùng một cuốn hai lần.");

                // Step 11-12: Create loan + decrease quantity
                var loan = new Loan
                {
                    ReaderId   = readerId,
                    BookId     = bookId,
                    BorrowDate = DateTime.Today,
                    DueDate    = DateTime.Today.AddDays(DefaultLoanDays)
                };

                book.Quantity -= 1;
                _context.Update(book);
                _context.Add(loan);

                // Step 13: Save in transaction
                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return BorrowResult.Ok(loan);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task<ReturnResult> ReturnBookAsync(int loanId, bool isLost = false, string? notes = null)
        {
            // ── FIX B2: Explicit transaction per flow.md step 7 ──────────────────
            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var loan = await _context.Loans
                    .Include(l => l.Book)
                    .Include(l => l.Reader)
                    .FirstOrDefaultAsync(l => l.Id == loanId);

                if (loan == null)
                    return ReturnResult.Fail("Không tìm thấy phiếu mượn.");

                // Step 3: Already returned
                if (loan.IsReturned)
                    return ReturnResult.Fail("Phiếu mượn này đã được trả rồi.");

                if (loan.IsLost)
                    return ReturnResult.Fail("Sách này đã được đánh dấu là mất rồi.");

                // Step 4: Set return date
                loan.ReturnDate = DateTime.Now;
                loan.IsLost     = isLost;
                loan.Notes      = notes;

                string? warning = null;

                if (loan.Book != null)
                {
                    if (isLost)
                    {
                        // Step 6: Lost → mark book Lost, do NOT increase quantity (business_rules #6)
                        loan.Book.Status = BookStatus.Lost;
                        _context.Update(loan.Book);
                    }
                    else
                    {
                        // Step 5: Normal return → increase quantity (business_rules #5)
                        loan.Book.Quantity += 1;

                        // Step 6: If book was damaged before → keep Damaged status
                        // (no change to status on normal return unless explicitly changed)
                        _context.Update(loan.Book);

                        // Overdue warning
                        if (DateTime.Today > loan.DueDate.Date)
                        {
                            int daysLate = (int)(DateTime.Today - loan.DueDate.Date).TotalDays;
                            warning = $"Sách trả trễ {daysLate} ngày (hạn trả: {loan.DueDate:dd/MM/yyyy}).";
                        }
                    }
                }

                _context.Update(loan);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return ReturnResult.Ok(loan, warning);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
    }
}
