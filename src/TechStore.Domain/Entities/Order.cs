using TechStore.Domain.Common;

namespace TechStore.Domain.Entities
{
    public class Order : BaseEntity
    {
        public string UserId { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
        public decimal TotalAmount { get; set; }
        public decimal DiscountAmount { get; set; }

        /// <summary>
        /// Pending, Shipping, Delivered, Cancelled
        /// </summary>
        public string Status { get; set; } = "Pending";

        /// <summary>
        /// Unpaid, Paid, Refunded
        /// </summary>
        public string PaymentStatus { get; set; } = "Unpaid";

        /// <summary>
        /// COD, MOMO, STRIPE
        /// </summary>
        public string PaymentMethod { get; set; } = "COD";

        public string? ShippingAddress { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Note { get; set; }
        public string? CouponCode { get; set; }

        public List<OrderItem> Items { get; set; } = new();
        public List<Payment> Payments { get; set; } = new();

        /// <summary>
        /// Concurrency token for optimistic locking (stock safety)
        /// </summary>
        public byte[] RowVersion { get; set; } = null!;
    }
}
