using System.ComponentModel.DataAnnotations;

namespace MyLibraryDemo.Data.Models
{
    public enum ReaderType
    {
        [Display(Name = "Sinh viên")]
        Student,

        [Display(Name = "Giảng viên")]
        Lecturer,

        [Display(Name = "Khác")]
        Other
    }

    public class Reader
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên độc giả")]
        [MaxLength(255, ErrorMessage = "Tên độc giả không được vượt quá 255 ký tự")]
        [Display(Name = "Tên độc giả")]
        public required string Name { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ email")]
        [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ")]
        [MaxLength(255, ErrorMessage = "Email không được vượt quá 255 ký tự")]
        [Display(Name = "Email")]
        public required string Email { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [RegularExpression(@"^[0-9]+$", ErrorMessage = "Số điện thoại chỉ được chứa chữ số")]
        [StringLength(11, MinimumLength = 10, ErrorMessage = "Số điện thoại phải từ 10 đến 11 chữ số")]
        [Display(Name = "Số điện thoại")]
        public required string PhoneNumber { get; set; }

        [Display(Name = "Loại độc giả")]
        public ReaderType Type { get; set; } = ReaderType.Other;

        [Display(Name = "Hạn mức mượn (cuốn)")]
        [Range(1, 20, ErrorMessage = "Hạn mức mượn phải từ 1 đến 20")]
        public int MaxBooksAllowed { get; set; } = 3;

        // Navigation
        public LibraryCard? LibraryCard { get; set; }
        public ICollection<Loan>? Loans { get; set; }
    }
}