# System Flow

## Borrow Flow

1. Select reader
2. Check reader exists
3. Check library card exists
4. Check library card validity (not expired)

5. Select book
6. Check book exists
7. Check book status:
   - Not Lost
   - Not Damaged (or restricted)

8. Check book quantity > 0

9. Check borrow limit (max books per reader)

10. Check duplicate borrow:
    - Reader has not borrowed same book without returning

11. Create borrow record:
    - Set BorrowDate = now
    - ReturnDate = null

12. Decrease book quantity

13. Save changes (transaction)

---

## Return Flow

1. Select borrow record
2. Check borrow exists

3. Check if already returned:
   - If ReturnDate != null → reject

4. Update return:
   - Set ReturnDate = now

5. Increase book quantity

6. Update book status if needed:
   - Lost / Damaged

7. Save changes (transaction)

---

## Error Handling

- Reader not found → error
- Book not found → error
- Card expired → reject
- Book out of stock → reject
- Borrow limit exceeded → reject
- Duplicate borrow → reject
- Return invalid → reject
## Library Card Renewal Flow

1. Input:
   - Barcode

2. Select duration:
   - 1 month / 6 months / 12 months

3. Validate duration (must be allowed value)

4. Find card
5. Check exists

6. Check expiry:
   - If still valid → extend
   - If expired → reset

7. Update ExpiryDate

8. Save DB