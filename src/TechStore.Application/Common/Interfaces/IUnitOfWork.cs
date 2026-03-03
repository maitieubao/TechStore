using TechStore.Domain.Entities;

namespace TechStore.Application.Common.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        IGenericRepository<Product> Products { get; }
        IGenericRepository<Category> Categories { get; }
        IGenericRepository<Order> Orders { get; }
        IGenericRepository<OrderItem> OrderItems { get; }
        IGenericRepository<CartItem> CartItems { get; }
        IGenericRepository<Review> Reviews { get; }
        IGenericRepository<Coupon> Coupons { get; }
        IGenericRepository<ProductImage> ProductImages { get; }
        IGenericRepository<Wishlist> Wishlists { get; }
        IGenericRepository<Payment> Payments { get; }

        Task<int> CompleteAsync();
    }
}
