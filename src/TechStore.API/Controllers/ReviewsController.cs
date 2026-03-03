using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Application.Common.Interfaces;
using TechStore.Domain.Entities;
using TechStore.Shared.DTOs;
using TechStore.Shared.Responses;

namespace TechStore.API.Controllers
{
    /// <summary>
    /// Controller quản lý đánh giá sản phẩm (Reviews/Ratings) từ người dùng.
    /// 
    /// Chức năng chính:
    ///   - Lấy danh sách đánh giá của một sản phẩm (public).
    ///   - Người dùng gửi đánh giá mới cho sản phẩm (yêu cầu đăng nhập).
    ///   - Xóa đánh giá (người dùng xóa của mình hoặc Admin xóa bất kỳ).
    /// 
    /// Route gốc: api/reviews
    /// Phân quyền: GET → public; POST → [Authorize]; DELETE → [Authorize] + kiểm tra sở hữu.
    /// 
    /// Ràng buộc nghiệp vụ:
    ///   - Mỗi user chỉ được đánh giá một sản phẩm 1 lần (kiểm tra duplicate).
    ///   - Rating hợp lệ: từ 1 đến 5 sao.
    ///   - Chỉ chủ review hoặc Admin mới được xóa.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ReviewsController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Khởi tạo controller với Unit of Work được inject qua DI.
        /// </summary>
        public ReviewsController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        /// [GET] api/reviews/product/{productId}
        /// Lấy tất cả đánh giá của một sản phẩm.
        /// 
        /// Workflow:
        ///   1. Lấy tất cả Review có ProductId tương ứng từ database.
        ///   2. Map sang ReviewDto: Id, UserId, UserName, ProductId, Rating, Comment, CreatedAt.
        ///   3. UserName: lấy từ navigation property User?.UserName (empty string nếu null).
        ///   4. Sắp xếp giảm dần theo CreatedAt (mới nhất lên đầu).
        ///   5. Trả về danh sách ReviewDto.
        /// 
        /// Route param: {productId} — ID sản phẩm cần lấy reviews.
        /// Public endpoint — không yêu cầu đăng nhập.
        /// Dùng để hiển thị section đánh giá trên trang chi tiết sản phẩm.
        /// </summary>
        [HttpGet("product/{productId}")]
        public async Task<ActionResult<ApiResponse<List<ReviewDto>>>> GetByProduct(int productId)
        {
            var reviews = await _unitOfWork.Reviews.FindAsync(r => r.ProductId == productId);
            var result = reviews.Select(r => new ReviewDto
            {
                Id = r.Id,
                UserId = r.UserId,
                UserName = r.User?.UserName ?? "", // Lấy username từ navigation property
                ProductId = r.ProductId,
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt
            }).OrderByDescending(r => r.CreatedAt).ToList(); // Mới nhất lên đầu

            return Ok(ApiResponse<List<ReviewDto>>.SuccessResponse(result));
        }

        /// <summary>
        /// [POST] api/reviews
        /// Gửi đánh giá mới cho một sản phẩm. Yêu cầu đăng nhập.
        /// 
        /// Workflow:
        ///   1. Lấy UserId từ JWT Claims.
        ///   2. Kiểm tra sản phẩm tồn tại (404 nếu không tìm thấy).
        ///   3. Kiểm tra user đã đánh giá sản phẩm này chưa (400 nếu đã review).
        ///      → Mỗi user chỉ được đánh giá 1 lần cho mỗi sản phẩm.
        ///   4. Validate Rating: phải nằm trong khoảng 1-5 (400 nếu vi phạm).
        ///   5. Tạo entity Review và lưu vào database.
        ///   6. Trả về ReviewDto của review vừa tạo.
        /// 
        /// Body: CreateReviewDto { ProductId, Rating, Comment }
        /// Phân quyền: [Authorize] — yêu cầu đăng nhập.
        /// </summary>
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<ApiResponse<ReviewDto>>> Create(CreateReviewDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            // Kiểm tra sản phẩm tồn tại
            var product = await _unitOfWork.Products.GetByIdAsync(dto.ProductId);
            if (product == null)
                return NotFound(ApiResponse<ReviewDto>.ErrorResponse("Product not found"));

            // Ngăn chặn đánh giá trùng lặp của cùng một user cho cùng một sản phẩm
            var existing = await _unitOfWork.Reviews.FindAsync(
                r => r.UserId == userId && r.ProductId == dto.ProductId);
            if (existing.Any())
                return BadRequest(ApiResponse<ReviewDto>.ErrorResponse("You have already reviewed this product"));

            // Validate rating nằm trong khoảng 1-5 sao
            if (dto.Rating < 1 || dto.Rating > 5)
                return BadRequest(ApiResponse<ReviewDto>.ErrorResponse("Rating must be between 1 and 5"));

            var review = new Review
            {
                UserId = userId,
                ProductId = dto.ProductId,
                Rating = dto.Rating,
                Comment = dto.Comment
            };

            await _unitOfWork.Reviews.AddAsync(review);
            await _unitOfWork.CompleteAsync();

            // Trả về thông tin review vừa tạo
            var result = new ReviewDto
            {
                Id = review.Id,
                UserId = review.UserId,
                ProductId = review.ProductId,
                Rating = review.Rating,
                Comment = review.Comment,
                CreatedAt = review.CreatedAt
            };

            return Ok(ApiResponse<ReviewDto>.SuccessResponse(result, "Review submitted"));
        }

        /// <summary>
        /// [DELETE] api/reviews/{id}
        /// Xóa một đánh giá. Chỉ chủ review hoặc Admin mới có quyền.
        /// 
        /// Workflow:
        ///   1. Lấy UserId từ JWT Claims và kiểm tra role Admin.
        ///   2. Tìm Review theo id (404 nếu không tìm thấy).
        ///   3. Kiểm tra quyền:
        ///      - Nếu không phải Admin VÀ review.UserId khác UserId hiện tại → 403 Forbid.
        ///      - Người dùng chỉ xóa được review của chính mình.
        ///      - Admin có thể xóa bất kỳ review nào (kiểm duyệt nội dung).
        ///   4. Xóa Review khỏi database.
        /// 
        /// Route param: {id} — ID của Review.
        /// Phân quyền: [Authorize] + kiểm tra sở hữu.
        /// Lưu ý: Hard delete — Review bị xóa vĩnh viễn.
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var isAdmin = User.IsInRole("Admin");

            var review = await _unitOfWork.Reviews.GetByIdAsync(id);
            if (review == null)
                return NotFound(ApiResponse<bool>.ErrorResponse("Review not found"));

            // Kiểm tra quyền: chỉ chủ review hoặc Admin mới được xóa (IDOR protection)
            if (!isAdmin && review.UserId != userId)
                return Forbid();

            _unitOfWork.Reviews.Delete(review);
            await _unitOfWork.CompleteAsync();

            return Ok(ApiResponse<bool>.SuccessResponse(true, "Review deleted"));
        }
    }
}
