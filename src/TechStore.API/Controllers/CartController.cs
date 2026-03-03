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
    /// Controller quản lý giỏ hàng (Shopping Cart) của người dùng.
    /// 
    /// Chức năng chính:
    ///   - Lấy toàn bộ giỏ hàng của người dùng hiện tại (kèm thông tin sản phẩm, ảnh).
    ///   - Thêm sản phẩm vào giỏ hàng (kiểm tra tồn kho, gộp nếu đã có).
    ///   - Cập nhật số lượng sản phẩm trong giỏ (xóa nếu quantity ≤ 0).
    ///   - Xóa một sản phẩm khỏi giỏ hàng.
    ///   - Xóa toàn bộ giỏ hàng (clear cart).
    /// 
    /// Route gốc: api/cart
    /// Phân quyền: [Authorize] — tất cả endpoints yêu cầu đăng nhập (JWT Bearer).
    /// Dữ liệu được tách biệt theo UserId nên mỗi user chỉ thấy giỏ hàng của mình.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CartController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Khởi tạo controller với Unit of Work được inject qua DI.
        /// </summary>
        public CartController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        /// Lấy UserId từ JWT Claims của người dùng đang đăng nhập.
        /// Helper method dùng chung cho tất cả actions trong controller.
        /// </summary>
        private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        /// <summary>
        /// [GET] api/cart
        /// Lấy toàn bộ giỏ hàng của người dùng hiện tại.
        /// 
        /// Workflow:
        ///   1. Lấy UserId từ JWT Claims.
        ///   2. Query CartItems với Eager Loading: Include Product → ThenInclude Images.
        ///   3. Dùng AsNoTracking() để tối ưu hiệu năng (chỉ đọc, không cần tracking).
        ///   4. Lọc lấy ảnh primary; nếu không có thì lấy ảnh đầu tiên.
        ///   5. Tính TotalAmount (tổng tiền) và TotalItems (tổng số lượng) trong CartSummaryDto.
        ///   6. Trả về CartSummaryDto gồm danh sách items + tổng tiền + tổng số lượng.
        /// 
        /// Tối ưu: Chỉ 1 round-trip database nhờ Include/ThenInclude.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<CartSummaryDto>>> GetCart()
        {
            var userId = GetUserId();
            
            // Optimized query ensuring 1 round-trip
            var items = await _unitOfWork.CartItems.Query()
                .AsNoTracking()
                .Include(c => c.Product)
                    .ThenInclude(p => p.Images)
                .Where(c => c.UserId == userId)
                .ToListAsync();

            var cartItems = items
                .Where(i => i.Product != null)
                .Select(item => new CartItemDto
                {
                    Id = item.Id,
                    ProductId = item.ProductId,
                    ProductName = item.Product.Name,
                    ProductPrice = item.Product.Price,
                    ProductImageUrl = item.Product.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl 
                                      ?? item.Product.Images.FirstOrDefault()?.ImageUrl,
                    Quantity = item.Quantity
                })
                .ToList();

            var summary = new CartSummaryDto
            {
                Items = cartItems,
                TotalAmount = cartItems.Sum(i => i.Subtotal),
                TotalItems = cartItems.Sum(i => i.Quantity)
            };

            return Ok(ApiResponse<CartSummaryDto>.SuccessResponse(summary));
        }

        /// <summary>
        /// [POST] api/cart
        /// Thêm sản phẩm vào giỏ hàng.
        /// 
        /// Workflow:
        ///   1. Lấy UserId từ JWT Claims.
        ///   2. Kiểm tra sản phẩm tồn tại (404 nếu không tìm thấy).
        ///   3. Kiểm tra số lượng tồn kho (400 nếu không đủ hàng).
        ///   4. Kiểm tra sản phẩm đã có trong giỏ chưa:
        ///      - Đã có: cộng thêm số lượng vào item hiện tại (upsert).
        ///      - Chưa có: tạo CartItem mới.
        ///   5. Lưu thay đổi vào database.
        /// 
        /// Body: AddToCartDto { ProductId, Quantity }
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ApiResponse<string>>> AddToCart(AddToCartDto dto)
        {
            var userId = GetUserId();
            var product = await _unitOfWork.Products.GetByIdAsync(dto.ProductId);
            if (product == null)
                return NotFound(ApiResponse<string>.ErrorResponse("Product not found"));

            if (product.StockQuantity < dto.Quantity)
                return BadRequest(ApiResponse<string>.ErrorResponse($"Insufficient stock. Available: {product.StockQuantity}"));

            var existingItems = await _unitOfWork.CartItems.FindAsync(
                c => c.UserId == userId && c.ProductId == dto.ProductId);
            var existing = existingItems.FirstOrDefault();

            if (existing != null)
            {
                // Sản phẩm đã có trong giỏ → tăng số lượng
                existing.Quantity += dto.Quantity;
                _unitOfWork.CartItems.Update(existing);
            }
            else
            {
                // Sản phẩm chưa có → tạo mới
                var cartItem = new CartItem
                {
                    UserId = userId,
                    ProductId = dto.ProductId,
                    Quantity = dto.Quantity
                };
                await _unitOfWork.CartItems.AddAsync(cartItem);
            }

            await _unitOfWork.CompleteAsync();
            return Ok(ApiResponse<string>.SuccessResponse("Added to cart"));
        }

        /// <summary>
        /// [PUT] api/cart/{id}
        /// Cập nhật số lượng của một CartItem trong giỏ hàng.
        /// 
        /// Workflow:
        ///   1. Tìm CartItem theo id.
        ///   2. Kiểm tra CartItem tồn tại và thuộc về người dùng hiện tại (bảo mật).
        ///   3. Nếu Quantity ≤ 0 → tự động xóa item khỏi giỏ hàng.
        ///   4. Nếu Quantity > 0 → cập nhật số lượng mới.
        ///   5. Lưu thay đổi vào database.
        /// 
        /// Route param: {id} — ID của CartItem (không phải ProductId).
        /// Body: UpdateCartItemDto { Quantity }
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<string>>> UpdateQuantity(int id, UpdateCartItemDto dto)
        {
            var userId = GetUserId();
            var cartItem = await _unitOfWork.CartItems.GetByIdAsync(id);

            // Kiểm tra item tồn tại và thuộc về đúng user (tránh IDOR)
            if (cartItem == null || cartItem.UserId != userId)
                return NotFound(ApiResponse<string>.ErrorResponse("Cart item not found"));

            if (dto.Quantity <= 0)
            {
                // Quantity <= 0 → xóa item khỏi giỏ
                _unitOfWork.CartItems.Delete(cartItem);
            }
            else
            {
                // Cập nhật số lượng mới
                cartItem.Quantity = dto.Quantity;
                _unitOfWork.CartItems.Update(cartItem);
            }

            await _unitOfWork.CompleteAsync();
            return Ok(ApiResponse<string>.SuccessResponse("Cart updated"));
        }

        /// <summary>
        /// [DELETE] api/cart/{id}
        /// Xóa một sản phẩm khỏi giỏ hàng.
        /// 
        /// Workflow:
        ///   1. Tìm CartItem theo id.
        ///   2. Kiểm tra CartItem tồn tại và thuộc về người dùng hiện tại (chống IDOR).
        ///   3. Xóa CartItem khỏi database.
        /// 
        /// Route param: {id} — ID của CartItem.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<string>>> RemoveItem(int id)
        {
            var userId = GetUserId();
            var cartItem = await _unitOfWork.CartItems.GetByIdAsync(id);

            if (cartItem == null || cartItem.UserId != userId)
                return NotFound(ApiResponse<string>.ErrorResponse("Cart item not found"));

            _unitOfWork.CartItems.Delete(cartItem);
            await _unitOfWork.CompleteAsync();

            return Ok(ApiResponse<string>.SuccessResponse("Item removed from cart"));
        }

        /// <summary>
        /// [DELETE] api/cart/clear
        /// Xóa toàn bộ giỏ hàng của người dùng hiện tại.
        /// 
        /// Workflow:
        ///   1. Lấy UserId từ JWT Claims.
        ///   2. Lấy tất cả CartItem thuộc về user này.
        ///   3. Xóa từng item khỏi database.
        ///   4. Lưu thay đổi (1 lần CommitAsync duy nhất).
        /// 
        /// Thường được gọi sau khi checkout thành công để làm sạch giỏ hàng.
        /// </summary>
        [HttpDelete("clear")]
        public async Task<ActionResult<ApiResponse<string>>> ClearCart()
        {
            var userId = GetUserId();
            var items = await _unitOfWork.CartItems.FindAsync(c => c.UserId == userId);

            foreach (var item in items)
            {
                _unitOfWork.CartItems.Delete(item);
            }

            await _unitOfWork.CompleteAsync();
            return Ok(ApiResponse<string>.SuccessResponse("Cart cleared"));
        }
    }
}
