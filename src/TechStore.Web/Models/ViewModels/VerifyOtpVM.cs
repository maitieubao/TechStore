using System.ComponentModel.DataAnnotations;

namespace TechStore.Web.Models.ViewModels
{
    public class VerifyOtpVM
    {
        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mã OTP")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP phải gồm 6 chữ số")]
        public string Otp { get; set; } = string.Empty;
    }
}
