using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechStore.Application.Common.Interfaces;
using TechStore.Application.Features.Products.Commands;
using TechStore.Application.Features.Products.Queries;
using TechStore.Domain.Common;
using TechStore.Shared.DTOs;
using TechStore.Shared.Responses;

namespace TechStore.API.Controllers
{
    /// <summary>
    /// Controller quản lý sản phẩm (Products) trong hệ thống TechStore.
    /// 
    /// Chức năng chính:
    ///   - Lấy danh sách sản phẩm có lọc động, phân trang (public).
    ///   - Lấy chi tiết sản phẩm theo ID (public).
    ///   - Lấy chi tiết sản phẩm theo slug SEO-friendly (public).
    ///   - Tạo sản phẩm mới (Admin only).
    ///   - Cập nhật sản phẩm (Admin only).
    ///   - Xóa mềm sản phẩm / Hard delete (Admin only).
    ///   - Lấy danh sách thương hiệu cho filter UI (public).
    ///   - Lấy khoảng giá sản phẩm cho slider filter (public).
    ///   - Cảnh báo sản phẩm sắp hết hàng (Admin only).
    /// 
    /// Route gốc: api/products
    /// Phân quyền: Read → public; Create/Update/Delete → [Authorize(Roles = "Admin")].
    /// 
    /// Kiến trúc: Sử dụng MediatR/CQRS Pattern:
    ///   - GetFilteredProductsQuery: Lấy sản phẩm với Specification Pattern + Pagination.
    ///   - GetProductByIdQuery: Lấy chi tiết sản phẩm.
    ///   - CreateProductCommand / UpdateProductCommand / DeleteProductCommand: CRUD Admin.
    /// 
    /// Specification Pattern: ProductFilterSpecification cho phép filter động theo
    ///   CategoryId, Brand, PriceRange, SearchText, IsActive, SortBy, và phân trang.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Khởi tạo controller với MediatR và Unit of Work được inject qua DI.
        /// </summary>
        public ProductsController(IMediator mediator, IUnitOfWork unitOfWork)
        {
            _mediator = mediator;
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        /// [GET] api/products?categoryId=&brand=&minPrice=&maxPrice=&search=&page=&pageSize=&sortBy=&isActive=
        /// Lấy danh sách sản phẩm với lọc động và phân trang (Specification Pattern).
        /// 
        /// Workflow:
        ///   1. Nhận ProductFilterDto từ query string (tất cả params là optional).
        ///   2. Dispatch GetFilteredProductsQuery { Filter = filter } qua MediatR.
        ///   3. Handler Query sử dụng ProductFilterSpecification để build câu query động.
        ///   4. Trả về PagedResultDto&lt;ProductDto&gt; gồm items + TotalCount + phân trang metadata.
        /// 
        /// Query params (qua ProductFilterDto):
        ///   - categoryId: Lọc theo danh mục
        ///   - brand: Lọc theo thương hiệu
        ///   - minPrice/maxPrice: Lọc theo khoảng giá
        ///   - search: Tìm kiếm theo tên/mô tả
        ///   - page/pageSize: Phân trang (mặc định page=1, pageSize=12)
        ///   - sortBy: Sắp xếp (price-asc, price-desc, newest, rating)
        ///   - isActive: Lọc theo trạng thái (dùng cho Admin panel)
        /// 
        /// Public endpoint — không yêu cầu đăng nhập.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResultDto<ProductDto>>>> GetAll([FromQuery] ProductFilterDto filter)
        {
            var result = await _mediator.Send(new GetFilteredProductsQuery { Filter = filter });
            return Ok(ApiResponse<PagedResultDto<ProductDto>>.SuccessResponse(result));
        }

        /// <summary>
        /// [GET] api/products/{id}
        /// Lấy chi tiết một sản phẩm theo ID số nguyên.
        /// 
        /// Workflow:
        ///   1. Dispatch GetProductByIdQuery(id) qua MediatR.
        ///   2. Nếu không tìm thấy → 404 Not Found.
        ///   3. Trả về ProductDto đầy đủ (kèm ảnh, đánh giá, category).
        /// 
        /// Route param: {id:int} — ràng buộc route chỉ nhận số nguyên.
        /// Public endpoint — không yêu cầu đăng nhập.
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<ProductDto>>> GetById(int id)
        {
            var result = await _mediator.Send(new GetProductByIdQuery(id));
            if (result == null)
                return NotFound(ApiResponse<ProductDto>.ErrorResponse("Product not found"));

            return Ok(ApiResponse<ProductDto>.SuccessResponse(result));
        }

        /// <summary>
        /// [GET] api/products/slug/{slug}
        /// Lấy chi tiết sản phẩm theo slug SEO-friendly.
        /// 
        /// Workflow:
        ///   1. Query sản phẩm từ database bằng Eager Loading:
        ///      Include Category + Include Images + Include Reviews.
        ///   2. Điều kiện: Slug khớp VÀ IsActive = true (không trả về sản phẩm đã ẩn).
        ///   3. Nếu không tìm thấy → 404 Not Found.
        ///   4. Map sang ProductDto đầy đủ:
        ///      - AverageRating: Tính từ danh sách Reviews (0 nếu chưa có đánh giá).
        ///      - IsLowStock: true nếu StockQuantity > 0 và ≤ LowStockThreshold.
        ///      - ImageUrls: Sắp xếp theo DisplayOrder.
        /// 
        /// Route param: {slug} — slug sản phẩm (ví dụ: "iphone-15-pro-max").
        /// Public endpoint. Dùng để tạo URL thân thiện SEO cho trang chi tiết sản phẩm.
        /// </summary>
        [HttpGet("slug/{slug}")]
        public async Task<ActionResult<ApiResponse<ProductDto>>> GetBySlug(string slug)
        {
            var product = await _unitOfWork.Products.Query()
                .Include(p => p.Category)
                .Include(p => p.Images)
                .Include(p => p.Reviews)
                .FirstOrDefaultAsync(p => p.Slug == slug && p.IsActive); // Chỉ lấy sản phẩm active

            if (product == null)
                return NotFound(ApiResponse<ProductDto>.ErrorResponse("Product not found"));

            var dto = new ProductDto
            {
                Id = product.Id,
                Name = product.Name,
                Slug = product.Slug,
                Description = product.Description,
                Price = product.Price,
                StockQuantity = product.StockQuantity,
                // Cảnh báo sắp hết hàng: còn hàng nhưng ≤ ngưỡng cảnh báo
                IsLowStock = product.StockQuantity > 0 && product.StockQuantity <= product.LowStockThreshold,
                Brand = product.Brand,
                Specifications = product.Specifications,
                CategoryId = product.CategoryId,
                CategoryName = product.Category?.Name,
                CategorySlug = product.Category?.Slug,
                // Tính điểm đánh giá trung bình (0 nếu chưa có review)
                AverageRating = product.Reviews.Any() ? product.Reviews.Average(r => (double)r.Rating) : 0,
                ReviewCount = product.Reviews.Count,
                // Sắp xếp ảnh theo thứ tự hiển thị
                ImageUrls = product.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).ToList(),
                IsActive = product.IsActive,
                IsCombo = product.IsCombo,
                OriginalPrice = product.OriginalPrice
            };

            return Ok(ApiResponse<ProductDto>.SuccessResponse(dto));
        }

        /// <summary>
        /// [POST] api/products
        /// Tạo mới một sản phẩm. Chỉ Admin mới có quyền.
        /// 
        /// Workflow:
        ///   1. Yêu cầu Bearer JWT với role "Admin".
        ///   2. Nhận CreateProductCommand từ request body.
        ///   3. Dispatch qua MediatR. Handler sẽ:
        ///      - Validate dữ liệu.
        ///      - Tự động tạo slug từ tên sản phẩm.
        ///      - Tạo entity Product và lưu vào database.
        ///   4. Trả về 201 Created kèm ID sản phẩm mới.
        /// 
        /// Phân quyền: [Authorize(Roles = "Admin")]
        /// Body: CreateProductCommand { Name, Description, Price, StockQuantity, CategoryId, Brand, Specifications, ... }
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<int>>> Create(CreateProductCommand command)
        {
            var result = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetById), new { id = result },
                ApiResponse<int>.SuccessResponse(result, "Product created successfully"));
        }

        /// <summary>
        /// [PUT] api/products/{id}
        /// Cập nhật thông tin sản phẩm. Chỉ Admin mới có quyền.
        /// 
        /// Workflow:
        ///   1. Yêu cầu Bearer JWT với role "Admin".
        ///   2. Gán id từ route vào command.Id (để tránh mismatch).
        ///   3. Dispatch UpdateProductCommand qua MediatR. Handler sẽ:
        ///      - Tìm sản phẩm theo Id (false nếu không tìm thấy).
        ///      - Cập nhật các trường được phép.
        ///      - Tự động cập nhật slug nếu tên thay đổi.
        ///   4. Nếu không tìm thấy → 404 Not Found.
        ///   5. Trả về 200 OK nếu thành công.
        /// 
        /// Phân quyền: [Authorize(Roles = "Admin")]
        /// Route param: {id} — ID sản phẩm cần cập nhật.
        /// Body: UpdateProductCommand
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> Update(int id, UpdateProductCommand command)
        {
            // Gán id từ route vào command để handler biết cập nhật sản phẩm nào
            command.Id = id;
            var result = await _mediator.Send(command);
            if (!result)
                return NotFound(ApiResponse<bool>.ErrorResponse("Product not found"));

            return Ok(ApiResponse<bool>.SuccessResponse(true, "Product updated successfully"));
        }

        /// <summary>
        /// [DELETE] api/products/{id}
        /// Xóa sản phẩm. Chỉ Admin mới có quyền.
        /// 
        /// Workflow:
        ///   1. Yêu cầu Bearer JWT với role "Admin".
        ///   2. Dispatch DeleteProductCommand(id) qua MediatR.
        ///   3. Handler thực hiện soft delete: IsActive = false (không xóa vật lý).
        ///      → Giữ lại dữ liệu lịch sử đơn hàng đã đặt trước đó.
        ///   4. Nếu không tìm thấy → 404 Not Found.
        ///   5. Trả về 200 OK nếu thành công.
        /// 
        /// Phân quyền: [Authorize(Roles = "Admin")]
        /// Lưu ý: Đây là SOFT DELETE — sản phẩm vẫn tồn tại trong database với IsActive = false.
        ///        Sản phẩm bị ẩn chỉ không hiển thị với user nhưng vẫn có trong lịch sử đơn hàng.
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var result = await _mediator.Send(new DeleteProductCommand(id));
            if (!result)
                return NotFound(ApiResponse<bool>.ErrorResponse("Product not found"));

            return Ok(ApiResponse<bool>.SuccessResponse(true, "Product deleted successfully"));
        }

        /// <summary>
        /// [GET] api/products/brands
        /// Lấy danh sách tất cả thương hiệu của sản phẩm đang active.
        /// 
        /// Workflow:
        ///   1. Query các sản phẩm có Brand != null và IsActive = true.
        ///   2. Lấy giá trị Brand duy nhất (Distinct), sắp xếp theo alphabet.
        ///   3. Trả về List&lt;string&gt; các tên thương hiệu.
        /// 
        /// Public endpoint. Dùng để populate dropdown/checkbox lọc thương hiệu trong Filter UI.
        /// </summary>
        [HttpGet("brands")]
        public async Task<ActionResult<ApiResponse<List<string>>>> GetBrands()
        {
            var brands = await _unitOfWork.Products.Query()
                .Where(p => p.Brand != null && p.IsActive) // Chỉ lấy sản phẩm active có brand
                .Select(p => p.Brand!)
                .Distinct()   // Loại bỏ trùng lặp
                .OrderBy(b => b) // Sắp xếp alphabet
                .ToListAsync();

            return Ok(ApiResponse<List<string>>.SuccessResponse(brands));
        }

        /// <summary>
        /// [GET] api/products/price-range
        /// Lấy khoảng giá (Min/Max) của tất cả sản phẩm đang active.
        /// 
        /// Workflow:
        ///   1. Query các sản phẩm IsActive = true.
        ///   2. Tính giá thấp nhất (Min) và cao nhất (Max) trong tập dữ liệu.
        ///   3. Trả về object { Min, Max }.
        /// 
        /// Public endpoint. Dùng để thiết lập giới hạn cho slider lọc giá trong Filter UI.
        /// Ví dụ: Min = 500.000đ, Max = 50.000.000đ → hiển thị range slider từ tới.
        /// </summary>
        [HttpGet("price-range")]
        public async Task<ActionResult<ApiResponse<object>>> GetPriceRange()
        {
            var query = _unitOfWork.Products.Query().Where(p => p.IsActive);
            var min = await query.MinAsync(p => p.Price);
            var max = await query.MaxAsync(p => p.Price);

            return Ok(ApiResponse<object>.SuccessResponse(new { Min = min, Max = max }));
        }

        /// <summary>
        /// [GET] api/products/low-stock
        /// Lấy danh sách sản phẩm sắp hết hàng (cần nhập thêm). Chỉ Admin.
        /// 
        /// Workflow:
        ///   1. Yêu cầu Bearer JWT với role "Admin".
        ///   2. Query sản phẩm active có StockQuantity ≤ LowStockThreshold.
        ///   3. Include Category để hiển thị tên danh mục.
        ///   4. Sắp xếp tăng dần theo StockQuantity (ưu tiên hiển thị hàng sắp hết nhất).
        ///   5. Map sang ProductDto với IsLowStock = true.
        ///   6. Trả về danh sách sản phẩm cần nhập hàng.
        /// 
        /// Phân quyền: [Authorize(Roles = "Admin")]
        /// Dùng cho trang quản lý kho hàng trong Admin Panel, có thể kết hợp email alert.
        /// </summary>
        [HttpGet("low-stock")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<List<ProductDto>>>> GetLowStock()
        {
            var products = await _unitOfWork.Products.Query()
                .Include(p => p.Category)
                .Where(p => p.StockQuantity <= p.LowStockThreshold && p.IsActive) // Sắp hết hàng và còn active
                .OrderBy(p => p.StockQuantity) // Hàng ít nhất hiển thị đầu tiên
                .Select(p => new ProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Slug = p.Slug,
                    StockQuantity = p.StockQuantity,
                    IsLowStock = true, // Đánh dấu cứng vì đã lọc theo điều kiện này
                    Brand = p.Brand,
                    Price = p.Price,
                    CategoryName = p.Category != null ? p.Category.Name : null
                })
                .ToListAsync();

            return Ok(ApiResponse<List<ProductDto>>.SuccessResponse(products));
        }
    }
}
