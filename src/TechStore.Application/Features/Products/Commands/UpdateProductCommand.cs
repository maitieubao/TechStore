using MediatR;

namespace TechStore.Application.Features.Products.Commands
{
    public class UpdateProductCommand : IRequest<bool>
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public string? Brand { get; set; }
        public string? Specifications { get; set; }
        public int? CategoryId { get; set; }
        public bool IsCombo { get; set; } = false;
        public decimal? OriginalPrice { get; set; }
    }
}
