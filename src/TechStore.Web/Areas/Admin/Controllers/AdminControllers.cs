using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Web.Models.ViewModels;
using TechStore.Web.Services.Interfaces;

namespace TechStore.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DashboardController : Controller
    {
        private readonly IDashboardApiService _dashboardService;

        public DashboardController(IDashboardApiService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        public async Task<IActionResult> Index()
        {
            var dashboard = await _dashboardService.GetDashboardAsync();
            return View(new AdminDashboardVM { Dashboard = dashboard ?? new() });
        }
    }

    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class OrderManagerController : Controller
    {
        private readonly IDashboardApiService _dashboardService;

        public OrderManagerController(IDashboardApiService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        public async Task<IActionResult> Index()
        {
            var orders = await _dashboardService.GetAllOrdersAsync();
            return View(new AdminOrderListVM { Orders = orders ?? new() });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int orderId, string status)
        {
            var success = await _dashboardService.UpdateOrderStatusAsync(orderId, status);
            TempData[success ? "Success" : "Error"] =
                success ? $"Cập nhật trạng thái thành '{status}'" : "Cập nhật thất bại";
            return RedirectToAction(nameof(Index));
        }
    }

    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ProductManagerController : Controller
    {
        private readonly IDashboardApiService _dashboardService;
        private readonly IProductApiService _productService;
        private readonly ICategoryApiService _categoryService;

        public ProductManagerController(IDashboardApiService dashboardService, IProductApiService productService, ICategoryApiService categoryService)
        {
            _dashboardService = dashboardService;
            _productService = productService;
            _categoryService = categoryService;
        }

        public async Task<IActionResult> Index(TechStore.Shared.DTOs.ProductFilterDto filter)
        {
            if (filter.Page < 1) filter.Page = 1;
            filter.PageSize = 20;

            // Mặc định khách hàng chỉ thấy sản phẩm có IsActive = true
            // Nhưng Admin cần thấy cả sản phẩm "đã xoá mềm", nên ta set IsActive = null nếu không có query
            if (!Request.Query.ContainsKey("IsActive"))
            {
                filter.IsActive = null;
            }

            var products = await _productService.GetProductsAsync(filter);
            return View(new AdminProductListVM
            {
                Products = products ?? new(),
                CurrentPage = filter.Page,
                Filter = filter,
                Categories = await _categoryService.GetCategoriesAsync() ?? new()
            });
        }

        public async Task<IActionResult> Create()
        {
            ViewBag.Categories = await _categoryService.GetCategoriesAsync();
            return View(new TechStore.Web.Models.ViewModels.CreateProductVM());
        }

        [HttpPost]
        public async Task<IActionResult> Create(TechStore.Web.Models.ViewModels.CreateProductVM vm)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Categories = await _categoryService.GetCategoriesAsync();
                return View(vm);
            }

            var newId = await _productService.CreateProductAsync(vm);
            if (newId > 0)
            {
                TempData["Success"] = "Thêm sản phẩm thành công";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError("", "Lỗi khi thêm sản phẩm");
            ViewBag.Categories = await _categoryService.GetCategoriesAsync();
            return View(vm);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var product = await _productService.GetProductByIdAsync(id);
            if (product == null) return NotFound();

            ViewBag.Categories = await _categoryService.GetCategoriesAsync();
            return View(new TechStore.Web.Models.ViewModels.CreateProductVM
            {
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                StockQuantity = product.StockQuantity,
                Brand = product.Brand,
                Specifications = product.Specifications,
                CategoryId = product.CategoryId,
                IsCombo = product.IsCombo,
                OriginalPrice = product.OriginalPrice
            });
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, TechStore.Web.Models.ViewModels.CreateProductVM vm)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Categories = await _categoryService.GetCategoriesAsync();
                return View(vm);
            }

            var success = await _productService.UpdateProductAsync(id, vm);
            if (success)
            {
                TempData["Success"] = "Cập nhật sản phẩm thành công";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError("", "Lỗi khi cập nhật sản phẩm");
            ViewBag.Categories = await _categoryService.GetCategoriesAsync();
            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _productService.DeleteProductAsync(id);
            TempData[success ? "Success" : "Error"] =
                success ? "Đã xóa sản phẩm" : "Lỗi khi xóa sản phẩm";
            return RedirectToAction(nameof(Index));
        }

        // Image Management
        public async Task<IActionResult> ManageImages(int id)
        {
            var product = await _productService.GetProductByIdAsync(id);
            if (product == null) return NotFound();
            return View(product);
        }

        [HttpPost]
        public async Task<IActionResult> UploadImage(int productId, IFormFile file, bool isPrimary)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn file ảnh.";
                return RedirectToAction(nameof(ManageImages), new { id = productId });
            }

            var success = await _productService.UploadImageAsync(productId, file, isPrimary);
            TempData[success ? "Success" : "Error"] = success ? "Tải ảnh lên thành công" : "Tải ảnh thất bại";
            
            return RedirectToAction(nameof(ManageImages), new { id = productId });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteImage(int productId, int imageId)
        {
            // Delete image logic needs an API endpoint, but we don't have DeleteImageAsync in IProductApiService yet.
            // Temporary workaround if we create DeleteImageAsync
            // var success = await _productService.DeleteImageAsync(imageId);
            // TempData[success ? "Success" : "Error"] = success ? "Xóa ảnh thành công" : "Xóa ảnh thất bại";
            TempData["Error"] = "Chức năng xóa ảnh đang được cập nhật thêm.";
            return RedirectToAction(nameof(ManageImages), new { id = productId });
        }
    }

    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class CategoryManagerController : Controller
    {
        private readonly ICategoryApiService _categoryService;

        public CategoryManagerController(ICategoryApiService categoryService)
        {
            _categoryService = categoryService;
        }

        public async Task<IActionResult> Index()
        {
            var categories = await _categoryService.GetCategoriesAsync();
            return View(categories ?? new());
        }

        public IActionResult Create()
        {
            return View(new TechStore.Shared.DTOs.CreateCategoryDto());
        }

        [HttpPost]
        public async Task<IActionResult> Create(TechStore.Shared.DTOs.CreateCategoryDto dto)
        {
            if (!ModelState.IsValid)
                return View(dto);

            var success = await _categoryService.CreateCategoryAsync(dto);
            if (success)
            {
                TempData["Success"] = "Thêm danh mục thành công";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError("", "Lỗi khi thêm danh mục");
            return View(dto);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var category = await _categoryService.GetByIdAsync(id);
            if (category == null) return NotFound();

            return View(new TechStore.Shared.DTOs.CreateCategoryDto
            {
                Name = category.Name,
                Description = category.Description,
                ImageUrl = category.ImageUrl
            });
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, TechStore.Shared.DTOs.CreateCategoryDto dto)
        {
            if (!ModelState.IsValid)
                return View(dto);

            var success = await _categoryService.UpdateCategoryAsync(id, dto);
            if (success)
            {
                TempData["Success"] = "Cập nhật danh mục thành công";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError("", "Lỗi khi cập nhật danh mục");
            return View(dto);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _categoryService.DeleteCategoryAsync(id);
            TempData[success ? "Success" : "Error"] =
                success ? "Đã xóa danh mục" : "Lỗi khi xóa danh mục (Có thể danh mục này đang chứa sản phẩm)";
            return RedirectToAction(nameof(Index));
        }
    }
}
