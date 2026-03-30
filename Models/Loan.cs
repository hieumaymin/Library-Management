using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyLibraryDemo.Data.Models
{
    public class Loan
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn sách")]
        [Display(Name = "Sách")]
        public int BookId { get; set; }

        [ForeignKey("BookId")]
        public Book? Book { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn độc giả")]
        [Display(Name = "Độc giả")]
        public int ReaderId { get; set; }

        [ForeignKey("ReaderId")]
        public Reader? Reader { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày mượn")]
        [Display(Name = "Ngày mượn")]
        [DataType(DataType.Date)]
        public DateTime BorrowDate { get; set; }

        [Display(Name = "Hạn trả")]
        [DataType(DataType.Date)]
        public DateTime DueDate { get; set; }

        [Display(Name = "Ngày trả")]
        [DataType(DataType.Date)]
        public DateTime? ReturnDate { get; set; }

        [Display(Name = "Mất sách")]
        public bool IsLost { get; set; } = false;

        [MaxLength(500)]
        [Display(Name = "Ghi chú")]
        public string? Notes { get; set; }

        // Computed properties
        [NotMapped]
        public bool IsReturned => ReturnDate.HasValue;

        [NotMapped]
        public bool IsOverdue => !IsReturned && DateTime.Now.Date > DueDate.Date;

        [NotMapped]
        public int DaysOverdue => IsOverdue ? (int)(DateTime.Now.Date - DueDate.Date).TotalDays : 0;

        [NotMapped]
        public string Status
        {
            get
            {
                if (IsLost) return "Mất sách";
                if (IsReturned) return "Đã trả";
                if (IsOverdue) return $"Quá hạn ({DaysOverdue} ngày)";
                return "Đang mượn";
            }
        }
    }
}