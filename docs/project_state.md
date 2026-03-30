# Project State

## Last Updated
2026-03-30

## Tech Stack
- ASP.NET Core MVC (.NET 9)
- Entity Framework Core 9.0.4
- SQLite (`library.db`)
- Bootstrap 5 + Inter Font + Font Awesome 6 (custom sidebar UI)

---

## Completed

### Phase 1 – Model Refactor & Database ✅
- [x] `Book.cs`: removed `IsAvailable`, added `Category`, `Language`, `Publisher`, `Quantity`, `BookStatus` enum (Normal/Damaged/Lost)
- [x] `Reader.cs`: added `ReaderType` enum (Student/Lecturer/Other), `MaxBooksAllowed`
- [x] `Loan.cs`: renamed `LoanDate` → `BorrowDate`, added `DueDate`, `IsLost`, `Notes`; computed `IsReturned`, `IsOverdue`, `DaysOverdue`, `Status`
- [x] `LibraryCard.cs`: new model with `Barcode` (unique), `IssueDate`, `ExpiryDate`, `IsActive`; computed `IsExpired`, `IsValid`, `StatusDisplay`
- [x] `LibraryDbContext.cs`: added `DbSet<LibraryCard>`, one-to-one Reader↔Card, enum-as-string, unique Barcode index
- [x] Migration `RefactorModels_AddLibraryCard` applied to `library.db`

### Phase 2 – Reader Module Upgrade ✅
- [x] `ReaderController.cs`: include LibraryCard, filter by ReaderType, auto-set MaxBooksAllowed, block delete if active loans
- [x] `Views/Reader/Index.cshtml`: ReaderType filter, card status badge, "Cấp thẻ" link
- [x] `Views/Reader/Create.cshtml`: ReaderType dropdown, JS auto-set MaxBooksAllowed
- [x] `Views/Reader/Edit.cshtml`, `Delete.cshtml`: updated with new fields

### Phase 3 – Library Card Module ✅
- [x] `LibraryCardController.cs`: Details, Create (block if active card exists), Renew (+1 year), Deactivate
- [x] Auto-generate unique Barcode (LIB + timestamp + random)
- [x] `Views/LibraryCard/Details.cshtml`: card mockup display, renew/deactivate buttons
- [x] `Views/LibraryCard/Create.cshtml`: barcode regeneration JS
- [x] `Views/LibraryCard/Renew.cshtml`: preview new expiry date before confirming

### Phase 4 – Book Module Upgrade ✅
- [x] `BookController.cs`: filter by Category/Language/Status; MarkDamaged/MarkLost actions; validate Quantity vs active loans on Edit
- [x] `Views/Book/Index.cshtml`: filter bar, status badges, inline MarkDamaged/MarkLost forms
- [x] `Views/Book/Create.cshtml`, `Edit.cshtml`: all new fields

### Phase 5 – Borrow/Return Module (Full Business Rules) ✅
- [x] `Services/BorrowService.cs` (IBorrowService interface):
  - BR1: Library card must exist and be active + not expired
  - BR2: Book.Quantity > 0 AND Book.Status != Lost
  - BR3: currentBorrowCount < Reader.MaxBooksAllowed
  - BR4: Cannot borrow same book twice (active loan check)
  - DueDate auto-calculated (14 days)
  - Return: normal → Quantity+1; lost → Book.Status=Lost, no quantity change
  - Overdue warning on return
- [x] `LoanController.cs`: delegates to BorrowService; ReturnConfirmed with isLost+notes params; Search with overdue/lost filters
- [x] `Views/Loan/Index.cshtml`: overdue highlighting, status badges, search with 4 status filters
- [x] `Views/Loan/Create.cshtml`: shows library card requirement note
- [x] `Views/Loan/Return.cshtml`: overdue warning, notes field, lost checkbox

### Phase 5b – Borrow Form Search (Autocomplete) ✅
- [x] `LoanController.SearchReaders` (GET /Loan/SearchReaders?term=):
  - Search by Name (partial, case-insensitive, contains)
  - Search by Library Card Barcode (partial, case-insensitive, contains)
  - Returns JSON: id, name, email, barcode, cardStatus, cardExpiry
  - Max 10 results, min 2 chars
- [x] `LoanController.SearchBooks` (GET /Loan/SearchBooks?term=):
  - Search by Title (partial, case-insensitive, contains)
  - Excludes Lost books from results
  - Returns JSON: id, title, author, category, quantity, status, available
  - Max 10 results, min 2 chars
- [x] `Views/Loan/Create.cshtml` rewritten with vanilla JS autocomplete:
  - Debounced input (300ms) to avoid excess requests
  - Reader dropdown: shows card status badge (valid/expired/inactive/none)
  - Readers with invalid/expired/no card are shown but **disabled** (cannot select)
  - Book dropdown: shows quantity badge, out-of-stock books disabled
  - Keyboard navigation: Arrow↑↓ to navigate, Enter to select, Escape to close
  - Selected item shown as info card with clear (×) button
  - Client-side guard: blocks form submit if reader/book not selected
  - XSS-safe: all dynamic HTML escaped via escHtml()

### Phase 6 – Layout & Dashboard ✅
- [x] `Views/Shared/_Layout.cshtml`: complete redesign – sidebar nav, Inter font, Font Awesome, custom CSS design system
- [x] `Views/Home/Index.cshtml`: Dashboard with 6 stat cards + active loans table + overdue list
- [x] `HomeController.cs`: DashboardStats model with all aggregates
- [x] `Program.cs`: registered `IBorrowService` as scoped DI service

### Phase 7 – Library Card Renewal (Strict flow.md) ✅
- [x] `Services/CardService.cs` (`ICardService` interface):
  - `RenewCardAsync(barcode, durationMonths)`: strictly follows 8 steps in flow.md
  - Step 1: Input Barcode only
  - Step 2-3: Validates duration must be one of {1, 6, 12}
  - Step 4-5: Finds card by barcode and verifies it exists
  - Step 6: Checks expiry; extends if valid, resets to today if expired
  - Step 7: Updates ExpiryDate and ensures IsActive is true
  - Step 8: Saves DB wrapped in explicit transaction
- [x] `Program.cs`: registered `ICardService` as scoped DI service
- [x] `Controllers/LibraryCardController.cs`:
  - `GET/POST /LibraryCard/RenewBySearch`: only takes barcode + selected duration
  - `GET /LibraryCard/LookupCard?term=`: JSON autocomplete API handles bounded subsets for memory limit
- [x] `Views/LibraryCard/RenewBySearch.cshtml`:
  - Autocomplete search by barcode or name (debounce, minimum 2 chars, `hasMore` hint)
  - Card preview mockup (gradient card, status badge, current expiry)
  - Duration select dropdown hardcoded to 1, 6, and 12 months
  - Live new-expiry preview updating in real-time based on duration logic
  - Submit guard: disabled button + spinner on submit
  - Sidebar link added to `_Layout.cshtml`

---

## In Progress

_(none)_

---

## Planned

_(none – all planned features completed)_

---

## Known Issues

- Migration warning: `PRAGMA foreign_keys = 0` cannot run in a transaction (SQLite limitation – safe to ignore, migration completed successfully)
- Old `Loans` data: `LoanDate` was renamed to `DueDate` in migration; existing loans have `BorrowDate = 0001-01-01` (default) — acceptable since these were test records
- `Reader.LibraryCard` is one-to-one: if a reader needs a second card (after deactivation), a new record is inserted (the `Details` view handles this case by showing "Cấp thẻ" button again)
- **Concurrent renewal race condition:** No optimistic concurrency logic on LibraryCard (a double click or twin requests could theoretically renew a card twice).
- **No renewal audit log:** System tracks current ExpiryDate natively, but historical renewals/extensions traces are not preserved in a separate log table natively.

---

## Database
- Engine: SQLite
- File: `library.db`
- ORM: Entity Framework Core 9.0.4
- Migrations applied:
  1. (Initial) – Books, Readers, Loans
  2. `RefactorModels_AddLibraryCard` – LibraryCards, new columns, enum-as-string, unique indexes
