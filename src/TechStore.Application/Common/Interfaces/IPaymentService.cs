using TechStore.Shared.DTOs;

namespace TechStore.Application.Common.Interfaces
{
    public interface IPaymentService
    {
        /// <summary>
        /// Tạo link thanh toán PayOS và trả về checkout URL
        /// </summary>
        Task<string> CreatePaymentUrlAsync(PaymentRequestDto request);

        /// <summary>
        /// Xác thực và xử lý webhook từ PayOS (server-to-server)
        /// </summary>
        Task<PaymentResponseDto> ProcessWebhookAsync(PayOSWebhookDto webhookData);

        /// <summary>
        /// Xử lý redirect sau thanh toán (return URL / cancel URL)
        /// </summary>
        Task<PaymentResponseDto> ProcessPaymentCallbackAsync(long orderCode, bool isSuccess);

        /// <summary>
        /// Huỷ link thanh toán PayOS theo orderCode
        /// </summary>
        Task<bool> CancelPaymentLinkAsync(long orderCode, string? reason = null);

        /// <summary>
        /// Truy vấn trạng thái thanh toán theo orderCode
        /// </summary>
        Task<PaymentResponseDto> QueryPaymentStatusAsync(long orderCode);
    }
}
