using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyLibraryDemo.Data;
using MyLibraryDemo.Data.Models;

public class ReaderController : Controller
{
    private readonly LibraryDbContext _context;

    public ReaderController(LibraryDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? sort)
    {
        var readers = await _context.Readers
            .Include(r => r.LibraryCard)
            .Include(r => r.Loans)
            .ToListAsync();
            
        if (sort == "borrow_desc")
        {
            readers = readers
                .OrderByDescending(r => r.Loans?.Count(l => !l.ReturnDate.HasValue && !l.IsLost) ?? 0)
                .ToList();
        }

        return View(readers);
    }

    public IActionResult Create() => View(new Reader
    {
        Name        = string.Empty,
        Email       = string.Empty,
        PhoneNumber = string.Empty,
        Type        = ReaderType.Other,
        MaxBooksAllowed = 2  // Default for Other
    });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Email,PhoneNumber,Type,MaxBooksAllowed")] Reader reader)
    {
        if (ModelState.IsValid)
        {
            if (await IsEmailDuplicate(reader.Email))
            {
                ModelState.AddModelError("Email", "Email này đã được sử dụng bởi độc giả khác.");
                return View(reader);
            }

            // ── FIX B8: Always derive MaxBooksAllowed from Type ────────────────
            // Remove fragile heuristic "== 3 means not overridden" which was wrong
            // for Lecturer type. Controller now always sets type-based default.
            // If admin wants to override, they should do so from the Edit page.
            reader.MaxBooksAllowed = GetDefaultMaxBooks(reader.Type);

            _context.Add(reader);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Thêm độc giả mới thành công!";
            return RedirectToAction(nameof(Index));
        }
        return View(reader);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var reader = await _context.Readers.FindAsync(id);
        if (reader == null) return NotFound();
        return View(reader);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Email,PhoneNumber,Type,MaxBooksAllowed")] Reader reader)
    {
        if (id != reader.Id) return NotFound();
        if (ModelState.IsValid)
        {
            try
            {
                if (await IsEmailDuplicate(reader.Email, reader.Id))
                {
                    ModelState.AddModelError("Email", "Email này đã được sử dụng bởi độc giả khác.");
                    return View(reader);
                }

                // ── FIX B8: Validate MaxBooksAllowed >= current active borrows ─
                var activeBorrows = await _context.Loans
                    .CountAsync(l => l.ReaderId == id && !l.ReturnDate.HasValue && !l.IsLost);

                if (reader.MaxBooksAllowed < activeBorrows)
                {
                    ModelState.AddModelError("MaxBooksAllowed",
                        $"Hạn mức không thể thấp hơn {activeBorrows} (số sách đang mượn).");
                    return View(reader);
                }

                _context.Update(reader);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Cập nhật độc giả thành công!";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ReaderExists(reader.Id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }
        return View(reader);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var reader = await _context.Readers
            .Include(r => r.LibraryCard)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (reader == null) return NotFound();
        return View(reader);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var reader = await _context.Readers.FindAsync(id);
        if (reader == null) return NotFound();

        var hasActiveLoans = await _context.Loans
            .AnyAsync(l => l.ReaderId == id && !l.ReturnDate.HasValue);

        if (hasActiveLoans)
        {
            TempData["Error"] = "Không thể xóa độc giả này vì đang có sách chưa trả.";
            return RedirectToAction(nameof(Index));
        }

        _context.Readers.Remove(reader);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Xóa độc giả thành công!";
        return RedirectToAction(nameof(Index));
    }

    // ── FIX B11: Add ToLower() for case-insensitive search ─────────────────
    public async Task<IActionResult> Search(string searchTerm, string? readerType)
    {
        IQueryable<Reader> query = _context.Readers.Include(r => r.LibraryCard);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var lower = searchTerm.ToLower();
            query = query.Where(r =>
                r.Name.ToLower().Contains(lower) ||
                r.Email.ToLower().Contains(lower) ||
                r.PhoneNumber.ToLower().Contains(lower));
        }

        if (!string.IsNullOrWhiteSpace(readerType) &&
            Enum.TryParse<ReaderType>(readerType, out var type))
        {
            query = query.Where(r => r.Type == type);
        }

        return View("Index", await query.ToListAsync());
    }

    private bool ReaderExists(int id) => _context.Readers.Any(e => e.Id == id);

    private async Task<bool> IsEmailDuplicate(string email, int? excludeReaderId = null)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        var query = _context.Readers.Where(r => r.Email.ToLower() == email.ToLower());
        if (excludeReaderId.HasValue)
            query = query.Where(r => r.Id != excludeReaderId.Value);
        return await query.AnyAsync();
    }

    private static int GetDefaultMaxBooks(ReaderType type) => type switch
    {
        ReaderType.Student  => 3,
        ReaderType.Lecturer => 5,
        ReaderType.Other    => 2,
        _ => 3
    };
}