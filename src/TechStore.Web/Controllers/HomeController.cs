using Microsoft.AspNetCore.Mvc;
using TechStore.Shared.DTOs;
using TechStore.Web.Models.ViewModels;
using TechStore.Web.Services.Interfaces;

namespace TechStore.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly IProductApiService _productService;
        private readonly ICategoryApiService _categoryService;

        public HomeController(IProductApiService productService, ICategoryApiService categoryService)
        {
            _productService = productService;
            _categoryService = categoryService;
        }

        public async Task<IActionResult> Index()
        {
            var featuredFilter = new ProductFilterDto { Page = 1, PageSize = 8, SortBy = "price", SortDescending = true };
            var newFilter = new ProductFilterDto { Page = 1, PageSize = 4, SortBy = "newest", SortDescending = true };

            var featured = await _productService.GetProductsAsync(featuredFilter);
            var newArrivals = await _productService.GetProductsAsync(newFilter);
            var categories = await _categoryService.GetCategoriesAsync();

            var vm = new HomeVM
            {
                FeaturedProducts = featured?.Items ?? new(),
                NewArrivals = newArrivals?.Items ?? new(),
                Categories = categories ?? new()
            };

            return View(vm);
        }

        public IActionResult About() => View();
        public IActionResult Contact() => View();

        [HttpPost]
        public async Task<IActionResult> Contact(ContactDto dto, [FromServices] IContactApiService contactService)
        {
            if (!ModelState.IsValid)
                return View(dto);

            var success = await contactService.SendContactAsync(dto);
            if (success)
            {
                TempData["Success"] = "Cảm ơn bạn đã liên hệ! Chúng tôi sẽ phản hồi sớm nhất có thể.";
                return RedirectToAction(nameof(Contact));
            }

            TempData["Error"] = "Có lỗi xảy ra, vui lòng thử lại sau.";
            return View(dto);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() => View();
    }
}
