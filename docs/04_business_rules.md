# Business Rules

1. Reader must have valid library card
2. Cannot borrow if:
   - Book quantity = 0
   - Card expired
3. Limit number of books per reader
4. Cannot borrow same book twice
5. Return book:
   - Increase quantity
6. Track lost/damaged books

## Book Status Rules

- Removed book:
  - Không hiển thị cho user
  - Không cho mượn

- Lost book:
  - Không cho mượn

- Damaged book:
  - Có thể hạn chế mượn (tuỳ hệ thống)

- Available:
  - Bình thường

