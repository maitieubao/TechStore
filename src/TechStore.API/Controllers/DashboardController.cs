using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechStore.Application.Common.Interfaces;
using TechStore.Domain.Entities;
using TechStore.Infrastructure.Persistence;
using TechStore.Shared.DTOs;
using TechStore.Shared.Responses;

namespace TechStore.API.Controllers
{
    /// <summary>
    /// Controller cung cấp dữ liệu tổng quan cho bảng điều khiển Admin (Dashboard).
    /// 
    /// Chức năng chính:
    ///   - Thống kê tổng doanh thu (chỉ tính đơn hàng "Delivered").
    ///   - Thống kê tổng số đơn hàng, sản phẩm, người dùng.
    ///   - Top 10 sản phẩm bán chạy nhất (theo số lượng bán).
    ///   - Biểu đồ doanh thu theo tháng trong 12 tháng gần nhất.
    ///   - Phân bổ đơn hàng theo trạng thái (Pending, Shipping, Delivered, Cancelled).
    /// 
    /// Route gốc: api/dashboard
    /// Phân quyền: [Authorize(Roles = "Admin")] — chỉ Admin mới có quyền truy cập.
    /// 
    /// Lưu ý kiến trúc: Controller này truy cập trực tiếp AppDbContext (thay vì qua UnitOfWork)
    /// để tận dụng các câu query phức tạp với GroupBy, SumAsync, CountAsync của EF Core.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        /// <summary>
        /// Khởi tạo controller với AppDbContext và UserManager được inject qua DI.
        /// </summary>
        /// <param name="context">Database context để query dữ liệu đơn hàng, sản phẩm.</param>
        /// <param name="userManager">ASP.NET Identity UserManager để đếm số tài khoản.</param>
        public DashboardController(AppDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        /// <summary>
        /// [GET] api/dashboard
        /// Lấy toàn bộ dữ liệu thống kê cho trang Dashboard của Admin.
        /// 
        /// Workflow (thực hiện song song nhiều query):
        /// 
        ///   [1] Tổng doanh thu (TotalRevenue):
        ///       - Lọc đơn hàng có Status = "Delivered".
        ///       - Tính tổng (TotalAmount - DiscountAmount) của các đơn đã giao thành công.
        /// 
        ///   [2] Các chỉ số tổng hợp:
        ///       - TotalOrders: Tổng số đơn hàng trong hệ thống.
        ///       - TotalProducts: Tổng số sản phẩm.
        ///       - TotalUsers: Tổng số tài khoản người dùng (qua UserManager).
        /// 
        ///   [3] Top 10 sản phẩm bán chạy (TopSellingProducts):
        ///       - GroupBy ProductId trong bảng OrderItems.
        ///       - Tính tổng số lượng bán và doanh thu từng sản phẩm.
        ///       - Sắp xếp giảm dần theo TotalSold, lấy 10 sản phẩm đầu.
        ///       - Sau đó lookup tên sản phẩm từ bảng Products (vòng lặp N+1 — cải thiện sau).
        /// 
        ///   [4] Doanh thu theo tháng (RevenueByMonth):
        ///       - Lọc đơn "Delivered" trong 12 tháng gần nhất.
        ///       - GroupBy theo (Year, Month).
        ///       - Tính tổng doanh thu và số đơn hàng theo từng tháng.
        ///       - Sắp xếp tăng dần theo thời gian → dữ liệu cho biểu đồ line chart.
        /// 
        ///   [5] Phân bổ đơn theo trạng thái (OrdersByStatus):
        ///       - GroupBy Status của tất cả đơn hàng.
        ///       - Trả về Dictionary&lt;string, int&gt; (Status → Count).
        ///       → Dữ liệu cho biểu đồ pie/donut chart.
        /// 
        /// Response: DashboardDto gộp tất cả thống kê trên.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<DashboardDto>>> GetDashboard()
        {
            // [1] Tổng doanh thu từ các đơn hàng đã giao thành công
            var totalRevenue = await _context.Orders
                .Where(o => o.Status == "Delivered")
                .SumAsync(o => o.TotalAmount - o.DiscountAmount);

            // [2] Các chỉ số thống kê tổng hợp
            var totalOrders = await _context.Orders.CountAsync();
            var totalProducts = await _context.Products.CountAsync();
            var totalUsers = await _userManager.Users.CountAsync();

            // [3] Top 10 sản phẩm bán chạy nhất (theo số lượng đã bán)
            var topProducts = await _context.OrderItems
                .GroupBy(oi => oi.ProductId)
                .Select(g => new TopProductDto
                {
                    ProductId = g.Key,
                    TotalSold = g.Sum(oi => oi.Quantity),
                    TotalRevenue = g.Sum(oi => oi.Quantity * oi.UnitPrice)
                })
                .OrderByDescending(tp => tp.TotalSold)
                .Take(10)
                .ToListAsync();

            // Lookup tên sản phẩm cho từng item trong top (cải thiện: dùng Join thay vì N+1)
            foreach (var tp in topProducts)
            {
                var product = await _context.Products.FindAsync(tp.ProductId);
                tp.ProductName = product?.Name ?? "Unknown";
            }

            // [4] Doanh thu theo tháng trong 12 tháng gần nhất (cho line chart)
            var twelveMonthsAgo = DateTime.UtcNow.AddMonths(-12);
            var revenueByMonth = await _context.Orders
                .Where(o => o.Status == "Delivered" && o.OrderDate >= twelveMonthsAgo)
                .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
                .Select(g => new RevenueByMonthDto
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Revenue = g.Sum(o => o.TotalAmount - o.DiscountAmount),
                    OrderCount = g.Count()
                })
                .OrderBy(r => r.Year).ThenBy(r => r.Month)
                .ToListAsync();

            // [5] Phân bổ số đơn hàng theo từng trạng thái (cho pie chart)
            var ordersByStatus = await _context.Orders
                .GroupBy(o => o.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Status, x => x.Count);

            // Gộp tất cả thống kê vào DashboardDto
            var dashboard = new DashboardDto
            {
                TotalRevenue = totalRevenue,
                TotalOrders = totalOrders,
                TotalProducts = totalProducts,
                TotalUsers = totalUsers,
                TopSellingProducts = topProducts,
                RevenueByMonth = revenueByMonth,
                OrdersByStatus = ordersByStatus
            };

            return Ok(ApiResponse<DashboardDto>.SuccessResponse(dashboard));
        }
    }
}
