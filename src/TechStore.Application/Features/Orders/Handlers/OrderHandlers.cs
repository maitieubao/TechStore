using MediatR;
using Microsoft.EntityFrameworkCore;
using TechStore.Application.Common.Exceptions;
using TechStore.Application.Common.Interfaces;
using TechStore.Application.Features.Orders.Commands;
using TechStore.Application.Features.Orders.Queries;
using TechStore.Domain.Entities;
using TechStore.Shared.DTOs;

namespace TechStore.Application.Features.Orders.Handlers
{
    public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, OrderDto>
    {
        private readonly IUnitOfWork _unitOfWork;

        public CreateOrderHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<OrderDto> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
        {
            // Create Order
            var order = new Order
            {
                UserId = request.UserId,
                OrderDate = DateTime.UtcNow,
                Status = "Pending",
                PaymentStatus = "Unpaid",
                PaymentMethod = "COD",
                ShippingAddress = request.ShippingAddress,
                PhoneNumber = request.PhoneNumber,
                Note = request.Note,
                CouponCode = request.CouponCode,
                Items = new List<OrderItem>()
            };

            decimal totalAmount = 0;
            var productUpdates = new List<Product>();

            // Process Items
            foreach (var itemDto in request.Items)
            {
                var product = await _unitOfWork.Products.GetByIdAsync(itemDto.ProductId);
                if (product == null)
                    throw new Exception($"Product with ID {itemDto.ProductId} not found");

                if (!product.IsActive)
                    throw new Exception($"Product '{product.Name}' is no longer available");

                // Check stock availability
                if (product.StockQuantity < itemDto.Quantity)
                    throw new Exception($"Insufficient stock for product '{product.Name}'. Available: {product.StockQuantity}");

                // Add Item
                var orderItem = new OrderItem
                {
                    ProductId = product.Id,
                    Quantity = itemDto.Quantity,
                    UnitPrice = product.Price
                };
                order.Items.Add(orderItem);

                // Calculate total
                totalAmount += product.Price * itemDto.Quantity;

                // Update Stock
                product.StockQuantity -= itemDto.Quantity;
                product.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.Products.Update(product);
                productUpdates.Add(product);
            }

            // Apply Coupon
            decimal discountAmount = 0;
            if (!string.IsNullOrEmpty(request.CouponCode))
            {
                var coupons = await _unitOfWork.Coupons.Query()
                    .Where(c => c.Code == request.CouponCode.ToUpper())
                    .ToListAsync(cancellationToken);
                
                var coupon = coupons.FirstOrDefault();

                if (coupon != null && coupon.IsActive && coupon.ExpiryDate > DateTime.UtcNow
                    && coupon.TimesUsed < coupon.UsageLimit && totalAmount >= coupon.MinOrderAmount)
                {
                    discountAmount = totalAmount * coupon.DiscountPercent / 100;
                    if (coupon.MaxDiscountAmount.HasValue && discountAmount > coupon.MaxDiscountAmount.Value)
                        discountAmount = coupon.MaxDiscountAmount.Value;

                    coupon.TimesUsed++;
                    _unitOfWork.Coupons.Update(coupon);
                }
            }

            order.TotalAmount = totalAmount;
            order.DiscountAmount = discountAmount;

            await _unitOfWork.Orders.AddAsync(order);

            try
            {
                await _unitOfWork.CompleteAsync();
            }
            catch (ConcurrencyException ex)
            {
                // This will be caught by global exception handling or controller
                throw new Exception("Stock validation failed due to concurrent orders. Please try again.", ex);
            }

            return new OrderDto
            {
                Id = order.Id,
                UserId = order.UserId,
                OrderDate = order.OrderDate,
                TotalAmount = order.TotalAmount,
                DiscountAmount = order.DiscountAmount,
                Status = order.Status,
                ShippingAddress = order.ShippingAddress,
                PhoneNumber = order.PhoneNumber,
                Note = order.Note,
                CouponCode = order.CouponCode,
                Items = order.Items.Select(i => new OrderItemDto
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    ProductName = productUpdates.FirstOrDefault(p => p.Id == i.ProductId)?.Name ?? ""
                }).ToList()
            };
        }
    }

    public class GetAllOrdersHandler : IRequestHandler<GetAllOrdersQuery, List<OrderDto>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetAllOrdersHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<List<OrderDto>> Handle(GetAllOrdersQuery request, CancellationToken cancellationToken)
        {
            var orders = await _unitOfWork.Orders.Query()
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync(cancellationToken);

            return orders.Select(OrderMapper.MapToDto).ToList();
        }
    }

    public class GetOrderByIdHandler : IRequestHandler<GetOrderByIdQuery, OrderDto?>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetOrderByIdHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<OrderDto?> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
        {
            var order = await _unitOfWork.Orders.Query()
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken);
            
            return order == null ? null : OrderMapper.MapToDto(order);
        }
    }

    public class GetOrdersByUserIdHandler : IRequestHandler<GetOrdersByUserIdQuery, List<OrderDto>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetOrdersByUserIdHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<List<OrderDto>> Handle(GetOrdersByUserIdQuery request, CancellationToken cancellationToken)
        {
            var orders = await _unitOfWork.Orders.Query()
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .Where(o => o.UserId == request.UserId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync(cancellationToken);

            return orders.Select(OrderMapper.MapToDto).ToList();
        }
    }

    internal static class OrderMapper
    {
        public static OrderDto MapToDto(Order order) => new()
        {
            Id = order.Id,
            UserId = order.UserId,
            OrderDate = order.OrderDate,
            TotalAmount = order.TotalAmount,
            DiscountAmount = order.DiscountAmount,
            Status = order.Status,
            ShippingAddress = order.ShippingAddress,
            PhoneNumber = order.PhoneNumber,
            Note = order.Note,
            CouponCode = order.CouponCode,
            Items = order.Items.Select(i => new OrderItemDto
            {
                ProductId = i.ProductId,
                ProductName = i.Product?.Name ?? "",
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList()
        };
    }
}
