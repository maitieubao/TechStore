using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using TechStore.Shared.Responses;
using TechStore.Web.Models.ViewModels;
using TechStore.Web.Services.Interfaces;

using Microsoft.AspNetCore.Authorization;

namespace TechStore.Web.Areas.User.Controllers
{
    [Area("User")]
    [Authorize]
    public class CartController : Controller
    {
        private readonly ICartApiService _cartService;

        public CartController(ICartApiService cartService)
        {
            _cartService = cartService;
        }

        public async Task<IActionResult> Index()
        {
            if (!User.Identity?.IsAuthenticated ?? true)
                return RedirectToAction("Login", "Account", new { area = "", returnUrl = "/User/Cart" });

            var cart = await _cartService.GetCartAsync();
            return View(new CartVM { Cart = cart ?? new() });
        }

        [HttpPost]
        public async Task<IActionResult> Add(int productId, int quantity = 1)
        {
            if (!User.Identity?.IsAuthenticated ?? true)
                return RedirectToAction("Login", "Account", new { area = "" });

            var response = await _cartService.AddToCartAsync(productId, quantity);
            
            if (response.Success)
            {
                TempData["Success"] = "Đã thêm vào giỏ hàng!";
            }
            else
            {
                if (response.Message != null && (response.Message.Contains("Unauthorized") || response.Message.Contains("401")))
                {
                     // Token expired but Cookie still valid -> Force Logout
                     await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                     Response.Cookies.Delete("TechStore_Token");
                     TempData["Error"] = "Phiên đăng nhập hết hạn. Vui lòng đăng nhập lại.";
                     return RedirectToAction("Login", "Account", new { area = "", returnUrl = Request.Headers.Referer.ToString() });
                }

                TempData["Error"] = response.Message ?? "Không thể thêm sản phẩm.";
            }

            return Redirect(Request.Headers.Referer.ToString() ?? "/");
        }

        [HttpPost]
        public async Task<IActionResult> Update(int cartItemId, int quantity)
        {
            await _cartService.UpdateQuantityAsync(cartItemId, quantity);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Remove(int cartItemId)
        {
            await _cartService.RemoveItemAsync(cartItemId);
            TempData["Success"] = "Đã xóa sản phẩm khỏi giỏ hàng.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Clear()
        {
            await _cartService.ClearCartAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
