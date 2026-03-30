using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyLibraryDemo.Data.Models
{
    public class LibraryCard
    {
        public int Id { get; set; }

        [Required]
        public int ReaderId { get; set; }

        [ForeignKey("ReaderId")]
        public Reader? Reader { get; set; }

        [Required(ErrorMessage = "Mã thẻ không được để trống")]
        [MaxLength(50)]
        [Display(Name = "Mã thẻ (Barcode)")]
        public required string Barcode { get; set; }

        [Display(Name = "Ngày cấp")]
        [DataType(DataType.Date)]
        public DateTime IssueDate { get; set; } = DateTime.Today;

        [Display(Name = "Ngày hết hạn")]
        [DataType(DataType.Date)]
        public DateTime ExpiryDate { get; set; }

        [Display(Name = "Kích hoạt")]
        public bool IsActive { get; set; } = true;

        // Computed properties
        [NotMapped]
        public bool IsExpired => DateTime.Now.Date > ExpiryDate.Date;

        [NotMapped]
        public bool IsValid => IsActive && !IsExpired;

        [NotMapped]
        public string StatusDisplay
        {
            get
            {
                if (!IsActive) return "Vô hiệu";
                if (IsExpired) return "Hết hạn";
                return "Hợp lệ";
            }
        }
    }
}
