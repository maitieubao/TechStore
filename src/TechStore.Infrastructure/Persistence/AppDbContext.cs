using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TechStore.Domain.Entities;

namespace TechStore.Infrastructure.Persistence
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<Coupon> Coupons { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<Wishlist> Wishlists { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<Payment> Payments { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Product
            builder.Entity<Product>(entity =>
            {
                entity.Property(p => p.Price).HasColumnType("decimal(18,2)");
                entity.HasIndex(p => p.Slug).IsUnique();
                entity.HasIndex(p => p.Brand);
                entity.HasIndex(p => p.Price);

                // Optimistic Concurrency for stock management
                entity.Property(p => p.RowVersion).IsRowVersion();

                entity.HasOne(p => p.Category)
                      .WithMany(c => c.Products)
                      .HasForeignKey(p => p.CategoryId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // Category
            builder.Entity<Category>(entity =>
            {
                entity.HasIndex(c => c.Slug).IsUnique();
            });

            // Order
            builder.Entity<Order>(entity =>
            {
                entity.Property(o => o.TotalAmount).HasColumnType("decimal(18,2)");
                entity.Property(o => o.DiscountAmount).HasColumnType("decimal(18,2)");
                entity.HasIndex(o => o.UserId);
                entity.HasIndex(o => o.Status);
                entity.HasIndex(o => o.OrderDate);
                entity.HasIndex(o => o.PaymentStatus);

                // Optimistic Concurrency
                entity.Property(o => o.RowVersion).IsRowVersion();
            });

            // OrderItem
            builder.Entity<OrderItem>(entity =>
            {
                entity.Property(oi => oi.UnitPrice).HasColumnType("decimal(18,2)");
                entity.HasOne(oi => oi.Product)
                      .WithMany()
                      .HasForeignKey(oi => oi.ProductId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // CartItem
            builder.Entity<CartItem>(entity =>
            {
                entity.HasOne(ci => ci.Product)
                      .WithMany()
                      .HasForeignKey(ci => ci.ProductId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(ci => new { ci.UserId, ci.ProductId }).IsUnique();
            });

            // Review
            builder.Entity<Review>(entity =>
            {
                entity.HasOne(r => r.Product)
                      .WithMany(p => p.Reviews)
                      .HasForeignKey(r => r.ProductId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(r => r.User)
                      .WithMany()
                      .HasForeignKey(r => r.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(r => new { r.UserId, r.ProductId }).IsUnique();
            });

            // Coupon
            builder.Entity<Coupon>(entity =>
            {
                entity.Property(c => c.DiscountPercent).HasColumnType("decimal(5,2)");
                entity.Property(c => c.MaxDiscountAmount).HasColumnType("decimal(18,2)");
                entity.Property(c => c.MinOrderAmount).HasColumnType("decimal(18,2)");
                entity.HasIndex(c => c.Code).IsUnique();
            });

            // ProductImage
            builder.Entity<ProductImage>(entity =>
            {
                entity.HasOne(pi => pi.Product)
                      .WithMany(p => p.Images)
                      .HasForeignKey(pi => pi.ProductId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Wishlist
            builder.Entity<Wishlist>(entity =>
            {
                entity.HasOne(w => w.Product)
                      .WithMany()
                      .HasForeignKey(w => w.ProductId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(w => new { w.UserId, w.ProductId }).IsUnique();
            });

            // RefreshToken
            builder.Entity<RefreshToken>(entity =>
            {
                entity.HasIndex(rt => rt.Token).IsUnique();
                entity.HasIndex(rt => rt.UserId);
            });

            // Payment
            builder.Entity<Payment>(entity =>
            {
                entity.Property(p => p.Amount).HasColumnType("decimal(18,2)");
                entity.HasIndex(p => p.OrderId);
                entity.HasIndex(p => p.UserId);
                entity.HasIndex(p => p.TransactionId);
                entity.HasIndex(p => p.Status);

                // Optimistic Concurrency
                entity.Property(p => p.RowVersion).IsRowVersion();

                entity.HasOne(p => p.Order)
                      .WithMany(o => o.Payments)
                      .HasForeignKey(p => p.OrderId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
