using TechStore.Domain.Common;

namespace TechStore.Domain.Entities
{
    public class CartItem : BaseEntity
    {
        public string UserId { get; set; } = string.Empty;
        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;
        public int Quantity { get; set; }
    }
}
