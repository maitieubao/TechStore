using System.ComponentModel.DataAnnotations;

namespace TechStore.Web.Models.ViewModels
{
    public class CreateProductVM
    {
        [Required(ErrorMessage = "Tên sản phẩm không được để trống")]
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Giá không được để trống")]
        [Range(0, double.MaxValue, ErrorMessage = "Giá phải lớn hơn hoặc bằng 0")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "Số lượng tồn kho không được để trống")]
        [Range(0, int.MaxValue, ErrorMessage = "Số lượng tồn kho phải lớn hơn hoặc bằng 0")]
        public int StockQuantity { get; set; }

        public string? Brand { get; set; }

        public string? Specifications { get; set; }

        [Required(ErrorMessage = "Danh mục không được để trống")]
        public int? CategoryId { get; set; }

        public bool IsCombo { get; set; } = false;

        [Range(0, double.MaxValue, ErrorMessage = "Giá gốc phải lớn hơn 0")]
        public decimal? OriginalPrice { get; set; }
    }
}
