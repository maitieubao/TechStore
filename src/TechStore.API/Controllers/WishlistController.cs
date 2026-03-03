using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Application.Common.Interfaces;
using TechStore.Domain.Entities;
using TechStore.Shared.DTOs;
using TechStore.Shared.Responses;
using Microsoft.EntityFrameworkCore;

namespace TechStore.API.Controllers
{
    /// <summary>
    /// Controller quản lý danh sách yêu thích (Wishlist) của người dùng.
    /// 
    /// Chức năng chính:
    ///   - Lấy toàn bộ wishlist của người dùng hiện tại (kèm ảnh, giá, trạng thái hàng).
    ///   - Thêm sản phẩm vào wishlist (kiểm tra trùng lặp).
    ///   - Xóa sản phẩm khỏi wishlist.
    ///   - Kiểm tra nhanh xem một sản phẩm có trong wishlist không.
    /// 
    /// Route gốc: api/wishlist
    /// Phân quyền: [Authorize] — tất cả endpoints yêu cầu đăng nhập (JWT Bearer).
    ///             Dữ liệu wishlist được tách biệt theo UserId.
    /// 
    /// Tối ưu hiệu năng: GetWishlist dùng Eager Loading (AsNoTracking + Include/ThenInclude)
    ///                   để tránh N+1 queries khi tải ảnh sản phẩm.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class WishlistController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Khởi tạo controller với Unit of Work được inject qua DI.
        /// </summary>
        public WishlistController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        /// Helper lấy UserId từ JWT Claims của người dùng đang đăng nhập.
        /// </summary>
        private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        /// <summary>
        /// [GET] api/wishlist
        /// Lấy toàn bộ danh sách yêu thích của người dùng hiện tại.
        /// 
        /// Workflow:
        ///   1. Lấy UserId từ JWT Claims.
        ///   2. Query Wishlists với Eager Loading (1 query duy nhất):
        ///      Include Product → ThenInclude Images.
        ///      Sử dụng AsNoTracking() vì chỉ đọc, không cần tracking.
        ///   3. Lọc bỏ items có Product = null (sản phẩm đã bị xóa).
        ///   4. Map sang WishlistItemDto:
        ///      - ProductImageUrl: Ưu tiên ảnh primary, nếu không có thì lấy ảnh đầu tiên.
        ///      - InStock: true nếu StockQuantity > 0.
        ///   5. Trả về danh sách WishlistItemDto.
        /// 
        /// Tối ưu: Chỉ 1 round-trip database nhờ Include/ThenInclude.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<WishlistItemDto>>>> GetWishlist()
        {
            var userId = GetUserId();
            
            // Optimized query với Eager Loading (1 query thay vì N+1)
            var items = await _unitOfWork.Wishlists.Query()
                .AsNoTracking() // Chỉ đọc → không cần EF tracking
                .Include(w => w.Product)
                    .ThenInclude(p => p.Images)
                .Where(w => w.UserId == userId)
                .ToListAsync();

            var result = items
                .Where(i => i.Product != null) // Lọc bỏ item mà sản phẩm đã bị xóa
                .Select(item => new WishlistItemDto
                {
                    Id = item.Id,
                    ProductId = item.ProductId,
                    ProductName = item.Product.Name,
                    ProductPrice = item.Product.Price,
                    // Ưu tiên ảnh primary; fallback về ảnh đầu tiên nếu không có primary
                    ProductImageUrl = item.Product.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl 
                                      ?? item.Product.Images.FirstOrDefault()?.ImageUrl,
                    ProductSlug = item.Product.Slug,
                    InStock = item.Product.StockQuantity > 0 // Kiểm tra còn hàng
                })
                .ToList();

            return Ok(ApiResponse<List<WishlistItemDto>>.SuccessResponse(result));
        }

        /// <summary>
        /// [POST] api/wishlist/{productId}
        /// Thêm một sản phẩm vào danh sách yêu thích.
        /// 
        /// Workflow:
        ///   1. Lấy UserId từ JWT Claims.
        ///   2. Kiểm tra sản phẩm tồn tại (404 nếu không tìm thấy).
        ///   3. Kiểm tra sản phẩm đã có trong wishlist của user chưa (400 nếu đã có).
        ///      → Ngăn chặn thêm trùng lặp.
        ///   4. Tạo entity Wishlist { UserId, ProductId } và lưu vào database.
        ///   5. Trả về 200 OK.
        /// 
        /// Route param: {productId} — ID sản phẩm cần thêm vào wishlist.
        /// Không cần body (productId lấy từ route).
        /// </summary>
        [HttpPost("{productId}")]
        public async Task<ActionResult<ApiResponse<string>>> AddToWishlist(int productId)
        {
            var userId = GetUserId();

            var product = await _unitOfWork.Products.GetByIdAsync(productId);
            if (product == null)
                return NotFound(ApiResponse<string>.ErrorResponse("Product not found"));

            // Kiểm tra đã có trong wishlist chưa (tránh thêm trùng)
            var existing = await _unitOfWork.Wishlists.FindAsync(
                w => w.UserId == userId && w.ProductId == productId);
            if (existing.Any())
                return BadRequest(ApiResponse<string>.ErrorResponse("Product already in wishlist"));

            var wishlistItem = new Wishlist
            {
                UserId = userId,
                ProductId = productId
            };

            await _unitOfWork.Wishlists.AddAsync(wishlistItem);
            await _unitOfWork.CompleteAsync();

            return Ok(ApiResponse<string>.SuccessResponse("Added to wishlist"));
        }

        /// <summary>
        /// [DELETE] api/wishlist/{productId}
        /// Xóa một sản phẩm khỏi danh sách yêu thích.
        /// 
        /// Workflow:
        ///   1. Lấy UserId từ JWT Claims.
        ///   2. Tìm Wishlist item theo UserId VÀ ProductId.
        ///   3. Nếu không tìm thấy → 404 Not Found.
        ///   4. Xóa item khỏi database.
        /// 
        /// Route param: {productId} — ID sản phẩm cần xóa khỏi wishlist.
        /// Bảo mật: Tìm theo cả UserId + ProductId → user chỉ xóa được item của chính mình.
        /// </summary>
        [HttpDelete("{productId}")]
        public async Task<ActionResult<ApiResponse<string>>> RemoveFromWishlist(int productId)
        {
            var userId = GetUserId();
            var items = await _unitOfWork.Wishlists.FindAsync(
                w => w.UserId == userId && w.ProductId == productId);
            var item = items.FirstOrDefault();

            if (item == null)
                return NotFound(ApiResponse<string>.ErrorResponse("Item not in wishlist"));

            _unitOfWork.Wishlists.Delete(item);
            await _unitOfWork.CompleteAsync();

            return Ok(ApiResponse<string>.SuccessResponse("Removed from wishlist"));
        }

        /// <summary>
        /// [GET] api/wishlist/check/{productId}
        /// Kiểm tra nhanh xem một sản phẩm có đang có trong wishlist của user hay không.
        /// 
        /// Workflow:
        ///   1. Lấy UserId từ JWT Claims.
        ///   2. Tìm Wishlist item theo UserId VÀ ProductId.
        ///   3. Trả về true nếu có, false nếu không có.
        /// 
        /// Route param: {productId} — ID sản phẩm cần kiểm tra.
        /// Response: ApiResponse&lt;bool&gt; — true/false.
        /// Dùng để hiển thị trạng thái icon trái tim (đã yêu thích / chưa yêu thích)
        /// trên trang danh sách và chi tiết sản phẩm mà không cần tải toàn bộ wishlist.
        /// </summary>
        [HttpGet("check/{productId}")]
        public async Task<ActionResult<ApiResponse<bool>>> IsInWishlist(int productId)
        {
            var userId = GetUserId();
            var items = await _unitOfWork.Wishlists.FindAsync(
                w => w.UserId == userId && w.ProductId == productId);

            return Ok(ApiResponse<bool>.SuccessResponse(items.Any()));
        }
    }
}
