using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Shared.DTOs;
using TechStore.Web.Services.Interfaces;

namespace TechStore.Web.Areas.User.Controllers
{
    [Area("User")]
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly IUserProfileApiService _profileService;

        public ProfileController(IUserProfileApiService profileService)
        {
            _profileService = profileService;
        }

        public async Task<IActionResult> Index()
        {
            var profile = await _profileService.GetProfileAsync();
            if (profile == null)
            {
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            return View(profile);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateInfo(UpdateProfileDto dto)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Vui lòng nhập đầy đủ thông tin hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            var success = await _profileService.UpdateProfileAsync(dto);
            TempData[success ? "Success" : "Error"] = success ? "Cập nhật thông tin thành công" : "Lỗi khi cập nhật thông tin";
            
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Mật khẩu không hợp lệ. Vui lòng kiểm tra lại.";
                return RedirectToAction(nameof(Index));
            }

            var success = await _profileService.ChangePasswordAsync(dto);
            TempData[success ? "Success" : "Error"] = success ? "Đổi mật khẩu thành công" : "Mật khẩu hiện tại không đúng hoặc lỗi hệ thống.";
            
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> UploadAvatar(IFormFile avatarFile)
        {
            if (avatarFile == null || avatarFile.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn ảnh đại diện hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            var success = await _profileService.UploadAvatarAsync(avatarFile);
            TempData[success ? "Success" : "Error"] = success ? "Đã thay đổi ảnh đại diện" : "Lỗi khi tải ảnh lên, file có thể quá lớn (Max: 2MB).";

            return RedirectToAction(nameof(Index));
        }
    }
}
