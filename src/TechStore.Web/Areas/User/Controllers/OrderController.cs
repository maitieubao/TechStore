using Microsoft.AspNetCore.Mvc;
using TechStore.Shared.DTOs;
using TechStore.Web.Models.ViewModels;
using TechStore.Web.Services.Interfaces;

using Microsoft.AspNetCore.Authorization;

namespace TechStore.Web.Areas.User.Controllers
{
    [Area("User")]
    [Authorize]
    public class OrderController : Controller
    {
        private readonly IOrderApiService _orderService;
        private readonly ICartApiService _cartService;
        private readonly IPaymentApiService _paymentService;

        public OrderController(
            IOrderApiService orderService,
            ICartApiService cartService,
            IPaymentApiService paymentService)
        {
            _orderService = orderService;
            _cartService = cartService;
            _paymentService = paymentService;
        }

        // GET: /order/checkout
        public async Task<IActionResult> Checkout()
        {
            if (!User.Identity?.IsAuthenticated ?? true)
                return RedirectToAction("Login", "Account", new { area = "", returnUrl = "/User/Order/Checkout" });

            var cart = await _cartService.GetCartAsync();
            if (cart == null || !cart.Items.Any())
            {
                TempData["Error"] = "Giỏ hàng trống!";
                return RedirectToAction("Index", "Cart");
            }

            return View(new CheckoutVM { Cart = cart });
        }

        // POST: /order/placeorder
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceOrder(CheckoutVM vm)
        {
            var cart = await _cartService.GetCartAsync();
            if (cart == null || !cart.Items.Any())
                return RedirectToAction("Index", "Cart");

            var dto = new CreateOrderDto
            {
                Items = cart.Items.Select(i => new CreateOrderItemDto
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity
                }).ToList(),
                ShippingAddress = vm.ShippingAddress,
                PhoneNumber = vm.PhoneNumber,
                Note = vm.Note,
                CouponCode = vm.CouponCode
            };

            var order = await _orderService.CreateOrderAsync(dto);
            if (order == null)
            {
                TempData["Error"] = "Đặt hàng thất bại. Vui lòng thử lại.";
                return RedirectToAction(nameof(Checkout));
            }

            // Clear cart after successful order
            await _cartService.ClearCartAsync();

            // If VIETQR payment method
            if (vm.PaymentMethod == "VIETQR")
            {
                var bankId = "970422"; // MB Bank
                var accountNo = "000000000000"; // Validate with user later
                var template = "compact2";
                var accountName = "TECHSTORE";
                
                var amount = (int)order.TotalAmount;
                var addInfo = $"TechStore {order.Id}";
                
                var qrUrl = $"https://img.vietqr.io/image/{bankId}-{accountNo}-{template}.png?amount={amount}&addInfo={Uri.EscapeDataString(addInfo)}&accountName={Uri.EscapeDataString(accountName)}";
                
                return RedirectToAction(nameof(PaymentQR), new { orderId = order.Id, qrUrl, amount = order.TotalAmount });
            }

            TempData["Success"] = "Đặt hàng thành công!";
            return RedirectToAction(nameof(Detail), new { id = order.Id });
        }

        // GET: /order/history
        public async Task<IActionResult> History()
        {
            if (!User.Identity?.IsAuthenticated ?? true)
                return RedirectToAction("Login", "Account", new { area = "" });

            var orders = await _orderService.GetMyOrdersAsync();
            return View(orders ?? new());
        }

        // GET: /order/detail/{id}
        public async Task<IActionResult> Detail(int id)
        {
            var order = await _orderService.GetOrderByIdAsync(id);
            if (order == null) return NotFound();

            var payments = await _paymentService.GetPaymentsByOrderAsync(id);

            return View(new OrderDetailVM
            {
                Order = order,
                Payments = payments ?? new()
            });
        }

        // POST: /order/cancel/{id}
        [HttpPost]
        public async Task<IActionResult> Cancel(int id)
        {
            var success = await _orderService.CancelOrderAsync(id);
            TempData[success ? "Success" : "Error"] =
                success ? "Đơn hàng đã được hủy." : "Không thể hủy đơn hàng.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        // GET: /order/payment-qr
        public IActionResult PaymentQR(int orderId, string qrUrl, decimal amount)
        {
            ViewBag.OrderId = orderId;
            ViewBag.QrUrl = qrUrl;
            ViewBag.Amount = amount;
            return View();
        }

    }
}
