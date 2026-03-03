using TechStore.Domain.Common;

namespace TechStore.Domain.Entities
{
    public class Payment : BaseEntity
    {
        public int OrderId { get; set; }
        public Order Order { get; set; } = null!;
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// COD, MOMO, STRIPE
        /// </summary>
        public string PaymentMethod { get; set; } = "COD";

        /// <summary>
        /// Pending, Processing, Completed, Failed, Refunded, Cancelled
        /// </summary>
        public string Status { get; set; } = "Pending";

        public decimal Amount { get; set; }
        public string? TransactionId { get; set; }    // Mã giao dịch từ cổng thanh toán
        public string? BankCode { get; set; }          // Ngân hàng thanh toán
        public string? BankTranNo { get; set; }        // Mã giao dịch ngân hàng
        public string? CardType { get; set; }          // ATM, VISA, MASTERCARD, JCB, QR
        public string? PaymentInfo { get; set; }       // Thông tin bổ sung
        public DateTime? PaidAt { get; set; }          // Thời điểm thanh toán
        public string? ResponseCode { get; set; }      // Mã kết quả trả về
        public string? ResponseMessage { get; set; }   // Mô tả kết quả

        /// <summary>
        /// Concurrency token for optimistic locking
        /// </summary>
        public byte[] RowVersion { get; set; } = null!;
    }
}
