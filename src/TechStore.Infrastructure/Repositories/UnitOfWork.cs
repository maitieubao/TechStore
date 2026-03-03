using Microsoft.EntityFrameworkCore;
using TechStore.Application.Common.Exceptions;
using TechStore.Application.Common.Interfaces;
using TechStore.Domain.Entities;
using TechStore.Infrastructure.Persistence;

namespace TechStore.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;

        public IGenericRepository<Product> Products { get; private set; }
        public IGenericRepository<Category> Categories { get; private set; }
        public IGenericRepository<Order> Orders { get; private set; }
        public IGenericRepository<OrderItem> OrderItems { get; private set; }
        public IGenericRepository<CartItem> CartItems { get; private set; }
        public IGenericRepository<Review> Reviews { get; private set; }
        public IGenericRepository<Coupon> Coupons { get; private set; }
        public IGenericRepository<ProductImage> ProductImages { get; private set; }
        public IGenericRepository<Wishlist> Wishlists { get; private set; }
        public IGenericRepository<Payment> Payments { get; private set; }

        public UnitOfWork(AppDbContext context)
        {
            _context = context;
            Products = new GenericRepository<Product>(_context);
            Categories = new GenericRepository<Category>(_context);
            Orders = new GenericRepository<Order>(_context);
            OrderItems = new GenericRepository<OrderItem>(_context);
            CartItems = new GenericRepository<CartItem>(_context);
            Reviews = new GenericRepository<Review>(_context);
            Coupons = new GenericRepository<Coupon>(_context);
            ProductImages = new GenericRepository<ProductImage>(_context);
            Wishlists = new GenericRepository<Wishlist>(_context);
            Payments = new GenericRepository<Payment>(_context);
        }

        public async Task<int> CompleteAsync()
        {
            try
            {
                return await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // Map EF Core concurrency exception to application exception
                throw new ConcurrencyException("Data was modified by another user. Please reload and try again.", ex);
            }
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
