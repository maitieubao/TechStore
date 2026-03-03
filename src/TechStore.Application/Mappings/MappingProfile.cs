using AutoMapper;
using TechStore.Application.Features.Products.Commands;
using TechStore.Domain.Entities;
using TechStore.Shared.DTOs;

namespace TechStore.Application.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // Product mappings
            CreateMap<Product, ProductDto>()
                .ForMember(d => d.CategoryName, opt => opt.MapFrom(s => s.Category != null ? s.Category.Name : null))
                .ForMember(d => d.CategorySlug, opt => opt.MapFrom(s => s.Category != null ? s.Category.Slug : null))
                .ForMember(d => d.IsLowStock, opt => opt.MapFrom(s => s.StockQuantity > 0 && s.StockQuantity <= s.LowStockThreshold))
                .ForMember(d => d.AverageRating, opt => opt.MapFrom(s => s.Reviews.Any() ? s.Reviews.Average(r => (double)r.Rating) : 0))
                .ForMember(d => d.ReviewCount, opt => opt.MapFrom(s => s.Reviews.Count))
                .ForMember(d => d.ImageUrls, opt => opt.MapFrom(s => s.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).ToList()));

            CreateMap<CreateProductCommand, Product>();
            CreateMap<UpdateProductCommand, Product>();

            // Category
            CreateMap<Category, CategoryDto>()
                .ForMember(d => d.ProductCount, opt => opt.MapFrom(s => s.Products != null ? s.Products.Count : 0));
            CreateMap<CreateCategoryDto, Category>();

            // Order
            CreateMap<Order, OrderDto>();
            CreateMap<OrderItem, OrderItemDto>()
                .ForMember(d => d.ProductName, opt => opt.MapFrom(s => s.Product != null ? s.Product.Name : ""));

            // Review
            CreateMap<Review, ReviewDto>()
                .ForMember(d => d.UserName, opt => opt.MapFrom(s => s.User != null ? s.User.UserName : ""));

            // Coupon
            CreateMap<Coupon, CouponDto>();
            CreateMap<CreateCouponDto, Coupon>();

            // Cart
            CreateMap<CartItem, CartItemDto>()
                .ForMember(d => d.ProductName, opt => opt.MapFrom(s => s.Product.Name))
                .ForMember(d => d.ProductPrice, opt => opt.MapFrom(s => s.Product.Price));
        }
    }
}
