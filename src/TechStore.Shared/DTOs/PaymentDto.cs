namespace TechStore.Shared.DTOs
{
    public class PaymentRequestDto
    {
        public int OrderId { get; set; }
        public decimal Amount { get; set; }
        public string OrderDescription { get; set; } = string.Empty;

        /// <summary>
        /// PAYOS, COD, etc.
        /// </summary>
        public string PaymentMethod { get; set; } = "PAYOS";
    }

    public class PaymentResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int OrderId { get; set; }
        public long OrderCode { get; set; }
        public decimal Amount { get; set; }
        public string? TransactionId { get; set; }
        public string? PaymentLinkId { get; set; }
        public string? AccountNumber { get; set; }
        public string? Reference { get; set; }
        public DateTime? PayDate { get; set; }
        public string? ResponseCode { get; set; }
        public string? CheckoutUrl { get; set; }
        public string? BankCode { get; set; }
        public string? BankTranNo { get; set; }
        public string? CardType { get; set; }
    }

    /// <summary>
    /// Webhook body gửi từ PayOS (server-to-server)
    /// ánh xạ đúng với PayOS.Models.Webhook
    /// </summary>
    public class PayOSWebhookDto
    {
        public string Code { get; set; } = string.Empty;
        public string Desc { get; set; } = string.Empty;
        public bool Success { get; set; }
        public WebhookDataDto? Data { get; set; }
        public string Signature { get; set; } = string.Empty;
    }

    public class WebhookDataDto
    {
        public long OrderCode { get; set; }
        public int Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public string TransactionDateTime { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public string PaymentLinkId { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Desc { get; set; } = string.Empty;
        public string CounterAccountBankId { get; set; } = string.Empty;
        public string CounterAccountBankName { get; set; } = string.Empty;
        public string CounterAccountName { get; set; } = string.Empty;
        public string CounterAccountNumber { get; set; } = string.Empty;
        public string VirtualAccountName { get; set; } = string.Empty;
        public string VirtualAccountNumber { get; set; } = string.Empty;
    }

    public class PaymentDto
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? TransactionId { get; set; }
        public string? PaymentLinkId { get; set; }
        public string? AccountNumber { get; set; }
        public string? BankCode { get; set; }
        public string? CardType { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
