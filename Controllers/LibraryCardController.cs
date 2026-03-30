using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyLibraryDemo.Data;
using MyLibraryDemo.Data.Models;
using MyLibraryDemo.Services;

public class LibraryCardController : Controller
{
    private readonly LibraryDbContext _context;
    private readonly ICardService _cardService;

    public LibraryCardController(LibraryDbContext context, ICardService cardService)
    {
        _context     = context;
        _cardService = cardService;
    }

    // ─────────────────────────────────────────────────────────────
    // DETAILS – show card info for a reader
    // GET /LibraryCard/Details?readerId=5
    // ─────────────────────────────────────────────────────────────
    public async Task<IActionResult> Details(int readerId)
    {
        var reader = await _context.Readers
            .Include(r => r.LibraryCard)
            .FirstOrDefaultAsync(r => r.Id == readerId);

        if (reader == null) return NotFound();

        return View(reader);
    }

    // ─────────────────────────────────────────────────────────────
    // CREATE – issue new card to a reader
    // GET /LibraryCard/Create?readerId=5
    // ─────────────────────────────────────────────────────────────
    public async Task<IActionResult> Create(int readerId)
    {
        var reader = await _context.Readers
            .Include(r => r.LibraryCard)
            .FirstOrDefaultAsync(r => r.Id == readerId);

        if (reader == null) return NotFound();

        if (reader.LibraryCard != null && reader.LibraryCard.IsValid)
        {
            TempData["Error"] = "Độc giả đã có thẻ thư viện hợp lệ.";
            return RedirectToAction(nameof(Details), new { readerId });
        }

        var card = new LibraryCard
        {
            ReaderId   = readerId,
            Barcode    = GenerateBarcode(),
            IssueDate  = DateTime.Today,
            ExpiryDate = DateTime.Today.AddYears(1)
        };

        return View(card);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("ReaderId,Barcode,IssueDate")] LibraryCard card, int durationMonths)
    {
        // Remove validation errors related to ExpiryDate since we calculate it
        ModelState.Remove("ExpiryDate");
        ModelState.Remove("durationMonths");

        if (!MyLibraryDemo.Services.RenewalDuration.IsValid(durationMonths))
        {
            var allowed = string.Join(", ", MyLibraryDemo.Services.RenewalDuration.AllowedMonths.Select(m => $"{m} tháng"));
            ModelState.AddModelError("ExpiryDate", $"Thời hạn không hợp lệ. Chỉ được chọn: {allowed}.");
        }
        else
        {
            card.ExpiryDate = card.IssueDate.AddMonths(durationMonths);
        }
        if (await _context.LibraryCards.AnyAsync(lc => lc.Barcode == card.Barcode))
            ModelState.AddModelError("Barcode", "Mã thẻ này đã tồn tại. Vui lòng dùng mã khác.");

        if (card.ExpiryDate <= card.IssueDate && MyLibraryDemo.Services.RenewalDuration.IsValid(durationMonths))
            ModelState.AddModelError("ExpiryDate", "Ngày hết hạn phải sau ngày cấp.");

        var existingCard = await _context.LibraryCards
            .FirstOrDefaultAsync(lc => lc.ReaderId == card.ReaderId
                                    && lc.IsActive
                                    && lc.ExpiryDate >= DateTime.Today);
        if (existingCard != null)
        {
            TempData["Error"] = "Độc giả đã có thẻ thư viện hợp lệ.";
            return RedirectToAction(nameof(Details), new { readerId = card.ReaderId });
        }

        if (ModelState.IsValid)
        {
            card.IsActive = true;
            _context.Add(card);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Cấp thẻ thư viện thành công!";
            return RedirectToAction(nameof(Details), new { readerId = card.ReaderId });
        }

        return View(card);
    }

    // ─────────────────────────────────────────────────────────────
    // RENEW (by cardId, from Details view)
    // GET /LibraryCard/Renew/5
    // ─────────────────────────────────────────────────────────────
    public async Task<IActionResult> Renew(int id)
    {
        var card = await _context.LibraryCards
            .Include(lc => lc.Reader)
            .FirstOrDefaultAsync(lc => lc.Id == id);

        if (card == null) return NotFound();

        if (!card.IsActive)
        {
            TempData["Error"] = "Thẻ đang bị vô hiệu hoá. Vui lòng kích hoạt lại thẻ trước khi gia hạn.";
            return RedirectToAction(nameof(Details), new { readerId = card.ReaderId });
        }

        return View(card);
    }

    [HttpPost, ActionName("Renew")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RenewConfirmed(int id, int durationMonths)
    {
        var card = await _context.LibraryCards
            .Include(lc => lc.Reader)
            .FirstOrDefaultAsync(lc => lc.Id == id);

        if (card == null) return NotFound();

        var result = await _cardService.RenewCardAsync(card.Barcode, durationMonths);

        if (result.Success)
            TempData["Success"] = result.RenewalSummary;
        else
            TempData["Error"] = result.ErrorMessage;

        return RedirectToAction(nameof(Details), new { readerId = card.ReaderId });
    }

    // ─────────────────────────────────────────────────────────────
    // RENEW BY SEARCH – new flexible renewal
    // GET  /LibraryCard/RenewBySearch
    // POST /LibraryCard/RenewBySearch
    // ─────────────────────────────────────────────────────────────

    /// <summary>Show the renewal search form.</summary>
    [HttpGet]
    public IActionResult RenewBySearch(string? barcode)
    {
        ViewBag.Barcode = barcode;
        return View();
    }

    /// <summary>
    /// Process renewal.
    /// Accepts: barcode (string), readerId (int?), durationMonths (int).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RenewBySearch(string barcode, int durationMonths)
    {
        // ── EC-4/EC-10 FIX: catch model-binding failure (empty / non-numeric) ──
        if (ModelState.ContainsKey("durationMonths"))
        {
            var bindingErrors = ModelState["durationMonths"]!.Errors;
            if (bindingErrors.Any(e => e.Exception != null))
            {
                bindingErrors.Clear();
                ModelState.AddModelError("durationMonths", "Thời hạn gia hạn phải là số nguyên (ví dụ: 12).");
            }
        }

        // ── flow.md Step 1: Input Barcode ─────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(barcode))
            ModelState.AddModelError("barcode", "Vui lòng nhập Barcode.");

        // ── flow.md Step 2, 3: Validate duration ──────────────────────────────────
        if (!RenewalDuration.IsValid(durationMonths))
        {
            var allowed = string.Join(", ", RenewalDuration.AllowedMonths.Select(m => $"{m} tháng"));
            ModelState.AddModelError("durationMonths", $"Thời hạn không hợp lệ. Chỉ được chọn: {allowed}.");
        }

        // ── EC-13: Controller validates presence; service validates business logic ─
        if (!ModelState.IsValid)
        {
            ViewBag.Barcode        = barcode;
            ViewBag.DurationMonths = durationMonths;
            return View();
        }

        // ── flow.md Steps 4-8: Find card, extend, save ────────────────────────────
        var result = await _cardService.RenewCardAsync(barcode, durationMonths);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            ViewBag.Barcode        = barcode;
            ViewBag.DurationMonths = durationMonths;
            return View();
        }

        TempData["Success"] = result.RenewalSummary;
        return RedirectToAction(nameof(Details), new { readerId = result.Card!.ReaderId });
    }

    // ─────────────────────────────────────────────────────────────
    // DEACTIVATE
    // POST /LibraryCard/Deactivate/5
    // ─────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        var card = await _context.LibraryCards.FindAsync(id);
        if (card == null) return NotFound();

        var activeLoans = await _context.Loans
            .CountAsync(l => l.ReaderId == card.ReaderId && !l.ReturnDate.HasValue && !l.IsLost);

        if (activeLoans > 0)
        {
            TempData["Error"] = $"Không thể vô hiệu hóa thẻ khi độc giả còn {activeLoans} sách đang mượn. " +
                                 "Vui lòng xử lý các phiếu mượn trước.";
            return RedirectToAction(nameof(Details), new { readerId = card.ReaderId });
        }

        card.IsActive = false;
        _context.Update(card);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Đã vô hiệu hóa thẻ thư viện.";
        return RedirectToAction(nameof(Details), new { readerId = card.ReaderId });
    }

    // ─────────────────────────────────────────────────────────────
    // ACTIVATE
    // POST /LibraryCard/Activate/5
    // ─────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id)
    {
        var card = await _context.LibraryCards.FindAsync(id);
        if (card == null) return NotFound();

        card.IsActive = true;
        _context.Update(card);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Đã kích hoạt lại thẻ thư viện.";
        return RedirectToAction(nameof(Details), new { readerId = card.ReaderId });
    }

    // ─────────────────────────────────────────────────────────────
    // JSON API – quick lookup for renewal form autocomplete
    // GET /LibraryCard/LookupCard?term=...
    // ─────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> LookupCard(string? term)
    {
        if (string.IsNullOrWhiteSpace(term) || term.Trim().Length < 2)
            return Json(new { items = Array.Empty<object>(), hasMore = false });

        var trimmed     = term.Trim();
        var trimmedUp   = trimmed.ToUpper();
        const int limit = 10;

        // ── EC-2 FIX: pre-filter barcode in SQL (barcodes are ASCII, ToUpper safe) ─
        var barcodeMatches = await _context.LibraryCards
            .Include(lc => lc.Reader)
            .Where(lc => lc.Barcode.ToUpper().Contains(trimmedUp))
            .Take(limit)
            .ToListAsync();

        // ── EC-2: for name search, load bounded set only (max 500, not ALL) ────────
        var barcodeIds   = barcodeMatches.Select(b => b.Id).ToHashSet();
        var remaining    = limit - barcodeMatches.Count;
        List<LibraryCard> nameMatches = new();

        if (remaining > 0)
        {
            var namePool = await _context.LibraryCards
                .Include(lc => lc.Reader)
                .Where(lc => !barcodeIds.Contains(lc.Id))
                .OrderBy(lc => lc.Reader!.Name)
                .Take(500)  // bounded: load max 500 for C#-side diacritics filter
                .ToListAsync();

            nameMatches = namePool
                .Where(lc => lc.Reader?.Name?.Contains(trimmed, StringComparison.CurrentCultureIgnoreCase) == true)
                .Take(remaining)
                .ToList();
        }

        var combined  = barcodeMatches.Concat(nameMatches).ToList();
        var totalHint = combined.Count;

        // ── EC-12 FIX: hasMore indicator ─────────────────────────────────────────
        // We can't get exact total efficiently; flag if we hit the limit exactly.
        var hasMore = combined.Count == limit;

        // ── EC-15 FIX: correct status priority (inactive takes priority over expired) ─
        // IsActive=false is an explicit administrative decision; check it first.
        var items = combined
            .Select(lc => new
            {
                cardId     = lc.Id,
                readerId   = lc.ReaderId,
                readerName = lc.Reader?.Name ?? "(Không rõ)",
                barcode    = lc.Barcode,
                status     = !lc.IsActive ? "inactive"
                           : lc.IsExpired  ? "expired"
                           : "valid",
                statusText = lc.StatusDisplay,
                expiry     = lc.ExpiryDate.ToString("dd/MM/yyyy")
            })
            .ToList();

        return Json(new { items, hasMore });
    }

    // ─────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────
    private static string GenerateBarcode()
        => $"LIB{DateTime.Now:yyyyMMddHHmm}{Random.Shared.Next(100, 999)}";
}
