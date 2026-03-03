using TechStore.Shared.DTOs;

namespace TechStore.Web.Models.ViewModels
{
    public class HomeVM
    {
        public List<ProductDto> FeaturedProducts { get; set; } = new();
        public List<CategoryDto> Categories { get; set; } = new();
        public List<ProductDto> NewArrivals { get; set; } = new();
    }

    public class ProductListVM
    {
        public PagedResultDto<ProductDto> Products { get; set; } = new();
        public ProductFilterDto Filter { get; set; } = new();
        public List<CategoryDto> Categories { get; set; } = new();
        public List<string> Brands { get; set; } = new();
        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }
    }

    public class ProductDetailVM
    {
        public ProductDto Product { get; set; } = new();
        public List<ReviewDto> Reviews { get; set; } = new();
        public List<ProductDto> RelatedProducts { get; set; } = new();
    }

    public class CartVM
    {
        public CartSummaryDto Cart { get; set; } = new();
    }

    public class CheckoutVM
    {
        public CartSummaryDto Cart { get; set; } = new();
        public string ShippingAddress { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Note { get; set; }
        public string? CouponCode { get; set; }
        public string PaymentMethod { get; set; } = "COD";
    }

    public class OrderDetailVM
    {
        public OrderDto Order { get; set; } = new();
        public List<PaymentDto> Payments { get; set; } = new();
    }

    public class LoginVM
    {
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? ReturnUrl { get; set; }
    }

    public class RegisterVM
    {
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
    }

    // Admin
    public class AdminDashboardVM
    {
        public DashboardDto Dashboard { get; set; } = new();
    }

    public class AdminOrderListVM
    {
        public List<OrderDto> Orders { get; set; } = new();
    }

    public class AdminProductListVM
    {
        public PagedResultDto<ProductDto> Products { get; set; } = new();
        public int CurrentPage { get; set; } = 1;
        public ProductFilterDto Filter { get; set; } = new();
        public List<CategoryDto> Categories { get; set; } = new();
    }
}
