using TechStore.Domain.Common;

namespace TechStore.Domain.Entities
{
    public class Product : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public int LowStockThreshold { get; set; } = 5;
        public string? Brand { get; set; }
        public string? Specifications { get; set; }
        public int? CategoryId { get; set; }
        public Category? Category { get; set; }
        public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
        public bool IsActive { get; set; } = true;
        public bool IsCombo { get; set; } = false;
        public decimal? OriginalPrice { get; set; } // giá gốc trước khi giảm (dành cho combo)

        /// <summary>
        /// Concurrency token for optimistic locking (stock safety)
        /// </summary>
        public byte[] RowVersion { get; set; } = null!;
    }
}
