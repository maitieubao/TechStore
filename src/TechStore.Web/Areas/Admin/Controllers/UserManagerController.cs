using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Web.Services.Interfaces;

namespace TechStore.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class UserManagerController : Controller
    {
        private readonly IUserApiService _userService;

        public UserManagerController(IUserApiService userService)
        {
            _userService = userService;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _userService.GetAllUsersAsync();
            return View(users ?? new());
        }

        [HttpPost]
        public async Task<IActionResult> LockUser(string id)
        {
            var success = await _userService.LockUserAsync(id);
            TempData[success ? "Success" : "Error"] =
                success ? "Đã khóa tài khoản người dùng" : "Không thể khóa tài khoản (có thể đây là tài khoản của bạn)";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> UnlockUser(string id)
        {
            var success = await _userService.UnlockUserAsync(id);
            TempData[success ? "Success" : "Error"] =
                success ? "Đã mở khóa tài khoản người dùng" : "Lỗi khi mở khóa tài khoản";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var success = await _userService.DeleteUserAsync(id);
            TempData[success ? "Success" : "Error"] =
                success ? "Đã xóa hoàn toàn tài khoản" : "Lỗi khi xóa tài khoản (có thể vi phạm khóa ngoại hoặc đây là tài khoản của bạn)";
            return RedirectToAction(nameof(Index));
        }
    }
}
