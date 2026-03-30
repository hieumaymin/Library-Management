using System.ComponentModel.DataAnnotations;

namespace MyLibraryDemo.Data.Models
{
    public enum BookStatus
    {
        Normal,
        Damaged,
        Lost
    }

    public class Book
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên sách")]
        [MaxLength(255, ErrorMessage = "Tên sách không được vượt quá 255 ký tự")]
        [Display(Name = "Tên sách")]
        public required string Title { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên tác giả")]
        [MaxLength(100, ErrorMessage = "Tên tác giả không được vượt quá 100 ký tự")]
        [Display(Name = "Tác giả")]
        public required string Author { get; set; }

        [Display(Name = "Năm xuất bản")]
        [Range(1800, 2100, ErrorMessage = "Năm xuất bản phải từ 1800 đến 2100")]
        public int? PublicationYear { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mô tả sách")]
        [MaxLength(1000, ErrorMessage = "Mô tả không được vượt quá 1000 ký tự")]
        [Display(Name = "Mô tả")]
        public required string Description { get; set; }

        [MaxLength(100)]
        [Display(Name = "Thể loại")]
        public string? Category { get; set; }

        [MaxLength(50)]
        [Display(Name = "Ngôn ngữ")]
        public string? Language { get; set; }

        [MaxLength(150)]
        [Display(Name = "Nhà xuất bản")]
        public string? Publisher { get; set; }

        [Display(Name = "Số lượng")]
        [Range(0, 9999, ErrorMessage = "Số lượng phải từ 0 đến 9999")]
        public int Quantity { get; set; } = 1;

        [Display(Name = "Tình trạng")]
        public BookStatus Status { get; set; } = BookStatus.Normal;

        // Navigation
        public ICollection<Loan>? Loans { get; set; }
    }
}