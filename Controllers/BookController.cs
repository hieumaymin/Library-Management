using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyLibraryDemo.Data;
using MyLibraryDemo.Data.Models;

public class BookController : Controller
{
    private readonly LibraryDbContext _context;

    public BookController(LibraryDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? searchTerm, string? category, string? language, string? status)
    {
        IQueryable<Book> query = _context.Books;

        // Text search
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var lower = searchTerm.ToLower();
            query = query.Where(b => b.Title.ToLower().Contains(lower) ||
                                     b.Author.ToLower().Contains(lower) ||
                                     (b.Category != null && b.Category.ToLower().Contains(lower)) ||
                                     (b.Publisher != null && b.Publisher.ToLower().Contains(lower)));
        }

        // ── FIX B10: Use ToLower() for case-insensitive filter ─────────────
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(b => b.Category != null &&
                                     b.Category.ToLower().Contains(category.ToLower()));

        if (!string.IsNullOrWhiteSpace(language))
            query = query.Where(b => b.Language != null &&
                                     b.Language.ToLower().Contains(language.ToLower()));

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<BookStatus>(status, out var bookStatus))
            query = query.Where(b => b.Status == bookStatus);

        ViewBag.CurrentSearchTerm = searchTerm;
        ViewBag.CurrentCategory = category;
        ViewBag.CurrentLanguage = language;
        ViewBag.CurrentStatus   = status;

        ViewBag.Categories = await _context.Books
            .Where(b => b.Category != null)
            .Select(b => b.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        return View(await query.ToListAsync());
    }

    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Title,Author,PublicationYear,Description,Category,Language,Publisher,Quantity,Status")] Book book)
    {
        if (ModelState.IsValid)
        {
            _context.Add(book);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Thêm sách mới thành công!";
            return RedirectToAction(nameof(Index));
        }
        return View(book);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var book = await _context.Books.FindAsync(id);
        if (book == null) return NotFound();
        return View(book);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Author,PublicationYear,Description,Category,Language,Publisher,Quantity,Status")] Book book)
    {
        if (id != book.Id) return NotFound();

        var activeLoanCount = await _context.Loans
            .CountAsync(l => l.BookId == id && !l.ReturnDate.HasValue && !l.IsLost);

        if (book.Quantity < activeLoanCount)
        {
            ModelState.AddModelError("Quantity",
                $"Không thể đặt số lượng thấp hơn {activeLoanCount} (số bản đang được mượn).");
        }

        if (ModelState.IsValid)
        {
            try
            {
                var existing = await _context.Books.FindAsync(id);
                if (existing == null) return NotFound();

                existing.Title           = book.Title;
                existing.Author          = book.Author;
                existing.PublicationYear = book.PublicationYear;
                existing.Description     = book.Description;
                existing.Category        = book.Category;
                existing.Language        = book.Language;
                existing.Publisher       = book.Publisher;
                existing.Quantity        = book.Quantity;
                existing.Status          = book.Status;

                _context.Update(existing);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Cập nhật sách thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!BookExists(book.Id)) return NotFound();
                else throw;
            }
        }
        return View(book);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var book = await _context.Books.FirstOrDefaultAsync(m => m.Id == id);
        if (book == null) return NotFound();
        return View(book);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var book = await _context.Books.FindAsync(id);
        if (book == null) return NotFound();

        // ── FIX B5: Include IsLost check – a lost-marked loan is still "open" ─
        var hasActiveLoans = await _context.Loans
            .AnyAsync(l => l.BookId == id && !l.ReturnDate.HasValue);

        if (hasActiveLoans)
        {
            TempData["Error"] = "Không thể xóa sách đang được mượn.";
            return RedirectToAction(nameof(Index));
        }

        _context.Books.Remove(book);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Xóa sách thành công!";
        return RedirectToAction(nameof(Index));
    }

    // POST: Mark book as damaged
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkDamaged(int id)
    {
        var book = await _context.Books.FindAsync(id);
        if (book == null) return NotFound();

        // ── FIX B7: Block if book has active loans ────────────────────────
        var activeLoans = await _context.Loans
            .CountAsync(l => l.BookId == id && !l.ReturnDate.HasValue && !l.IsLost);

        if (activeLoans > 0)
        {
            TempData["Error"] = $"Không thể đánh dấu hư hỏng khi sách còn {activeLoans} bản đang được mượn.";
            return RedirectToAction(nameof(Index));
        }

        if (book.Status == BookStatus.Lost)
        {
            TempData["Error"] = "Sách đã được đánh dấu là mất, không thể thay đổi.";
            return RedirectToAction(nameof(Index));
        }

        book.Status = BookStatus.Damaged;
        _context.Update(book);
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Đã đánh dấu sách \"{book.Title}\" là hư hỏng.";
        return RedirectToAction(nameof(Index));
    }

    // POST: Mark book as lost
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkLost(int id)
    {
        var book = await _context.Books.FindAsync(id);
        if (book == null) return NotFound();

        // ── FIX B6: Block if book has active loans ────────────────────────
        var activeLoans = await _context.Loans
            .CountAsync(l => l.BookId == id && !l.ReturnDate.HasValue && !l.IsLost);

        if (activeLoans > 0)
        {
            TempData["Error"] = $"Không thể đánh dấu mất khi sách còn {activeLoans} bản đang được mượn. " +
                                 "Hãy xử lý phiếu mượn qua chức năng Trả sách trước.";
            return RedirectToAction(nameof(Index));
        }

        if (book.Status == BookStatus.Lost)
        {
            TempData["Error"] = "Sách đã được đánh dấu là mất rồi.";
            return RedirectToAction(nameof(Index));
        }

        book.Status = BookStatus.Lost;
        if (book.Quantity > 0) book.Quantity -= 1;
        _context.Update(book);
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Đã đánh dấu sách \"{book.Title}\" là mất.";
        return RedirectToAction(nameof(Index));
    }

    // (Legacy Search action removed to merge with Index)

    private bool BookExists(int id) => _context.Books.Any(e => e.Id == id);
}