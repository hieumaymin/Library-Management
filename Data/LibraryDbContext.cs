using Microsoft.EntityFrameworkCore;
using MyLibraryDemo.Data.Models;

namespace MyLibraryDemo.Data
{
    public class LibraryDbContext : DbContext
    {
        public LibraryDbContext(DbContextOptions<LibraryDbContext> options) : base(options)
        {
        }

        public DbSet<Book> Books { get; set; }
        public DbSet<Reader> Readers { get; set; }
        public DbSet<Loan> Loans { get; set; }
        public DbSet<LibraryCard> LibraryCards { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Book - Loan: one-to-many
            modelBuilder.Entity<Loan>()
                .HasOne(l => l.Book)
                .WithMany(b => b.Loans)
                .HasForeignKey(l => l.BookId)
                .OnDelete(DeleteBehavior.Restrict); // prevent cascade delete if loans exist

            // Reader - Loan: one-to-many
            modelBuilder.Entity<Loan>()
                .HasOne(l => l.Reader)
                .WithMany(r => r.Loans)
                .HasForeignKey(l => l.ReaderId)
                .OnDelete(DeleteBehavior.Restrict);

            // Reader - LibraryCard: one-to-one
            modelBuilder.Entity<LibraryCard>()
                .HasOne(lc => lc.Reader)
                .WithOne(r => r.LibraryCard)
                .HasForeignKey<LibraryCard>(lc => lc.ReaderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Unique index on LibraryCard.Barcode
            modelBuilder.Entity<LibraryCard>()
                .HasIndex(lc => lc.Barcode)
                .IsUnique();

            // Store BookStatus and ReaderType as strings for readability
            modelBuilder.Entity<Book>()
                .Property(b => b.Status)
                .HasConversion<string>();

            modelBuilder.Entity<Reader>()
                .Property(r => r.Type)
                .HasConversion<string>();

            base.OnModelCreating(modelBuilder);
        }
    }
}