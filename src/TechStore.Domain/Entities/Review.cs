using TechStore.Domain.Common;

namespace TechStore.Domain.Entities
{
    public class Review : BaseEntity
    {
        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;
        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;
        public int Rating { get; set; } // 1-5
        public string Comment { get; set; } = string.Empty;
    }
}
