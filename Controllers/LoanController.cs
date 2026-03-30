using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyLibraryDemo.Data;
using MyLibraryDemo.Data.Models;
using MyLibraryDemo.Services;

public class LoanController : Controller
{
    private readonly LibraryDbContext _context;
    private readonly IBorrowService _borrowService;

    public LoanController(LibraryDbContext context, IBorrowService borrowService)
    {
        _context = context;
        _borrowService = borrowService;
    }

    // ─────────────────────────────────────────────────────────────
    // INDEX
    // ─────────────────────────────────────────────────────────────
    public async Task<IActionResult> Index()
    {
        var loans = await _context.Loans
            .Include(l => l.Book)
            .Include(l => l.Reader)
            .OrderByDescending(l => l.BorrowDate)
            .ToListAsync();
        return View(loans);
    }

    // ─────────────────────────────────────────────────────────────
    // CREATE (Borrow)
    // ─────────────────────────────────────────────────────────────
    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("BookId,ReaderId")] Loan loan)
    {
        if (loan.ReaderId == 0)
            ModelState.AddModelError("ReaderId", "Vui lòng chọn độc giả.");
        if (loan.BookId == 0)
            ModelState.AddModelError("BookId", "Vui lòng chọn sách.");

        if (!ModelState.IsValid)
            return View(loan);

        var result = await _borrowService.BorrowBookAsync(loan.ReaderId, loan.BookId);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            return View(loan);
        }

        TempData["Success"] = $"Tạo phiếu mượn thành công! Hạn trả: {result.Loan!.DueDate:dd/MM/yyyy}.";
        return RedirectToAction(nameof(Index));
    }

    // ─────────────────────────────────────────────────────────────
    // SEARCH APIs – JSON endpoints for autocomplete
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Search readers by Name (partial, case-insensitive) OR Barcode (partial, case-insensitive).
    /// Vietnamese diacritics handled via C# StringComparison instead of SQL LOWER().
    /// Also returns borrow count so UI can show capacity.
    /// GET /Loan/SearchReaders?term=...
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> SearchReaders(string? term)
    {
        if (string.IsNullOrWhiteSpace(term) || term.Trim().Length < 2)
            return Json(Array.Empty<object>());

        var trimmed = term.Trim();

        // ── FIX B3: Load to C# then filter with CurrentCultureIgnoreCase ─────
        // SQLite LOWER() does NOT handle Vietnamese diacritics; filter in-process.
        var allReaders = await _context.Readers
            .Include(r => r.LibraryCard)
            .Include(r => r.Loans)
            .ToListAsync();

        var matched = allReaders
            .Where(r =>
                r.Name.Contains(trimmed, StringComparison.CurrentCultureIgnoreCase) ||
                (r.LibraryCard != null &&
                 r.LibraryCard.Barcode.Contains(trimmed, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(r => r.Name)
            .Take(10)
            .Select(r =>
            {
                var currentBorrows = r.Loans?.Count(l => !l.ReturnDate.HasValue && !l.IsLost) ?? 0;
                return new
                {
                    id             = r.Id,
                    name           = r.Name,
                    email          = r.Email,
                    barcode        = r.LibraryCard?.Barcode,
                    cardStatus     = r.LibraryCard == null ? "none"
                                   : !r.LibraryCard.IsActive ? "inactive"
                                   : r.LibraryCard.IsExpired ? "expired"
                                   : "valid",
                    cardExpiry     = r.LibraryCard?.ExpiryDate.ToString("dd/MM/yyyy"),
                    currentBorrows,
                    maxAllowed     = r.MaxBooksAllowed,
                    atLimit        = currentBorrows >= r.MaxBooksAllowed
                };
            })
            .ToList();

        return Json(matched);
    }

    /// <summary>
    /// Search books by Title (partial, case-insensitive).
    /// Excludes Lost AND Damaged books per flow.md step 7.
    /// Optionally marks books already borrowed by a specific reader.
    /// GET /Loan/SearchBooks?term=...&readerId=...
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> SearchBooks(string? term, int? readerId)
    {
        if (string.IsNullOrWhiteSpace(term) || term.Trim().Length < 2)
            return Json(Array.Empty<object>());

        var trimmed = term.Trim();

        // ── FIX B3: C#-side filter for diacritics ────────────────────────────
        // ── FIX B4: Exclude Damaged per flow.md step 7 ───────────────────────
        var allBooks = await _context.Books
            .Where(b => b.Status != BookStatus.Lost && b.Status != BookStatus.Damaged)
            .ToListAsync();

        // Get books currently borrowed by this reader (for pre-warning)
        HashSet<int> alreadyBorrowedIds = new();
        if (readerId.HasValue && readerId > 0)
        {
            alreadyBorrowedIds = (await _context.Loans
                .Where(l => l.ReaderId == readerId && !l.ReturnDate.HasValue && !l.IsLost)
                .Select(l => l.BookId)
                .ToListAsync()).ToHashSet();
        }

        var matched = allBooks
            .Where(b => b.Title.Contains(trimmed, StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(b => b.Title)
            .Take(10)
            .Select(b => new
            {
                id              = b.Id,
                title           = b.Title,
                author          = b.Author,
                category        = b.Category,
                quantity        = b.Quantity,
                status          = b.Status.ToString(),
                available       = b.Quantity > 0,
                alreadyBorrowed = alreadyBorrowedIds.Contains(b.Id)
            })
            .ToList();

        return Json(matched);
    }

    // ─────────────────────────────────────────────────────────────
    // RETURN
    // ─────────────────────────────────────────────────────────────
    public async Task<IActionResult> Return(int? id)
    {
        if (id == null) return NotFound();

        var loan = await _context.Loans
            .Include(l => l.Book)
            .Include(l => l.Reader)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (loan == null) return NotFound();

        if (loan.IsReturned)
        {
            TempData["Error"] = "Phiếu mượn này đã được trả rồi.";
            return RedirectToAction(nameof(Index));
        }

        return View(loan);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReturnConfirmed(int id, bool isLost = false, string? notes = null)
    {
        var result = await _borrowService.ReturnBookAsync(id, isLost, notes);

        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage;
            return RedirectToAction(nameof(Index));
        }

        if (!string.IsNullOrEmpty(result.WarningMessage))
            TempData["Warning"] = result.WarningMessage;

        TempData["Success"] = isLost
            ? "Đã ghi nhận sách mất. Tình trạng sách đã được cập nhật."
            : "Trả sách thành công!";

        return RedirectToAction(nameof(Index));
    }

    // ─────────────────────────────────────────────────────────────
    // SEARCH LOANS (Index filter bar)
    // ─────────────────────────────────────────────────────────────
    public async Task<IActionResult> Search(string? bookTitle, string? readerName, string? status)
    {
        IQueryable<Loan> query = _context.Loans
            .Include(l => l.Book)
            .Include(l => l.Reader)
            .OrderByDescending(l => l.BorrowDate);

        if (!string.IsNullOrWhiteSpace(bookTitle))
            query = query.Where(l => l.Book != null && l.Book.Title.ToLower().Contains(bookTitle.ToLower()));

        if (!string.IsNullOrWhiteSpace(readerName))
            query = query.Where(l => l.Reader != null && l.Reader.Name.ToLower().Contains(readerName.ToLower()));

        if (!string.IsNullOrWhiteSpace(status))
        {
            switch (status.ToLower())
            {
                case "returned": query = query.Where(l => l.ReturnDate.HasValue); break;
                case "active":   query = query.Where(l => !l.ReturnDate.HasValue && !l.IsLost); break;
                case "overdue":  query = query.Where(l => !l.ReturnDate.HasValue && !l.IsLost && l.DueDate < DateTime.Today); break;
                case "lost":     query = query.Where(l => l.IsLost); break;
            }
        }

        return View("Index", await query.ToListAsync());
    }
}