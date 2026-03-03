namespace TechStore.Shared.DTOs
{
    public class ProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public bool IsLowStock { get; set; }
        public string? Brand { get; set; }
        public string? Specifications { get; set; }
        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? CategorySlug { get; set; }
        public double AverageRating { get; set; }
        public int ReviewCount { get; set; }
        public List<string> ImageUrls { get; set; } = new();
        public bool IsActive { get; set; }
        public bool IsCombo { get; set; }
        public decimal? OriginalPrice { get; set; }
    }
}
