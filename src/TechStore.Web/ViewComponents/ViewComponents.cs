using Microsoft.AspNetCore.Mvc;
using TechStore.Web.Services.Interfaces;

namespace TechStore.Web.ViewComponents
{
    public class CategoryMenuViewComponent : ViewComponent
    {
        private readonly ICategoryApiService _categoryService;

        public CategoryMenuViewComponent(ICategoryApiService categoryService)
        {
            _categoryService = categoryService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var categories = await _categoryService.GetCategoriesAsync();
            return View(categories ?? new());
        }
    }

    public class CartWidgetViewComponent : ViewComponent
    {
        private readonly ICartApiService _cartService;

        public CartWidgetViewComponent(ICartApiService cartService)
        {
            _cartService = cartService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                return View(new TechStore.Shared.DTOs.CartSummaryDto());
            }

            var cart = await _cartService.GetCartAsync();
            return View(cart ?? new());
        }
    }
}
