using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Application.Common.Interfaces;
using TechStore.Domain.Entities;
using TechStore.Shared.DTOs;
using TechStore.Shared.Responses;

namespace TechStore.API.Controllers
{
    /// <summary>
    /// Controller quản lý mã giảm giá (Coupons) trong hệ thống.
    /// 
    /// Chức năng chính:
    ///   - Admin: Xem danh sách tất cả coupon.
    ///   - Admin: Tạo mới coupon với các điều kiện (phần trăm giảm, giới hạn giảm tối đa,
    ///            đơn hàng tối thiểu, ngày hết hạn, giới hạn số lần dùng).
    ///   - Admin: Bật/tắt trạng thái coupon (toggle active/inactive).
    ///   - Admin: Xóa coupon.
    ///   - Public: Kiểm tra và tính toán giá trị giảm của một coupon trước khi thanh toán.
    /// 
    /// Route gốc: api/coupons
    /// Phân quyền: Mặc định [Authorize(Roles = "Admin")] cho toàn bộ controller.
    ///             Endpoint "validate" được override với [AllowAnonymous] để public access.
    /// Coupon code được chuẩn hóa về chữ HOA (UPPERCASE) khi lưu và khi validate.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class CouponsController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Khởi tạo controller với Unit of Work được inject qua DI.
        /// </summary>
        public CouponsController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        /// [GET] api/coupons
        /// Lấy danh sách tất cả mã giảm giá. Chỉ Admin mới có quyền.
        /// 
        /// Workflow:
        ///   1. Lấy toàn bộ coupons từ database.
        ///   2. Map sang CouponDto bao gồm: Code, DiscountPercent, MaxDiscountAmount,
        ///      MinOrderAmount, ExpiryDate, UsageLimit, TimesUsed, IsActive.
        ///   3. Trả về danh sách CouponDto.
        /// 
        /// Phân quyền: [Authorize(Roles = "Admin")]
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<CouponDto>>>> GetAll()
        {
            var coupons = await _unitOfWork.Coupons.GetAllAsync();
            var result = coupons.Select(c => new CouponDto
            {
                Id = c.Id,
                Code = c.Code,
                DiscountPercent = c.DiscountPercent,
                MaxDiscountAmount = c.MaxDiscountAmount,
                MinOrderAmount = c.MinOrderAmount,
                ExpiryDate = c.ExpiryDate,
                UsageLimit = c.UsageLimit,
                TimesUsed = c.TimesUsed,
                IsActive = c.IsActive
            }).ToList();

            return Ok(ApiResponse<List<CouponDto>>.SuccessResponse(result));
        }

        /// <summary>
        /// [POST] api/coupons
        /// Tạo mới một mã giảm giá. Chỉ Admin mới có quyền.
        /// 
        /// Workflow:
        ///   1. Yêu cầu Bearer JWT với role "Admin".
        ///   2. Nhận CreateCouponDto (Code, DiscountPercent, MaxDiscountAmount,
        ///      MinOrderAmount, ExpiryDate, UsageLimit).
        ///   3. Kiểm tra Code đã tồn tại chưa (400 nếu trùng).
        ///   4. Chuẩn hóa Code về UPPERCASE trước khi lưu.
        ///   5. Tạo Coupon với IsActive = true mặc định và TimesUsed = 0.
        ///   6. Lưu vào database và trả về ID coupon mới.
        /// 
        /// Body: CreateCouponDto { Code, DiscountPercent, MaxDiscountAmount, MinOrderAmount, ExpiryDate, UsageLimit }
        /// Lưu ý: Code tự động chuyển thành UPPERCASE.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ApiResponse<int>>> Create(CreateCouponDto dto)
        {
            // Kiểm tra mã coupon đã tồn tại trong hệ thống chưa
            var existing = await _unitOfWork.Coupons.FindAsync(c => c.Code == dto.Code);
            if (existing.Any())
                return BadRequest(ApiResponse<int>.ErrorResponse("Coupon code already exists"));

            var coupon = new Coupon
            {
                Code = dto.Code.ToUpper(), // Chuẩn hóa UPPERCASE
                DiscountPercent = dto.DiscountPercent,
                MaxDiscountAmount = dto.MaxDiscountAmount,
                MinOrderAmount = dto.MinOrderAmount,
                ExpiryDate = dto.ExpiryDate,
                UsageLimit = dto.UsageLimit,
                IsActive = true // Mặc định kích hoạt ngay khi tạo
            };

            await _unitOfWork.Coupons.AddAsync(coupon);
            await _unitOfWork.CompleteAsync();

            return Ok(ApiResponse<int>.SuccessResponse(coupon.Id, "Coupon created"));
        }

        /// <summary>
        /// [PUT] api/coupons/{id}/toggle
        /// Bật/tắt trạng thái active của coupon. Chỉ Admin mới có quyền.
        /// 
        /// Workflow:
        ///   1. Tìm coupon theo id (404 nếu không tìm thấy).
        ///   2. Đảo ngược giá trị IsActive: true → false hoặc false → true.
        ///   3. Lưu thay đổi vào database.
        ///   4. Trả về thông báo rõ ràng: "Coupon activated" hoặc "Coupon deactivated".
        /// 
        /// Dùng để tạm dừng coupon mà không cần xóa (có thể bật lại sau).
        /// </summary>
        [HttpPut("{id}/toggle")]
        public async Task<ActionResult<ApiResponse<bool>>> ToggleActive(int id)
        {
            var coupon = await _unitOfWork.Coupons.GetByIdAsync(id);
            if (coupon == null)
                return NotFound(ApiResponse<bool>.ErrorResponse("Coupon not found"));

            // Toggle: đảo ngược trạng thái active
            coupon.IsActive = !coupon.IsActive;
            _unitOfWork.Coupons.Update(coupon);
            await _unitOfWork.CompleteAsync();

            return Ok(ApiResponse<bool>.SuccessResponse(true, $"Coupon {(coupon.IsActive ? "activated" : "deactivated")}"));
        }

        /// <summary>
        /// [DELETE] api/coupons/{id}
        /// Xóa vĩnh viễn một mã giảm giá. Chỉ Admin mới có quyền.
        /// 
        /// Workflow:
        ///   1. Tìm coupon theo id (404 nếu không tìm thấy).
        ///   2. Xóa coupon khỏi database (hard delete).
        /// 
        /// Lưu ý: Đây là hard delete. Nếu muốn tạm dừng coupon, dùng endpoint toggle thay thế.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var coupon = await _unitOfWork.Coupons.GetByIdAsync(id);
            if (coupon == null)
                return NotFound(ApiResponse<bool>.ErrorResponse("Coupon not found"));

            _unitOfWork.Coupons.Delete(coupon);
            await _unitOfWork.CompleteAsync();

            return Ok(ApiResponse<bool>.SuccessResponse(true, "Coupon deleted"));
        }

        /// <summary>
        /// [POST] api/coupons/validate?orderTotal={amount}
        /// Kiểm tra tính hợp lệ của coupon và tính toán số tiền được giảm.
        /// 
        /// Workflow:
        ///   1. [AllowAnonymous] — endpoint này public, không cần đăng nhập.
        ///   2. Nhận ApplyCouponDto (Code) từ body và orderTotal từ query string.
        ///   3. Tìm coupon theo Code (UPPERCASE).
        ///   4. Kiểm tra tuần tự các điều kiện (đều trả về IsValid=false nếu vi phạm):
        ///      a. Coupon tồn tại và IsActive.
        ///      b. Coupon chưa hết hạn (ExpiryDate > DateTime.UtcNow).
        ///      c. Chưa vượt quá giới hạn sử dụng (TimesUsed &lt; UsageLimit).
        ///      d. Tổng đơn hàng đạt mức tối thiểu (orderTotal >= MinOrderAmount).
        ///   5. Tính toán số tiền giảm:
        ///      - discountAmount = orderTotal × DiscountPercent / 100
        ///      - Áp dụng giới hạn tối đa MaxDiscountAmount nếu có.
        ///   6. Trả về CouponValidationResultDto { IsValid, Message, DiscountAmount }.
        /// 
        /// Phân quyền: [AllowAnonymous] — override [Authorize(Roles="Admin")] của controller.
        /// Query param: orderTotal — tổng giá trị đơn hàng trước khi giảm (decimal).
        /// Body: ApplyCouponDto { Code }
        /// </summary>
        [HttpPost("validate")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<CouponValidationResultDto>>> ValidateCoupon(ApplyCouponDto dto, [FromQuery] decimal orderTotal)
        {
            var coupons = await _unitOfWork.Coupons.FindAsync(c => c.Code == dto.Code.ToUpper());
            var coupon = coupons.FirstOrDefault();

            // Kiểm tra 1: Coupon tồn tại và đang active
            if (coupon == null || !coupon.IsActive)
                return Ok(ApiResponse<CouponValidationResultDto>.SuccessResponse(
                    new CouponValidationResultDto { IsValid = false, Message = "Invalid coupon code" }));

            // Kiểm tra 2: Coupon chưa hết hạn
            if (coupon.ExpiryDate < DateTime.UtcNow)
                return Ok(ApiResponse<CouponValidationResultDto>.SuccessResponse(
                    new CouponValidationResultDto { IsValid = false, Message = "Coupon has expired" }));

            // Kiểm tra 3: Chưa vượt quá số lần sử dụng tối đa
            if (coupon.TimesUsed >= coupon.UsageLimit)
                return Ok(ApiResponse<CouponValidationResultDto>.SuccessResponse(
                    new CouponValidationResultDto { IsValid = false, Message = "Coupon usage limit reached" }));

            // Kiểm tra 4: Đơn hàng đạt giá trị tối thiểu để áp dụng coupon
            if (orderTotal < coupon.MinOrderAmount)
                return Ok(ApiResponse<CouponValidationResultDto>.SuccessResponse(
                    new CouponValidationResultDto { IsValid = false, Message = $"Minimum order amount: {coupon.MinOrderAmount:C}" }));

            // Tính toán số tiền được giảm
            var discountAmount = orderTotal * coupon.DiscountPercent / 100;
            // Áp dụng giới hạn giảm tối đa nếu có cấu hình
            if (coupon.MaxDiscountAmount.HasValue && discountAmount > coupon.MaxDiscountAmount.Value)
                discountAmount = coupon.MaxDiscountAmount.Value;

            return Ok(ApiResponse<CouponValidationResultDto>.SuccessResponse(
                new CouponValidationResultDto
                {
                    IsValid = true,
                    Message = $"Coupon applied: -{coupon.DiscountPercent}%",
                    DiscountAmount = discountAmount
                }));
        }
    }
}
