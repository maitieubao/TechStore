using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TechStore.Domain.Common;
using TechStore.Domain.Entities;
using TechStore.Infrastructure.Persistence;

namespace TechStore.Infrastructure.Identity
{
    public static class DataSeeder
    {
        public static async Task SeedDataAsync(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<AppDbContext>();

            // Skip if data already exists
            if (await context.Categories.AnyAsync())
                return;

            // Seed Categories
            var categories = new List<Category>
            {
                new() { Name = "Laptop", Slug = "laptop", Description = "Máy tính xách tay các loại", ImageUrl = "/images/categories/laptop.png" },
                new() { Name = "Điện thoại", Slug = "dien-thoai", Description = "Smartphone và phụ kiện điện thoại", ImageUrl = "/images/categories/phone.png" },
                new() { Name = "Tablet", Slug = "tablet", Description = "Máy tính bảng", ImageUrl = "/images/categories/tablet.png" },
                new() { Name = "Phụ kiện", Slug = "phu-kien", Description = "Chuột, bàn phím, tai nghe, sạc...", ImageUrl = "/images/categories/accessories.png" },
                new() { Name = "Màn hình", Slug = "man-hinh", Description = "Màn hình máy tính các loại", ImageUrl = "/images/categories/monitor.png" },
                new() { Name = "Linh kiện PC", Slug = "linh-kien-pc", Description = "CPU, RAM, SSD, VGA, Mainboard...", ImageUrl = "/images/categories/components.png" }
            };

            context.Categories.AddRange(categories);
            await context.SaveChangesAsync();

            // Seed Products
            var products = new List<Product>
            {
                // Laptops
                new()
                {
                    Name = "MacBook Pro 14\" M4 Pro",
                    Slug = "macbook-pro-14-m4-pro",
                    Description = "Laptop chuyên nghiệp với chip Apple M4 Pro, màn hình Liquid Retina XDR 14.2 inch",
                    Price = 49990000, StockQuantity = 15, LowStockThreshold = 3,
                    Brand = "Apple", CategoryId = categories[0].Id,
                    Specifications = "{\"CPU\":\"Apple M4 Pro\",\"RAM\":\"18GB\",\"SSD\":\"512GB\",\"Screen\":\"14.2 inch Liquid Retina XDR\",\"Battery\":\"17 hours\"}"
                },
                new()
                {
                    Name = "Dell XPS 15 9530",
                    Slug = "dell-xps-15-9530",
                    Description = "Laptop cao cấp Dell XPS 15 với màn hình OLED 3.5K, Intel Core i7",
                    Price = 42990000, StockQuantity = 10, LowStockThreshold = 3,
                    Brand = "Dell", CategoryId = categories[0].Id,
                    Specifications = "{\"CPU\":\"Intel Core i7-13700H\",\"RAM\":\"16GB DDR5\",\"SSD\":\"512GB\",\"Screen\":\"15.6 inch OLED 3.5K\",\"GPU\":\"RTX 4060\"}"
                },
                new()
                {
                    Name = "ASUS ROG Strix G16",
                    Slug = "asus-rog-strix-g16",
                    Description = "Laptop gaming ASUS ROG Strix G16 với RTX 4070, màn hình 165Hz",
                    Price = 38990000, StockQuantity = 20, LowStockThreshold = 5,
                    Brand = "ASUS", CategoryId = categories[0].Id,
                    Specifications = "{\"CPU\":\"Intel Core i9-13980HX\",\"RAM\":\"16GB DDR5\",\"SSD\":\"1TB\",\"Screen\":\"16 inch QHD+ 165Hz\",\"GPU\":\"RTX 4070\"}"
                },
                new()
                {
                    Name = "Lenovo ThinkPad X1 Carbon Gen 11",
                    Slug = "lenovo-thinkpad-x1-carbon-gen-11",
                    Description = "Laptop doanh nhân siêu nhẹ, bền bỉ theo chuẩn quân đội MIL-STD-810H",
                    Price = 35990000, StockQuantity = 12, LowStockThreshold = 3,
                    Brand = "Lenovo", CategoryId = categories[0].Id,
                    Specifications = "{\"CPU\":\"Intel Core i7-1365U\",\"RAM\":\"16GB\",\"SSD\":\"512GB\",\"Screen\":\"14 inch 2.8K OLED\",\"Weight\":\"1.12kg\"}"
                },

                // Phones
                new()
                {
                    Name = "iPhone 16 Pro Max 256GB",
                    Slug = "iphone-16-pro-max-256gb",
                    Description = "iPhone 16 Pro Max với chip A18 Pro, camera 48MP, titanium design",
                    Price = 34990000, StockQuantity = 50, LowStockThreshold = 10,
                    Brand = "Apple", CategoryId = categories[1].Id,
                    Specifications = "{\"Chip\":\"A18 Pro\",\"RAM\":\"8GB\",\"Storage\":\"256GB\",\"Screen\":\"6.9 inch Super Retina XDR\",\"Camera\":\"48MP + 12MP + 12MP\"}"
                },
                new()
                {
                    Name = "Samsung Galaxy S24 Ultra",
                    Slug = "samsung-galaxy-s24-ultra",
                    Description = "Samsung Galaxy S24 Ultra với S-Pen tích hợp, AI features, camera 200MP",
                    Price = 31990000, StockQuantity = 35, LowStockThreshold = 8,
                    Brand = "Samsung", CategoryId = categories[1].Id,
                    Specifications = "{\"Chip\":\"Snapdragon 8 Gen 3\",\"RAM\":\"12GB\",\"Storage\":\"256GB\",\"Screen\":\"6.8 inch Dynamic AMOLED 2X\",\"Camera\":\"200MP\"}"
                },
                new()
                {
                    Name = "Xiaomi 14 Ultra",
                    Slug = "xiaomi-14-ultra",
                    Description = "Xiaomi 14 Ultra với camera Leica, chip Snapdragon 8 Gen 3",
                    Price = 22990000, StockQuantity = 25, LowStockThreshold = 5,
                    Brand = "Xiaomi", CategoryId = categories[1].Id,
                    Specifications = "{\"Chip\":\"Snapdragon 8 Gen 3\",\"RAM\":\"16GB\",\"Storage\":\"512GB\",\"Screen\":\"6.73 inch AMOLED\",\"Camera\":\"50MP Leica\"}"
                },

                // Tablets
                new()
                {
                    Name = "iPad Pro M4 11 inch",
                    Slug = "ipad-pro-m4-11-inch",
                    Description = "iPad Pro mỏng nhất từ trước đến nay với chip M4 và màn hình tandem OLED",
                    Price = 27990000, StockQuantity = 30, LowStockThreshold = 5,
                    Brand = "Apple", CategoryId = categories[2].Id,
                    Specifications = "{\"Chip\":\"Apple M4\",\"RAM\":\"8GB\",\"Storage\":\"256GB\",\"Screen\":\"11 inch Tandem OLED\",\"Weight\":\"444g\"}"
                },
                new()
                {
                    Name = "Samsung Galaxy Tab S9 Ultra",
                    Slug = "samsung-galaxy-tab-s9-ultra",
                    Description = "Tablet Android cao cấp với màn hình 14.6 inch Dynamic AMOLED 2X",
                    Price = 24990000, StockQuantity = 15, LowStockThreshold = 3,
                    Brand = "Samsung", CategoryId = categories[2].Id,
                    Specifications = "{\"Chip\":\"Snapdragon 8 Gen 2\",\"RAM\":\"12GB\",\"Storage\":\"256GB\",\"Screen\":\"14.6 inch Dynamic AMOLED 2X\"}"
                },

                // Accessories
                new()
                {
                    Name = "Apple AirPods Pro 2",
                    Slug = "apple-airpods-pro-2",
                    Description = "Tai nghe true wireless với chống ồn chủ động, chip H2, USB-C",
                    Price = 6490000, StockQuantity = 100, LowStockThreshold = 15,
                    Brand = "Apple", CategoryId = categories[3].Id,
                    Specifications = "{\"Driver\":\"Custom Apple\",\"ANC\":\"Active\",\"Battery\":\"6 hours\",\"Charging\":\"USB-C, MagSafe, Qi\"}"
                },
                new()
                {
                    Name = "Logitech MX Master 3S",
                    Slug = "logitech-mx-master-3s",
                    Description = "Chuột không dây cao cấp cho dân chuyên nghiệp, sensor 8000 DPI",
                    Price = 2490000, StockQuantity = 60, LowStockThreshold = 10,
                    Brand = "Logitech", CategoryId = categories[3].Id,
                    Specifications = "{\"Sensor\":\"Darkfield 8000 DPI\",\"Battery\":\"70 days\",\"Connectivity\":\"Bluetooth + USB receiver\",\"Weight\":\"141g\"}"
                },
                new()
                {
                    Name = "Keychron K2 Pro Mechanical Keyboard",
                    Slug = "keychron-k2-pro-mechanical-keyboard",
                    Description = "Bàn phím cơ không dây 75% với hot-swap và QMK/VIA firmware",
                    Price = 2290000, StockQuantity = 40, LowStockThreshold = 8,
                    Brand = "Keychron", CategoryId = categories[3].Id,
                    Specifications = "{\"Switch\":\"Gateron G Pro\",\"Layout\":\"75%\",\"Connectivity\":\"Bluetooth + USB-C\",\"Battery\":\"4000mAh\"}"
                },

                // Monitors
                new()
                {
                    Name = "LG UltraGear 27GP850-B",
                    Slug = "lg-ultragear-27gp850-b",
                    Description = "Màn hình gaming 27 inch QHD Nano IPS, 165Hz, 1ms, HDR400",
                    Price = 11990000, StockQuantity = 20, LowStockThreshold = 5,
                    Brand = "LG", CategoryId = categories[4].Id,
                    Specifications = "{\"Size\":\"27 inch\",\"Resolution\":\"QHD 2560x1440\",\"Panel\":\"Nano IPS\",\"Refresh\":\"165Hz\",\"Response\":\"1ms\"}"
                },
                new()
                {
                    Name = "Dell UltraSharp U2723QE",
                    Slug = "dell-ultrasharp-u2723qe",
                    Description = "Màn hình chuyên nghiệp 4K 27 inch IPS Black, 100% sRGB, USB-C Hub",
                    Price = 14990000, StockQuantity = 15, LowStockThreshold = 3,
                    Brand = "Dell", CategoryId = categories[4].Id,
                    Specifications = "{\"Size\":\"27 inch\",\"Resolution\":\"4K 3840x2160\",\"Panel\":\"IPS Black\",\"Color\":\"100% sRGB, 98% DCI-P3\",\"USB-C\":\"90W PD\"}"
                },

                // PC Components
                new()
                {
                    Name = "NVIDIA GeForce RTX 4070 Ti Super",
                    Slug = "nvidia-geforce-rtx-4070-ti-super",
                    Description = "Card đồ họa gaming cao cấp với 16GB GDDR6X, DLSS 3.5, Ray Tracing",
                    Price = 21990000, StockQuantity = 10, LowStockThreshold = 3,
                    Brand = "NVIDIA", CategoryId = categories[5].Id,
                    Specifications = "{\"VRAM\":\"16GB GDDR6X\",\"Cores\":\"8448 CUDA\",\"Boost Clock\":\"2610 MHz\",\"TDP\":\"285W\"}"
                },
                new()
                {
                    Name = "AMD Ryzen 9 7950X",
                    Slug = "amd-ryzen-9-7950x",
                    Description = "CPU Desktop 16 nhân 32 luồng, xung nhịp boost lên tới 5.7GHz",
                    Price = 14990000, StockQuantity = 8, LowStockThreshold = 2,
                    Brand = "AMD", CategoryId = categories[5].Id,
                    Specifications = "{\"Cores\":\"16C/32T\",\"Base Clock\":\"4.5GHz\",\"Boost Clock\":\"5.7GHz\",\"TDP\":\"170W\",\"Socket\":\"AM5\"}"
                },
                new()
                {
                    Name = "Samsung 990 Pro 2TB NVMe SSD",
                    Slug = "samsung-990-pro-2tb-nvme-ssd",
                    Description = "SSD NVMe Gen4 tốc độ đọc 7450MB/s, ghi 6900MB/s",
                    Price = 5490000, StockQuantity = 45, LowStockThreshold = 10,
                    Brand = "Samsung", CategoryId = categories[5].Id,
                    Specifications = "{\"Capacity\":\"2TB\",\"Interface\":\"PCIe Gen4 NVMe\",\"Read\":\"7450 MB/s\",\"Write\":\"6900 MB/s\",\"TBW\":\"1200TB\"}"
                }
            };

            context.Products.AddRange(products);
            await context.SaveChangesAsync();

            // Seed Coupons
            var coupons = new List<Coupon>
            {
                new()
                {
                    Code = "WELCOME10", DiscountPercent = 10, MaxDiscountAmount = 2000000,
                    MinOrderAmount = 5000000, ExpiryDate = DateTime.UtcNow.AddMonths(6),
                    UsageLimit = 100, IsActive = true
                },
                new()
                {
                    Code = "TECHSTORE20", DiscountPercent = 20, MaxDiscountAmount = 5000000,
                    MinOrderAmount = 20000000, ExpiryDate = DateTime.UtcNow.AddMonths(3),
                    UsageLimit = 50, IsActive = true
                },
                new()
                {
                    Code = "NEWYEAR2026", DiscountPercent = 15, MaxDiscountAmount = 3000000,
                    MinOrderAmount = 10000000, ExpiryDate = new DateTime(2026, 3, 31),
                    UsageLimit = 200, IsActive = true
                }
            };

            context.Coupons.AddRange(coupons);
            await context.SaveChangesAsync();
        }
    }
}
