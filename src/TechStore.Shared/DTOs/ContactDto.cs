using System.ComponentModel.DataAnnotations;

namespace TechStore.Shared.DTOs
{
    public class ContactDto
    {
        [Required(ErrorMessage = "Vui lòng nhập họ và tên")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập nội dung tin nhắn")]
        public string Message { get; set; } = string.Empty;
    }
}
