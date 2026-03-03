using TechStore.Domain.Common;

namespace TechStore.Domain.Entities
{
    public class Coupon : BaseEntity
    {
        public string Code { get; set; } = string.Empty;
        public decimal DiscountPercent { get; set; }  // e.g., 10 = 10%
        public decimal? MaxDiscountAmount { get; set; } // cap
        public decimal MinOrderAmount { get; set; }    // minimum order to apply
        public DateTime ExpiryDate { get; set; }
        public int UsageLimit { get; set; }            // max times coupon can be used
        public int TimesUsed { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
