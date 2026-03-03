using Microsoft.AspNetCore.Mvc;
using TechStore.Shared.DTOs;
using TechStore.Web.Models.ViewModels;
using TechStore.Web.Services.Interfaces;

namespace TechStore.Web.Controllers
{
    public class ProductController : Controller
    {
        private readonly IProductApiService _productService;
        private readonly ICategoryApiService _categoryService;
        private readonly IReviewApiService _reviewService;

        public ProductController(
            IProductApiService productService,
            ICategoryApiService categoryService,
            IReviewApiService reviewService)
        {
            _productService = productService;
            _categoryService = categoryService;
            _reviewService = reviewService;
        }

        // GET: /product?search=...&categoryId=...&brand=...
        public async Task<IActionResult> Index([FromQuery] ProductFilterDto filter)
        {
            if (filter.PageSize <= 0) filter.PageSize = 12;
            if (filter.Page <= 0) filter.Page = 1;

            var products = await _productService.GetProductsAsync(filter);
            var categories = await _categoryService.GetCategoriesAsync();
            var brands = await _productService.GetBrandsAsync();
            var priceRange = await _productService.GetPriceRangeAsync();

            var vm = new ProductListVM
            {
                Products = products ?? new(),
                Filter = filter,
                Categories = categories ?? new(),
                Brands = brands ?? new(),
                MinPrice = priceRange?.Min ?? 0,
                MaxPrice = priceRange?.Max ?? 100000000
            };

            return View(vm);
        }

        // GET: /product/detail/{slug}
        [Route("product/{slug}")]
        public async Task<IActionResult> Detail(string slug)
        {
            var product = await _productService.GetProductBySlugAsync(slug);
            if (product == null)
                return NotFound();

            var reviews = await _reviewService.GetByProductAsync(product.Id);

            // Related products (same category)
            var relatedFilter = new ProductFilterDto
            {
                CategoryId = product.CategoryId,
                PageSize = 4,
                Page = 1
            };
            var related = await _productService.GetProductsAsync(relatedFilter);

            var vm = new ProductDetailVM
            {
                Product = product,
                Reviews = reviews ?? new(),
                RelatedProducts = related?.Items?
                    .Where(p => p.Id != product.Id)
                    .Take(4).ToList() ?? new()
            };

            return View(vm);
        }

        // POST: /product/review (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Review(CreateReviewDto dto)
        {
            if (!User.Identity?.IsAuthenticated ?? true)
                return Unauthorized();

            var success = await _reviewService.CreateReviewAsync(dto);
            if (success)
                TempData["Success"] = "Đánh giá đã được gửi!";
            else
                TempData["Error"] = "Bạn đã đánh giá sản phẩm này rồi.";

            return RedirectToAction(nameof(Detail), new { id = dto.ProductId });
        }
    }
}
