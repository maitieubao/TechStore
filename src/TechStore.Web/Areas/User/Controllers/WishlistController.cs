using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Shared.DTOs;
using TechStore.Web.Services.Interfaces;

namespace TechStore.Web.Areas.User.Controllers
{
    [Area("User")]
    [Authorize]
    public class WishlistController : Controller
    {
        private readonly IWishlistApiService _wishlistService;

        public WishlistController(IWishlistApiService wishlistService)
        {
            _wishlistService = wishlistService;
        }

        public async Task<IActionResult> Index()
        {
            var items = await _wishlistService.GetWishlistAsync();
            return View(items ?? new List<WishlistItemDto>());
        }

        [HttpPost]
        public async Task<IActionResult> Toggle(int productId)
        {
            var isIn = await _wishlistService.IsInWishlistAsync(productId);
            bool success;
            if (isIn)
            {
                success = await _wishlistService.RemoveFromWishlistAsync(productId);
            }
            else
            {
                success = await _wishlistService.AddToWishlistAsync(productId);
            }
            
            return Json(new { success, isAdded = !isIn });
        }
        
        [HttpPost]
        public async Task<IActionResult> Remove(int productId)
        {
            var success = await _wishlistService.RemoveFromWishlistAsync(productId);
            if (success)
            {
                TempData["Success"] = "Đã xóa khỏi danh sách yêu thích";
            }
            else
            {
                TempData["Error"] = "Lỗi khi xóa sản phẩm";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
