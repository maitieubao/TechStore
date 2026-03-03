using System.ComponentModel.DataAnnotations;

namespace TechStore.Web.Models.ViewModels
{
    public class ResetPasswordVM
    {
        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mã OTP")]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập Mật khẩu mới")]
        [StringLength(100, ErrorMessage = "Mật khẩu phải có độ dài tối thiểu {2} ký tự.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
