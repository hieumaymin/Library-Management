using Microsoft.EntityFrameworkCore;
using MyLibraryDemo.Data;
using MyLibraryDemo.Data.Models;

namespace MyLibraryDemo.Services
{
    // ─── Allowed renewal durations per flow.md step 2 ────────────────────────────
    public static class RenewalDuration
    {
        public static readonly int[] AllowedMonths = { 1, 6, 12 };

        public static bool IsValid(int months) => AllowedMonths.Contains(months);

        public static string DisplayName(int months) => months switch
        {
            1  => "1 tháng",
            6  => "6 tháng",
            12 => "12 tháng",
            _  => $"{months} tháng"
        };
    }

    // ─── Result DTO ───────────────────────────────────────────────────────────────

    public class CardRenewalResult
    {
        public bool Success { get; private set; }
        public string? ErrorMessage { get; private set; }
        public LibraryCard? Card { get; private set; }
        public string? RenewalSummary { get; private set; }

        public static CardRenewalResult Ok(LibraryCard card, string summary)
            => new() { Success = true, Card = card, RenewalSummary = summary };

        public static CardRenewalResult Fail(string message)
            => new() { Success = false, ErrorMessage = message };
    }

    // ─── Interface ────────────────────────────────────────────────────────────────

    public interface ICardService
    {
        /// <summary>
        /// Renew a library card. Follows flow.md – Library Card Renewal Flow.
        ///
        /// Step 1  : barcode (required)
        /// Step 2-3: durationMonths must be one of { 1, 6, 12 }
        /// Step 4-5: find card by barcode; check exists
        /// Step 6  : if still valid → extend from ExpiryDate; if expired → reset from today
        /// Step 7-8: update ExpiryDate, save (transaction)
        /// </summary>
        Task<CardRenewalResult> RenewCardAsync(string barcode, int durationMonths);
    }

    // ─── Implementation ───────────────────────────────────────────────────────────

    public class CardService : ICardService
    {
        private readonly LibraryDbContext _context;

        public CardService(LibraryDbContext context)
        {
            _context = context;
        }

        public async Task<CardRenewalResult> RenewCardAsync(string barcode, int durationMonths)
        {
            // ── flow.md Step 1: Validate barcode input ────────────────────────────
            if (string.IsNullOrWhiteSpace(barcode))
                return CardRenewalResult.Fail("Vui lòng nhập mã thẻ (Barcode).");

            var trimmedBarcode = barcode.Trim();

            // ── flow.md Step 2-3: Validate duration (MUST be 1, 6, or 12) ─────────
            if (!RenewalDuration.IsValid(durationMonths))
            {
                var allowed = string.Join(", ", RenewalDuration.AllowedMonths.Select(m => $"{m} tháng"));
                return CardRenewalResult.Fail(
                    $"Thời hạn gia hạn không hợp lệ. Chỉ được chọn: {allowed}.");
            }

            // ── flow.md Step 4: Find card by barcode ─────────────────────────────
            // EC-1: case-insensitive lookup (barcodes are ASCII)
            var card = await _context.LibraryCards
                .Include(lc => lc.Reader)
                .FirstOrDefaultAsync(lc => lc.Barcode.ToUpper() == trimmedBarcode.ToUpper());

            // ── flow.md Step 5: Check card exists ────────────────────────────────
            if (card == null)
                return CardRenewalResult.Fail(
                    $"Không tìm thấy thẻ thư viện với mã barcode: \"{trimmedBarcode}\".");

            // NEW RULE: Reject if deactivated
            if (!card.IsActive)
                return CardRenewalResult.Fail(
                    "Thẻ đang bị vô hiệu hoá. Vui lòng kích hoạt lại thẻ trước khi gia hạn.");

            // ── flow.md Step 6: Check expiry ─────────────────────────────────────
            var today         = DateTime.Today;
            var oldExpiry     = card.ExpiryDate;
            var wasExpired    = card.IsExpired;

            // flow.md: "If still valid → extend" → base = current ExpiryDate
            //          "If expired    → reset"   → base = today
            var baseDate  = (!wasExpired) ? oldExpiry : today;
            var newExpiry = baseDate.AddMonths(durationMonths);

            // Sanity guard: new expiry must be strictly in the future
            if (newExpiry <= today)
                return CardRenewalResult.Fail(
                    $"Ngày hết hạn mới ({newExpiry:dd/MM/yyyy}) không hợp lệ – " +
                    "vui lòng chọn thời hạn dài hơn.");

            // ── flow.md Step 7: Update ExpiryDate ────────────────────────────────
            card.ExpiryDate = newExpiry;

            // ── flow.md Step 8: Save DB (transaction) ────────────────────────────
            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Update(card);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }

            // ── Build summary ─────────────────────────────────────────────────────
            // EC-6 FIX: use wasExpired snapshots (not current card state)
            var extendedFrom = (!wasExpired)
                ? $"từ {oldExpiry:dd/MM/yyyy}"           // still valid → extended
                : $"từ hôm nay ({today:dd/MM/yyyy})";   // expired → reset

            var readerName = card.Reader?.Name ?? $"ReaderId={card.ReaderId}";
            var notes      = new List<string>();
            if (wasExpired)  notes.Add($"thẻ đã hết hạn {oldExpiry:dd/MM/yyyy}");

            var summary = $"Gia hạn thành công cho \"{readerName}\" " +
                          $"({RenewalDuration.DisplayName(durationMonths)}) " +
                          $"{extendedFrom}. " +
                          $"Ngày hết hạn mới: {newExpiry:dd/MM/yyyy}.";

            if (notes.Any())
                summary += $" Ghi chú: {string.Join("; ", notes)}.";

            return CardRenewalResult.Ok(card, summary);
        }
    }
}
