using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechStore.Application.Common.Interfaces;
using TechStore.Domain.Entities;
using TechStore.Shared.DTOs;
using TechStore.Shared.Responses;

namespace TechStore.API.Controllers
{
    /// <summary>
    /// Controller xử lý toàn bộ luồng thanh toán trực tuyến qua cổng PayOS.
    /// 
    /// Chức năng chính:
    ///   - Tạo link thanh toán PayOS cho đơn hàng (user authenticated).
    ///   - Xử lý redirect từ PayOS sau khi user hoàn tất thanh toán (Return URL).
    ///   - Xử lý redirect khi user hủy thanh toán (Cancel URL).
    ///   - Nhận webhook từ server PayOS sau khi thanh toán (server-to-server).
    ///   - Truy vấn trạng thái thanh toán theo orderCode.
    ///   - Lấy lịch sử thanh toán theo đơn hàng.
    ///   - Admin: Xem tất cả payments có phân trang và lọc.
    /// 
    /// Route gốc: api/payments
    /// 
    /// Luồng thanh toán PayOS chuẩn:
    ///   [1] User → POST /create-payment-url → Nhận CheckoutUrl
    ///   [2] User → Redirect đến PayOS CheckoutUrl → Thực hiện thanh toán
    ///   [3a] Thành công → PayOS redirect → GET /payos-return (Return URL)
    ///   [3b] Hủy      → PayOS redirect → GET /payos-cancel (Cancel URL)
    ///   [4] Song song → PayOS server gửi → POST /payos-webhook (Webhook - đáng tin cậy hơn)
    /// 
    /// OrderCode encoding: orderCode = orderId × 1_000_000 + randomSuffix
    ///   → Decode: orderId = orderCode / 1_000_000
    /// 
    /// Trạng thái payment: Processing → Completed | Failed | Cancelled
    /// Trạng thái đơn hàng sau thanh toán: PaymentStatus = "Paid", Status = "Shipping"
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentsController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPaymentService _paymentService;
        private readonly IEmailService _emailService;
        private readonly IBackgroundJobService _backgroundJobService;
        private readonly ILogger<PaymentsController> _logger;

        /// <summary>
        /// Khởi tạo controller với các dependencies được inject qua DI.
        /// </summary>
        public PaymentsController(
            IUnitOfWork unitOfWork,
            IPaymentService paymentService,
            IEmailService emailService,
            IBackgroundJobService backgroundJobService,
            ILogger<PaymentsController> logger)
        {
            _unitOfWork = unitOfWork;
            _paymentService = paymentService;
            _emailService = emailService;
            _backgroundJobService = backgroundJobService;
            _logger = logger;
        }

        // ─────────────────────────────────────────────────────────────────────
        // CREATE PAYMENT LINK
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// [POST] api/payments/create-payment-url
        /// Tạo link thanh toán PayOS cho một đơn hàng cụ thể.
        /// 
        /// Workflow:
        ///   1. Lấy UserId từ JWT Claims (401 nếu chưa đăng nhập).
        ///   2. Tìm đơn hàng theo OrderId (404 nếu không tìm thấy).
        ///   3. Kiểm tra quyền sở hữu: chỉ chủ đơn mới được thanh toán (403 nếu không phải).
        ///   4. Kiểm tra đơn chưa được thanh toán (400 nếu đã "Paid").
        ///   5. Kiểm tra đơn chưa bị hủy (400 nếu "Cancelled").
        ///   6. Tạo Payment record với Status = "Processing" và PaymentMethod = "PAYOS".
        ///   7. Cập nhật PaymentMethod trên Order.
        ///   8. Tính Amount = TotalAmount - DiscountAmount.
        ///   9. Gọi IPaymentService.CreatePaymentUrlAsync() → nhận về CheckoutUrl.
        ///   10. Trả về CheckoutUrl để frontend redirect người dùng.
        /// 
        /// Body: PaymentRequestDto { OrderId }
        /// Response: CheckoutUrl (string) để redirect.
        /// Phân quyền: [Authorize]
        /// </summary>
        [HttpPost("create-payment-url")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<string>>> CreatePaymentUrl([FromBody] PaymentRequestDto request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized(ApiResponse<string>.ErrorResponse("Unauthorized"));

            var order = await _unitOfWork.Orders.GetByIdAsync(request.OrderId);
            if (order == null)
                return NotFound(ApiResponse<string>.ErrorResponse("Order not found"));

            // Kiểm tra quyền sở hữu đơn hàng
            if (order.UserId != userId)
                return Forbid();

            if (order.PaymentStatus == "Paid")
                return BadRequest(ApiResponse<string>.ErrorResponse("Order has already been paid"));

            if (order.Status == "Cancelled")
                return BadRequest(ApiResponse<string>.ErrorResponse("Cannot pay for a cancelled order"));

            // Tạo Payment record với trạng thái Processing
            var payment = new Payment
            {
                OrderId       = order.Id,
                UserId        = userId,
                PaymentMethod = "PAYOS",
                Status        = "Processing",
                Amount        = order.TotalAmount - order.DiscountAmount
            };
            await _unitOfWork.Payments.AddAsync(payment);

            // Cập nhật phương thức thanh toán trên Order
            order.PaymentMethod = "PAYOS";
            _unitOfWork.Orders.Update(order);
            await _unitOfWork.CompleteAsync();

            // Thiết lập thông tin thanh toán từ dữ liệu đơn hàng thực tế
            request.Amount           = order.TotalAmount - order.DiscountAmount;
            request.OrderDescription = $"Don hang #{order.Id}";
            request.PaymentMethod    = "PAYOS";

            var checkoutUrl = await _paymentService.CreatePaymentUrlAsync(request);

            return Ok(ApiResponse<string>.SuccessResponse(checkoutUrl, "Payment link created successfully"));
        }

        // ─────────────────────────────────────────────────────────────────────
        // PAYOS RETURN URL (user redirect sau thanh toán)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// [GET] api/payments/payos-return
        /// PayOS Return URL — người dùng được redirect về đây sau khi hoàn tất thanh toán.
        /// 
        /// PayOS truyền các query params sau:
        ///   - orderCode: Mã đơn hàng PayOS (long)
        ///   - status: Trạng thái ("PAID", "CANCELLED", v.v.)
        ///   - code: Mã kết quả ("00" = thành công, các mã khác = thất bại)
        ///   - cancel: true nếu user hủy thanh toán
        /// 
        /// Workflow:
        ///   1. Xác định isSuccess: code == "00" AND status == "PAID" AND cancel != true.
        ///   2. Log thông tin redirect để debug.
        ///   3. Gọi IPaymentService.ProcessPaymentCallbackAsync(orderCode, isSuccess).
        ///   4. Decode orderId từ orderCode: orderId = orderCode / 1_000_000.
        ///   5. Tìm đơn hàng (404 nếu không tìm thấy).
        ///   6. Kiểm tra idempotency: nếu đã "Paid" → trả về luôn (tránh xử lý trùng).
        ///   7. Gọi UpdateOrderAndPaymentAsync() để cập nhật trạng thái Order và Payment.
        ///   8. Trả về kết quả thanh toán.
        /// 
        /// Lưu ý: Endpoint này có thể không đáng tin cậy (user có thể đóng trình duyệt trước khi redirect).
        /// → Dùng kết hợp với Webhook để đảm bảo xử lý đầy đủ.
        /// </summary>
        [HttpGet("payos-return")]
        public async Task<IActionResult> PayOSReturn(
            [FromQuery] long orderCode,
            [FromQuery] string? status,
            [FromQuery] string? code,
            [FromQuery] bool? cancel)
        {
            bool isSuccess = code == "00" && status == "PAID" && cancel != true;

            _logger.LogInformation(
                "PayOS Return: OrderCode={OrderCode}, Status={Status}, Code={Code}, Cancel={Cancel}",
                orderCode, status, code, cancel);

            var response = await _paymentService.ProcessPaymentCallbackAsync(orderCode, isSuccess);

            if (response.OrderCode <= 0)
                return BadRequest(ApiResponse<PaymentResponseDto>.ErrorResponse("Invalid order reference"));

            // Decode orderId từ orderCode (công thức: orderId = orderCode / 1_000_000)
            var orderId = (int)(orderCode / 1_000_000L);
            var order   = await _unitOfWork.Orders.GetByIdAsync(orderId);
            if (order == null)
            {
                _logger.LogWarning("PayOS Return: Order #{OrderId} not found", orderId);
                return NotFound(ApiResponse<PaymentResponseDto>.ErrorResponse("Order not found"));
            }

            // Idempotency: tránh xử lý trùng lặp nếu đã thanh toán
            if (order.PaymentStatus == "Paid")
                return Ok(ApiResponse<PaymentResponseDto>.SuccessResponse(response, "Payment already processed"));

            await UpdateOrderAndPaymentAsync(order, response, isSuccess);

            return Ok(ApiResponse<PaymentResponseDto>.SuccessResponse(
                response,
                response.Success ? "Payment successful" : "Payment failed or cancelled"));
        }

        /// <summary>
        /// [GET] api/payments/payos-cancel
        /// PayOS Cancel URL — người dùng được redirect về đây khi chủ động hủy thanh toán.
        /// 
        /// Workflow:
        ///   1. Log thông tin hủy thanh toán.
        ///   2. Decode orderId từ orderCode.
        ///   3. Tìm đơn hàng tương ứng.
        ///   4. Nếu đơn tồn tại và chưa thanh toán (PaymentStatus != "Paid"):
        ///      - Tìm Payment record đang "Processing".
        ///      - Cập nhật trạng thái Payment thành "Cancelled" với ResponseCode = "01".
        ///   5. Trả về 200 OK với thông báo đã hủy.
        /// 
        /// Lưu ý: Đơn hàng vẫn còn, chỉ có Payment bị hủy.
        ///        User có thể thử thanh toán lại sau.
        /// </summary>
        [HttpGet("payos-cancel")]
        public async Task<IActionResult> PayOSCancel([FromQuery] long orderCode)
        {
            _logger.LogInformation("PayOS Cancel: OrderCode={OrderCode}", orderCode);

            var orderId = (int)(orderCode / 1_000_000L);
            var order   = await _unitOfWork.Orders.GetByIdAsync(orderId);

            // Cập nhật Payment record sang Cancelled nếu đơn chưa thanh toán
            if (order != null && order.PaymentStatus != "Paid")
            {
                var payment = await _unitOfWork.Payments.FirstOrDefaultAsync(
                    p => p.OrderId == orderId && p.Status == "Processing");
                if (payment != null)
                {
                    payment.Status          = "Cancelled";
                    payment.ResponseCode    = "01";
                    payment.ResponseMessage = "User cancelled payment";
                    _unitOfWork.Payments.Update(payment);
                    await _unitOfWork.CompleteAsync();
                }
            }

            return Ok(ApiResponse<string>.SuccessResponse("cancelled", "Payment cancelled by user"));
        }

        // ─────────────────────────────────────────────────────────────────────
        // PAYOS WEBHOOK (server-to-server)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// [POST] api/payments/payos-webhook
        /// PayOS Webhook — PayOS server tự động gửi POST request sau khi thanh toán hoàn tất.
        /// Đây là cơ chế đáng tin cậy nhất để xác nhận thanh toán (không phụ thuộc browser của user).
        /// 
        /// Yêu cầu setup: Đăng ký URL này tại PayOS Dashboard trước khi sử dụng.
        /// Không có [Authorize] vì request đến từ server PayOS, không phải từ browser.
        /// 
        /// Workflow:
        ///   1. Gọi IPaymentService.ProcessWebhookAsync(webhookData) để:
        ///      - Xác thực chữ ký HMAC của PayOS (phòng chống giả mạo).
        ///      - Parse dữ liệu webhook.
        ///   2. Nếu chữ ký không hợp lệ (ResponseCode = "97") → trả về { code: "97" }.
        ///   3. Decode orderId từ response.OrderCode.
        ///   4. Tìm đơn hàng (trả về { code: "01" } nếu không tìm thấy).
        ///   5. Idempotency: nếu đơn đã "Paid" → trả về { code: "00" } ngay.
        ///   6. Kiểm tra số tiền: so sánh response.Amount với TotalAmount - DiscountAmount.
        ///      → Phòng chống tấn công giả mạo webhook với số tiền sai.
        ///   7. Gọi UpdateOrderAndPaymentAsync() để cập nhật trạng thái.
        ///   8. Trả về { code: "00", desc: "Confirm success" } cho PayOS server.
        /// 
        /// Mã response theo chuẩn PayOS:
        ///   "00" = Thành công
        ///   "01" = Không tìm thấy đơn hàng
        ///   "04" = Số tiền không khớp
        ///   "97" = Chữ ký không hợp lệ
        ///   "99" = Lỗi nội bộ
        /// Body: PayOSWebhookDto (cấu trúc do PayOS quy định).
        /// </summary>
        [HttpPost("payos-webhook")]
        public async Task<IActionResult> PayOSWebhook([FromBody] PayOSWebhookDto webhookData)
        {
            try
            {
                var response = await _paymentService.ProcessWebhookAsync(webhookData);

                // Chữ ký không hợp lệ → từ chối xử lý
                if (response.ResponseCode == "97")
                {
                    _logger.LogWarning("PayOS webhook: Invalid signature");
                    return Ok(new { code = "97", desc = "Invalid signature" });
                }

                if (response.OrderCode <= 0)
                    return Ok(new { code = "01", desc = "Invalid order code" });

                var orderId = (int)(response.OrderCode / 1_000_000L);
                var order   = await _unitOfWork.Orders.GetByIdAsync(orderId);
                if (order == null)
                {
                    _logger.LogWarning("PayOS webhook: Order #{OrderId} not found", orderId);
                    return Ok(new { code = "01", desc = "Order not found" });
                }

                // Idempotency: đơn đã thanh toán → bỏ qua (trả về success cho PayOS)
                if (order.PaymentStatus == "Paid")
                    return Ok(new { code = "00", desc = "Already confirmed" });

                // Kiểm tra số tiền thanh toán khớp với đơn hàng (phòng chống gian lận)
                var expectedAmount = (int)Math.Round(order.TotalAmount - order.DiscountAmount);
                if (response.Amount > 0 && (int)response.Amount != expectedAmount)
                {
                    _logger.LogWarning(
                        "PayOS webhook amount mismatch: Expected={Expected}, Got={Got}",
                        expectedAmount, response.Amount);
                    return Ok(new { code = "04", desc = "Invalid amount" });
                }

                await UpdateOrderAndPaymentAsync(order, response, response.Success);

                return Ok(new { code = "00", desc = "Confirm success" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in PayOS webhook");
                return Ok(new { code = "99", desc = "Internal error" });
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // QUERY PAYMENT STATUS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// [GET] api/payments/status/{orderCode}
        /// Truy vấn trạng thái thanh toán trực tiếp từ PayOS theo orderCode.
        /// 
        /// Workflow:
        ///   1. Gọi IPaymentService.QueryPaymentStatusAsync(orderCode).
        ///   2. Service gọi PayOS API để lấy trạng thái thực tế.
        ///   3. Trả về PaymentResponseDto với thông tin trạng thái và số tiền.
        /// 
        /// Route param: {orderCode} — orderCode PayOS (long).
        /// Dùng để polling trạng thái thanh toán từ frontend.
        /// Phân quyền: [Authorize]
        /// </summary>
        [HttpGet("status/{orderCode}")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<PaymentResponseDto>>> QueryStatus(long orderCode)
        {
            var response = await _paymentService.QueryPaymentStatusAsync(orderCode);
            return Ok(ApiResponse<PaymentResponseDto>.SuccessResponse(response));
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET PAYMENT HISTORY
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// [GET] api/payments/order/{orderId}
        /// Lấy lịch sử thanh toán của một đơn hàng cụ thể.
        /// 
        /// Workflow:
        ///   1. Kiểm tra đơn hàng tồn tại (404 nếu không).
        ///   2. Kiểm tra quyền: Admin xem được tất cả; User chỉ xem đơn của mình.
        ///   3. Lấy danh sách Payment theo orderId, sắp xếp giảm dần theo thời gian tạo.
        ///   4. Trả về danh sách PaymentDto.
        /// 
        /// Route param: {orderId} — ID đơn hàng.
        /// Phân quyền: [Authorize] + kiểm tra sở hữu.
        /// Một đơn hàng có thể có nhiều Payment record (ví dụ: thử thanh toán nhiều lần).
        /// </summary>
        [HttpGet("order/{orderId}")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<List<PaymentDto>>>> GetByOrder(int orderId)
        {
            var userId  = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            if (order == null)
                return NotFound(ApiResponse<List<PaymentDto>>.ErrorResponse("Order not found"));

            // Kiểm tra quyền truy cập (IDOR protection)
            if (!isAdmin && order.UserId != userId)
                return Forbid();

            var payments = await _unitOfWork.Payments.Query()
                .Where(p => p.OrderId == orderId)
                .OrderByDescending(p => p.CreatedAt) // Mới nhất lên đầu
                .Select(p => new PaymentDto
                {
                    Id            = p.Id,
                    OrderId       = p.OrderId,
                    PaymentMethod = p.PaymentMethod,
                    Status        = p.Status,
                    Amount        = p.Amount,
                    TransactionId = p.TransactionId,
                    BankCode      = p.BankCode,
                    CardType      = p.CardType,
                    PaidAt        = p.PaidAt,
                    CreatedAt     = p.CreatedAt
                })
                .ToListAsync();

            return Ok(ApiResponse<List<PaymentDto>>.SuccessResponse(payments));
        }

        /// <summary>
        /// [GET] api/payments?status={status}&method={method}&page={n}&pageSize={n}
        /// Admin: Lấy danh sách tất cả payments với phân trang và lọc theo trạng thái/phương thức.
        /// 
        /// Workflow:
        ///   1. Chỉ Admin mới có quyền truy cập.
        ///   2. Áp dụng filter theo Status (nếu có): Processing, Completed, Failed, Cancelled.
        ///   3. Áp dụng filter theo PaymentMethod (nếu có): PAYOS, COD, v.v.
        ///   4. Đếm tổng số records (TotalCount).
        ///   5. Áp dụng phân trang: Skip((page-1) × pageSize).Take(pageSize).
        ///   6. Sắp xếp giảm dần theo CreatedAt.
        ///   7. Trả về PagedResultDto&lt;PaymentDto&gt; gồm items + metadata phân trang.
        /// 
        /// Query params:
        ///   - status: Lọc theo trạng thái payment (optional)
        ///   - method: Lọc theo phương thức thanh toán (optional)
        ///   - page: Trang hiện tại (mặc định 1)
        ///   - pageSize: Số lượng mỗi trang (mặc định 20)
        /// Phân quyền: [Authorize(Roles = "Admin")]
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<PagedResultDto<PaymentDto>>>> GetAll(
            [FromQuery] string? status,
            [FromQuery] string? method,
            [FromQuery] int page     = 1,
            [FromQuery] int pageSize = 20)
        {
            var query = _unitOfWork.Payments.Query().AsQueryable();

            // Áp dụng filter theo Status nếu có
            if (!string.IsNullOrEmpty(status))
                query = query.Where(p => p.Status == status);

            // Áp dụng filter theo PaymentMethod nếu có
            if (!string.IsNullOrEmpty(method))
                query = query.Where(p => p.PaymentMethod == method);

            var totalCount = await query.CountAsync();
            var payments   = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)  // Phân trang
                .Take(pageSize)
                .Select(p => new PaymentDto
                {
                    Id            = p.Id,
                    OrderId       = p.OrderId,
                    PaymentMethod = p.PaymentMethod,
                    Status        = p.Status,
                    Amount        = p.Amount,
                    TransactionId = p.TransactionId,
                    BankCode      = p.BankCode,
                    CardType      = p.CardType,
                    PaidAt        = p.PaidAt,
                    CreatedAt     = p.CreatedAt
                })
                .ToListAsync();

            var result = new PagedResultDto<PaymentDto>
            {
                Items      = payments,
                TotalCount = totalCount,
                Page       = page,
                PageSize   = pageSize
            };

            return Ok(ApiResponse<PagedResultDto<PaymentDto>>.SuccessResponse(result));
        }

        // ─────────────────────────────────────────────────────────────────────
        // PRIVATE HELPERS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Helper dùng chung: Cập nhật trạng thái Order và Payment sau khi xử lý PayOS callback/webhook.
        /// 
        /// Được gọi bởi cả PayOSReturn và PayOSWebhook để tránh duplicate code.
        /// 
        /// Nếu thanh toán THÀNH CÔNG (isSuccess = true):
        ///   - Order.PaymentStatus = "Paid"
        ///   - Order.Status: Nếu đang "Pending" → chuyển sang "Shipping"
        ///   - Payment.Status = "Completed", lưu TransactionId, PaidAt, ResponseCode, ResponseMessage.
        ///   - Enqueue background job gửi email xác nhận thanh toán cho user.
        ///   - Log thông tin thành công.
        /// 
        /// Nếu thanh toán THẤT BẠI (isSuccess = false):
        ///   - Payment.Status = "Failed", lưu ResponseCode, ResponseMessage.
        ///   - Order không thay đổi trạng thái (user có thể thử lại).
        ///   - Log cảnh báo thất bại.
        /// 
        /// Trong cả hai trường hợp: Tìm Payment đang "Processing" thuộc đơn hàng để cập nhật.
        /// </summary>
        /// <param name="order">Entity Order cần cập nhật.</param>
        /// <param name="response">Dữ liệu kết quả thanh toán từ PayOS.</param>
        /// <param name="isSuccess">true nếu thanh toán thành công.</param>
        private async Task UpdateOrderAndPaymentAsync(
            Order order,
            PaymentResponseDto response,
            bool isSuccess)
        {
            if (isSuccess)
            {
                // Cập nhật trạng thái thanh toán đơn hàng
                order.PaymentStatus = "Paid";
                // Chỉ chuyển sang Shipping nếu đơn đang ở Pending
                order.Status        = order.Status == "Pending" ? "Shipping" : order.Status;

                // Cập nhật Payment record đang Processing
                var payment = await _unitOfWork.Payments.FirstOrDefaultAsync(
                    p => p.OrderId == order.Id && p.Status == "Processing");

                if (payment != null)
                {
                    payment.Status          = "Completed";
                    payment.TransactionId   = response.TransactionId ?? response.Reference; // Lấy ID giao dịch
                    payment.PaidAt          = response.PayDate ?? DateTime.UtcNow;
                    payment.ResponseCode    = response.ResponseCode;
                    payment.ResponseMessage = response.Message;
                    _unitOfWork.Payments.Update(payment);
                }

                _unitOfWork.Orders.Update(order);
                await _unitOfWork.CompleteAsync();

                // Gửi email xác nhận thanh toán thành công bất đồng bộ
                _backgroundJobService.Enqueue(() =>
                    _emailService.SendOrderConfirmationAsync(order.Id, order.UserId));

                _logger.LogInformation(
                    "Order #{OrderId} marked as Paid via PayOS (OrderCode={OrderCode})",
                    order.Id, response.OrderCode);
            }
            else
            {
                // Đánh dấu Payment thất bại
                var payment = await _unitOfWork.Payments.FirstOrDefaultAsync(
                    p => p.OrderId == order.Id && p.Status == "Processing");

                if (payment != null)
                {
                    payment.Status          = "Failed";
                    payment.ResponseCode    = response.ResponseCode;
                    payment.ResponseMessage = response.Message;
                    _unitOfWork.Payments.Update(payment);
                    await _unitOfWork.CompleteAsync();
                }

                _logger.LogWarning(
                    "Payment failed for Order #{OrderId}: {Message}", order.Id, response.Message);
            }
        }
    }
}
