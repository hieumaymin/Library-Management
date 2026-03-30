using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyLibraryDemo.Data;
using MyLibraryDemo.Data.Models;

public class HomeController : Controller
{
    private readonly LibraryDbContext _context;

    public HomeController(LibraryDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var now = DateTime.Today;

        var stats = new DashboardStats
        {
            TotalBooks = await _context.Books.SumAsync(b => (int?)b.Quantity) ?? 0,
            TotalBookTitles = await _context.Books.CountAsync(),
            DamagedBooks = await _context.Books.CountAsync(b => b.Status == BookStatus.Damaged),
            LostBooks = await _context.Books.CountAsync(b => b.Status == BookStatus.Lost),

            TotalReaders = await _context.Readers.CountAsync(),
            ActiveCards = await _context.LibraryCards.CountAsync(lc => lc.IsActive && lc.ExpiryDate >= now),
            ExpiredCards = await _context.LibraryCards.CountAsync(lc => lc.IsActive && lc.ExpiryDate < now),

            ActiveLoans = await _context.Loans.CountAsync(l => !l.ReturnDate.HasValue && !l.IsLost),
            OverdueLoans = await _context.Loans.CountAsync(l => !l.ReturnDate.HasValue && !l.IsLost && l.DueDate < now),
            LostLoans = await _context.Loans.CountAsync(l => l.IsLost),

            RecentLoans = await _context.Loans
                .Include(l => l.Book)
                .Include(l => l.Reader)
                .Where(l => !l.ReturnDate.HasValue && !l.IsLost)
                .OrderBy(l => l.DueDate)
                .Take(5)
                .ToListAsync(),

            OverdueList = await _context.Loans
                .Include(l => l.Book)
                .Include(l => l.Reader)
                .Where(l => !l.ReturnDate.HasValue && !l.IsLost && l.DueDate < now)
                .OrderBy(l => l.DueDate)
                .Take(5)
                .ToListAsync()
        };

        return View(stats);
    }

    public IActionResult Privacy() => View();
}

public class DashboardStats
{
    // Books
    public int TotalBooks { get; set; }
    public int TotalBookTitles { get; set; }
    public int DamagedBooks { get; set; }
    public int LostBooks { get; set; }

    // Readers
    public int TotalReaders { get; set; }
    public int ActiveCards { get; set; }
    public int ExpiredCards { get; set; }

    // Loans
    public int ActiveLoans { get; set; }
    public int OverdueLoans { get; set; }
    public int LostLoans { get; set; }

    // Lists
    public List<Loan> RecentLoans { get; set; } = new();
    public List<Loan> OverdueList { get; set; } = new();
}
