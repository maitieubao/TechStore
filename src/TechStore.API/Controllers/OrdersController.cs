using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechStore.Application.Common.Interfaces;
using TechStore.Application.Features.Orders.Commands;
using TechStore.Application.Features.Orders.Queries;
using TechStore.Domain.Entities;
using TechStore.Shared.DTOs;
using TechStore.Shared.Responses;

namespace TechStore.API.Controllers
{
    /// <summary>
    /// Controller quản lý đơn hàng (Orders) trong hệ thống TechStore.
    /// 
    /// Chức năng chính:
    ///   - Admin: Lấy toàn bộ danh sách đơn hàng.
    ///   - User/Admin: Lấy chi tiết một đơn hàng (có kiểm tra quyền sở hữu).
    ///   - User: Lấy danh sách đơn hàng của bản thân.
    ///   - User: Tạo đơn hàng mới (có hỗ trợ coupon, tự động trừ tồn kho, gửi email xác nhận).
    ///   - Admin: Cập nhật trạng thái đơn hàng (Pending → Shipping → Delivered/Cancelled).
    ///   - User: Hủy đơn hàng (chỉ khi ở trạng thái Pending, tự động hoàn kho).
    /// 
    /// Route gốc: api/orders
    /// Phân quyền: [Authorize] — tất cả endpoints yêu cầu đăng nhập.
    ///             Admin-only: GET all, PUT status.
    /// 
    /// Kiến trúc: Sử dụng MediatR/CQRS Pattern cho các thao tác phức tạp.
    ///   - CreateOrderCommand: Xử lý tạo đơn hàng với coupon và quản lý tồn kho.
    ///   - GetAllOrdersQuery / GetOrderByIdQuery / GetOrdersByUserIdQuery: Truy vấn đơn hàng.
    /// 
    /// Trạng thái đơn hàng hợp lệ: Pending → Shipping → Delivered | Cancelled
    ///   - Không thể thay đổi trạng thái nếu đơn đã "Delivered" hoặc "Cancelled".
    ///   - Hủy đơn → tự động hoàn trả số lượng tồn kho.
    /// 
    /// Background Jobs: Gửi email xác nhận đơn hàng và email cập nhật trạng thái bất đồng bộ.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailService _emailService;
        private readonly IBackgroundJobService _backgroundJobService;

        /// <summary>
        /// Khởi tạo controller với các dependencies được inject qua DI.
        /// </summary>
        /// <param name="mediator">MediatR để dispatch Commands và Queries.</param>
        /// <param name="unitOfWork">Unit of Work để truy cập trực tiếp repository.</param>
        /// <param name="emailService">Service gửi email thông báo.</param>
        /// <param name="backgroundJobService">Service xử lý tác vụ nền (Hangfire).</param>
        public OrdersController(
            IMediator mediator, 
            IUnitOfWork unitOfWork, 
            IEmailService emailService,
            IBackgroundJobService backgroundJobService)
        {
            _mediator = mediator;
            _unitOfWork = unitOfWork;
            _emailService = emailService;
            _backgroundJobService = backgroundJobService;
        }

        /// <summary>
        /// [GET] api/orders
        /// Lấy toàn bộ danh sách đơn hàng trong hệ thống. Chỉ Admin mới có quyền.
        /// 
        /// Workflow:
        ///   1. Dispatch GetAllOrdersQuery qua MediatR.
        ///   2. Handler query trả về danh sách tất cả OrderDto.
        ///   3. Trả về 200 OK với danh sách đơn hàng.
        /// 
        /// Phân quyền: [Authorize(Roles = "Admin")]
        /// Dùng cho trang quản lý đơn hàng trong Admin Panel.
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<List<OrderDto>>>> GetAll()
        {
            var result = await _mediator.Send(new GetAllOrdersQuery());
            return Ok(ApiResponse<List<OrderDto>>.SuccessResponse(result));
        }

        /// <summary>
        /// [GET] api/orders/{id}
        /// Lấy chi tiết một đơn hàng theo ID. Admin hoặc chủ đơn hàng mới có quyền xem.
        /// 
        /// Workflow:
        ///   1. Dispatch GetOrderByIdQuery qua MediatR.
        ///   2. Nếu không tìm thấy → 404 Not Found.
        ///   3. Kiểm tra quyền truy cập:
        ///      - Nếu không phải Admin VÀ UserId trong đơn không khớp với UserId hiện tại → 403 Forbid.
        ///      - Nếu là Admin hoặc chủ sở hữu → trả về OrderDto.
        /// 
        /// Route param: {id} — ID đơn hàng.
        /// Bảo mật: Ngăn user A xem đơn hàng của user B (IDOR protection).
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<OrderDto>>> GetById(int id)
        {
            var result = await _mediator.Send(new GetOrderByIdQuery(id));
            if (result == null)
                return NotFound(ApiResponse<OrderDto>.ErrorResponse("Order not found"));

            // Kiểm tra quyền: chỉ admin hoặc chủ đơn hàng mới được xem
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            if (!isAdmin && result.UserId != userId)
                return Forbid();

            return Ok(ApiResponse<OrderDto>.SuccessResponse(result));
        }

        /// <summary>
        /// [GET] api/orders/my-orders
        /// Lấy danh sách tất cả đơn hàng của người dùng đang đăng nhập.
        /// 
        /// Workflow:
        ///   1. Lấy UserId từ JWT Claims.
        ///   2. Dispatch GetOrdersByUserIdQuery(userId) qua MediatR.
        ///   3. Handler query lọc đơn hàng theo UserId và trả về danh sách OrderDto.
        /// 
        /// Mỗi user chỉ nhìn thấy đơn hàng của chính mình.
        /// Dùng cho trang "Đơn hàng của tôi" trên giao diện người dùng.
        /// </summary>
        [HttpGet("my-orders")]
        public async Task<ActionResult<ApiResponse<List<OrderDto>>>> GetMyOrders()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var result = await _mediator.Send(new GetOrdersByUserIdQuery(userId));
            return Ok(ApiResponse<List<OrderDto>>.SuccessResponse(result));
        }

        /// <summary>
        /// [POST] api/orders
        /// Tạo đơn hàng mới cho người dùng đang đăng nhập.
        /// 
        /// Workflow:
        ///   1. Lấy UserId từ JWT Claims.
        ///   2. Tạo CreateOrderCommand từ CreateOrderDto:
        ///      { UserId, Items, ShippingAddress, PhoneNumber, Note, CouponCode? }
        ///   3. Dispatch CreateOrderCommand qua MediatR. Handler sẽ:
        ///      a. Kiểm tra tồn kho từng sản phẩm.
        ///      b. Trừ tồn kho (StockQuantity -= Quantity).
        ///      c. Áp dụng coupon nếu CouponCode hợp lệ (tính DiscountAmount, tăng TimesUsed).
        ///      d. Tạo Order với các OrderItems.
        ///      e. Lưu vào database.
        ///   4. Sau khi tạo thành công:
        ///      - Enqueue background job gửi email xác nhận đơn hàng.
        ///   5. Trả về 201 Created với OrderDto.
        ///   6. Nếu có lỗi (hết hàng, coupon không hợp lệ) → 400 Bad Request.
        /// 
        /// Body: CreateOrderDto { Items, ShippingAddress, PhoneNumber, Note, CouponCode? }
        /// Email: Gửi xác nhận đơn hàng bất đồng bộ qua background job.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ApiResponse<OrderDto>>> Create(CreateOrderDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var command = new CreateOrderCommand
            {
                UserId = userId,
                Items = dto.Items,
                ShippingAddress = dto.ShippingAddress,
                PhoneNumber = dto.PhoneNumber,
                Note = dto.Note,
                CouponCode = dto.CouponCode
            };

            try
            {
                var result = await _mediator.Send(command);

                // Gửi email xác nhận đơn hàng bất đồng bộ qua Hangfire background job
                _backgroundJobService.Enqueue(() => _emailService.SendOrderConfirmationAsync(result.Id, userId));

                return CreatedAtAction(nameof(GetById), new { id = result.Id },
                    ApiResponse<OrderDto>.SuccessResponse(result, "Order created successfully"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<OrderDto>.ErrorResponse(ex.Message));
            }
        }

        /// <summary>
        /// [PUT] api/orders/{id}/status
        /// Cập nhật trạng thái đơn hàng. Chỉ Admin mới có quyền.
        /// 
        /// Workflow:
        ///   1. Yêu cầu Bearer JWT với role "Admin".
        ///   2. Kiểm tra Status hợp lệ: chỉ chấp nhận "Pending", "Shipping", "Delivered", "Cancelled".
        ///   3. Tìm đơn hàng theo id (404 nếu không tìm thấy).
        ///   4. Kiểm tra ràng buộc trạng thái:
        ///      - Nếu đơn đã "Delivered" hoặc "Cancelled" → từ chối thay đổi (400).
        ///      → Ngăn chặn việc tái kích hoạt đơn đã kết thúc.
        ///   5. Cập nhật trạng thái mới và UpdatedAt.
        ///   6. Nếu trạng thái mới là "Cancelled" (và trước đó chưa Cancelled):
        ///      - Lấy danh sách OrderItems của đơn hàng.
        ///      - Hoàn trả tồn kho: StockQuantity += Quantity cho từng sản phẩm.
        ///   7. Lưu tất cả thay đổi vào database.
        ///   8. Enqueue background job gửi email thông báo trạng thái mới cho user.
        /// 
        /// Phân quyền: [Authorize(Roles = "Admin")]
        /// Body: UpdateOrderStatusDto { Status }
        /// Email: Gửi thông báo cập nhật trạng thái bất đồng bộ.
        /// </summary>
        [HttpPut("{id}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateStatus(int id, UpdateOrderStatusDto dto)
        {
            // Kiểm tra giá trị Status hợp lệ
            var validStatuses = new[] { "Pending", "Shipping", "Delivered", "Cancelled" };
            if (!validStatuses.Contains(dto.Status))
                return BadRequest(ApiResponse<bool>.ErrorResponse($"Invalid status. Valid: {string.Join(", ", validStatuses)}"));

            var order = await _unitOfWork.Orders.GetByIdAsync(id);
            if (order == null)
                return NotFound(ApiResponse<bool>.ErrorResponse("Order not found"));

            var previousStatus = order.Status;
            
            // Không cho phép thay đổi trạng thái nếu đơn hàng đã Giao thành công hoặc Đã huỷ
            if (previousStatus == "Delivered" || previousStatus == "Cancelled")
            {
                return BadRequest(ApiResponse<bool>.ErrorResponse($"Không thể thay đổi trạng thái của đơn hàng đã '{previousStatus}'."));
            }

            order.Status = dto.Status;
            order.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Orders.Update(order);

            // Hoàn trả tồn kho khi Admin hủy đơn hàng
            if (dto.Status == "Cancelled" && previousStatus != "Cancelled")
            {
                var orderItems = await _unitOfWork.OrderItems.Query()
                    .Where(oi => oi.OrderId == id)
                    .ToListAsync();

                foreach (var item in orderItems)
                {
                    var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId);
                    if (product != null)
                    {
                        product.StockQuantity += item.Quantity; // Hoàn trả tồn kho
                        product.UpdatedAt = DateTime.UtcNow;
                        _unitOfWork.Products.Update(product);
                    }
                }
            }

            await _unitOfWork.CompleteAsync();

            // Gửi email thông báo trạng thái mới cho người dùng (bất đồng bộ)
            _backgroundJobService.Enqueue(() => _emailService.SendOrderStatusUpdateAsync(id, dto.Status, order.UserId));

            return Ok(ApiResponse<bool>.SuccessResponse(true, $"Order status updated to '{dto.Status}'"));
        }

        /// <summary>
        /// [PUT] api/orders/{id}/cancel
        /// Người dùng tự hủy đơn hàng của mình. Chỉ được hủy khi đơn ở trạng thái "Pending".
        /// 
        /// Workflow:
        ///   1. Lấy UserId từ JWT Claims.
        ///   2. Tìm đơn hàng theo id (404 nếu không tìm thấy).
        ///   3. Kiểm tra quyền sở hữu: chỉ chủ đơn hoặc Admin mới được hủy.
        ///   4. Kiểm tra trạng thái: chỉ cho phép hủy khi Status = "Pending" (400 nếu không phải Pending).
        ///      → Đơn đang vận chuyển (Shipping) không thể hủy được nữa.
        ///   5. Cập nhật Status = "Cancelled".
        ///   6. Hoàn trả tồn kho: tải OrderItems và cộng lại StockQuantity cho từng sản phẩm.
        ///   7. Lưu tất cả thay đổi vào database.
        /// 
        /// Khác với UpdateStatus: endpoint này dành cho User tự hủy, không cần role Admin.
        /// Bảo mật: Kiểm tra UserId để ngăn user A hủy đơn của user B (IDOR protection).
        /// </summary>
        [HttpPut("{id}/cancel")]
        public async Task<ActionResult<ApiResponse<bool>>> CancelOrder(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var order = await _unitOfWork.Orders.GetByIdAsync(id);

            if (order == null)
                return NotFound(ApiResponse<bool>.ErrorResponse("Order not found"));

            // Kiểm tra quyền: chỉ chủ đơn hoặc Admin mới được hủy
            if (order.UserId != userId && !User.IsInRole("Admin"))
                return Forbid();

            // Chỉ cho hủy đơn ở trạng thái Pending
            if (order.Status != "Pending")
                return BadRequest(ApiResponse<bool>.ErrorResponse("Only pending orders can be cancelled"));

            order.Status = "Cancelled";
            order.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Orders.Update(order);

            // Hoàn trả tồn kho: cộng lại số lượng từng sản phẩm trong đơn
            var orderItems = await _unitOfWork.OrderItems.Query()
                .Where(oi => oi.OrderId == id)
                .ToListAsync();

            foreach (var item in orderItems)
            {
                var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId);
                if (product != null)
                {
                    product.StockQuantity += item.Quantity; // Hoàn trả tồn kho
                    product.UpdatedAt = DateTime.UtcNow;
                    _unitOfWork.Products.Update(product);
                }
            }

            await _unitOfWork.CompleteAsync();

            return Ok(ApiResponse<bool>.SuccessResponse(true, "Order cancelled and stock restored"));
        }
    }
}
