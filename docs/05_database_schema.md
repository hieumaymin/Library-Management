# Database Schema

## Reader
- Id
- Name
- Email
- Type

## LibraryCard
- Id
- ReaderId
- Barcode
- ExpiryDate

## Book
- Id
- Title
- Category
- Language
- Author
- Publisher
- Quantity
- Status (Normal, Damaged, Lost)

## Borrow
- Id
- ReaderId
- BookId
- BorrowDate
- ReturnDate

## User
- Id
- Username
- Password
- Role (Admin, Staff)