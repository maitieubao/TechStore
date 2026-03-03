using Moq;
using FluentAssertions;
using TechStore.Application.Common.Interfaces;
using TechStore.Application.Common.Exceptions;
using TechStore.Application.Features.Orders.Commands;
using TechStore.Application.Features.Orders.Handlers;
using TechStore.Domain.Entities;
using System.Linq.Expressions;

namespace TechStore.Tests.Handlers
{
    public class OrderHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IGenericRepository<Product>> _mockProductRepo;
        private readonly Mock<IGenericRepository<Order>> _mockOrderRepo;
        private readonly Mock<IGenericRepository<Coupon>> _mockCouponRepo;

        public OrderHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockProductRepo = new Mock<IGenericRepository<Product>>();
            _mockOrderRepo = new Mock<IGenericRepository<Order>>();
            _mockCouponRepo = new Mock<IGenericRepository<Coupon>>();

            _mockUnitOfWork.Setup(u => u.Products).Returns(_mockProductRepo.Object);
            _mockUnitOfWork.Setup(u => u.Orders).Returns(_mockOrderRepo.Object);
            _mockUnitOfWork.Setup(u => u.Coupons).Returns(_mockCouponRepo.Object);
            _mockUnitOfWork.Setup(u => u.CompleteAsync()).ReturnsAsync(1);
        }

        [Fact]
        public async Task CreateOrder_ShouldSucceed_WhenStockAvailable()
        {
            // Arrange
            var product = new Product
            {
                Id = 1,
                Name = "Test Laptop",
                Price = 15000000,
                StockQuantity = 10,
                IsActive = true
            };

            _mockProductRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(product);

            var handler = new CreateOrderHandler(_mockUnitOfWork.Object);
            var command = new CreateOrderCommand
            {
                UserId = "user-001",
                Items = new() { new() { ProductId = 1, Quantity = 2 } },
                ShippingAddress = "Hanoi, Vietnam",
                PhoneNumber = "0987654321"
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.TotalAmount.Should().Be(30000000);
            result.Status.Should().Be("Pending");
            result.Items.Should().HaveCount(1);
            result.Items[0].Quantity.Should().Be(2);
            result.Items[0].UnitPrice.Should().Be(15000000);

            // Verify stock was deducted
            product.StockQuantity.Should().Be(8);

            _mockOrderRepo.Verify(r => r.AddAsync(It.IsAny<Order>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.CompleteAsync(), Times.Once);
        }

        [Fact]
        public async Task CreateOrder_ShouldFail_WhenProductNotFound()
        {
            // Arrange
            _mockProductRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Product?)null);

            var handler = new CreateOrderHandler(_mockUnitOfWork.Object);
            var command = new CreateOrderCommand
            {
                UserId = "user-001",
                Items = new() { new() { ProductId = 999, Quantity = 1 } }
            };

            // Act & Assert
            var act = async () => await handler.Handle(command, CancellationToken.None);
            await act.Should().ThrowAsync<Exception>()
                .WithMessage("*not found*");
        }

        [Fact]
        public async Task CreateOrder_ShouldFail_WhenInsufficientStock()
        {
            // Arrange
            var product = new Product
            {
                Id = 1,
                Name = "Limited Product",
                Price = 5000000,
                StockQuantity = 2,
                IsActive = true
            };

            _mockProductRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(product);

            var handler = new CreateOrderHandler(_mockUnitOfWork.Object);
            var command = new CreateOrderCommand
            {
                UserId = "user-001",
                Items = new() { new() { ProductId = 1, Quantity = 5 } }
            };

            // Act & Assert
            var act = async () => await handler.Handle(command, CancellationToken.None);
            await act.Should().ThrowAsync<Exception>()
                .WithMessage("*Insufficient stock*");
        }

        [Fact]
        public async Task CreateOrder_ShouldFail_WhenProductInactive()
        {
            // Arrange
            var product = new Product
            {
                Id = 1,
                Name = "Discontinued Product",
                Price = 5000000,
                StockQuantity = 10,
                IsActive = false
            };

            _mockProductRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(product);

            var handler = new CreateOrderHandler(_mockUnitOfWork.Object);
            var command = new CreateOrderCommand
            {
                UserId = "user-001",
                Items = new() { new() { ProductId = 1, Quantity = 1 } }
            };

            // Act & Assert
            var act = async () => await handler.Handle(command, CancellationToken.None);
            await act.Should().ThrowAsync<Exception>()
                .WithMessage("*no longer available*");
        }

        [Fact]
        public async Task CreateOrder_ShouldCalculateDiscount_WhenCouponApplied()
        {
            // Arrange
            var product = new Product
            {
                Id = 1,
                Name = "Test Product",
                Price = 10000000,
                StockQuantity = 10,
                IsActive = true
            };

            var coupon = new Coupon
            {
                Id = 1,
                Code = "SAVE10",
                DiscountPercent = 10,
                IsActive = true,
                ExpiryDate = DateTime.UtcNow.AddDays(30),
                TimesUsed = 0,
                UsageLimit = 100,
                MinOrderAmount = 0,
                MaxDiscountAmount = 5000000
            };

            _mockProductRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(product);

            // Mock IQueryable for Coupons
            var couponList = new List<Coupon> { coupon }.AsQueryable();
            var mockQueryable = new Mock<IQueryable<Coupon>>();
            mockQueryable.As<IAsyncEnumerable<Coupon>>()
                .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
                .Returns(new TestAsyncEnumerator<Coupon>(couponList.GetEnumerator()));
            mockQueryable.As<IQueryProvider>()
                .Setup(m => m.CreateQuery<Coupon>(It.IsAny<Expression>()))
                .Returns(couponList);
            mockQueryable.Setup(m => m.Provider).Returns(new TestAsyncQueryProvider<Coupon>(couponList.Provider));
            mockQueryable.Setup(m => m.Expression).Returns(couponList.Expression);
            mockQueryable.Setup(m => m.ElementType).Returns(couponList.ElementType);
            mockQueryable.Setup(m => m.GetEnumerator()).Returns(couponList.GetEnumerator());

            _mockCouponRepo.Setup(r => r.Query()).Returns(mockQueryable.Object);

            var handler = new CreateOrderHandler(_mockUnitOfWork.Object);
            var command = new CreateOrderCommand
            {
                UserId = "user-001",
                Items = new() { new() { ProductId = 1, Quantity = 1 } },
                CouponCode = "SAVE10"
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.TotalAmount.Should().Be(10000000);
            result.DiscountAmount.Should().Be(1000000); // 10% of 10M
        }

        [Fact]
        public async Task CreateOrder_ShouldHandleConcurrencyException()
        {
            // Arrange
            var product = new Product
            {
                Id = 1,
                Name = "Popular Product",
                Price = 5000000,
                StockQuantity = 1,
                IsActive = true
            };

            _mockProductRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(product);
            _mockUnitOfWork.Setup(u => u.CompleteAsync())
                .ThrowsAsync(new ConcurrencyException("Concurrent update detected"));

            var handler = new CreateOrderHandler(_mockUnitOfWork.Object);
            var command = new CreateOrderCommand
            {
                UserId = "user-001",
                Items = new() { new() { ProductId = 1, Quantity = 1 } }
            };

            // Act & Assert
            var act = async () => await handler.Handle(command, CancellationToken.None);
            await act.Should().ThrowAsync<Exception>()
                .WithMessage("*concurrent*");
        }

        [Fact]
        public async Task CreateOrder_MultipleItems_ShouldCalculateTotal()
        {
            // Arrange
            var product1 = new Product { Id = 1, Name = "Product A", Price = 5000000, StockQuantity = 10, IsActive = true };
            var product2 = new Product { Id = 2, Name = "Product B", Price = 3000000, StockQuantity = 5, IsActive = true };

            _mockProductRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(product1);
            _mockProductRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(product2);

            var handler = new CreateOrderHandler(_mockUnitOfWork.Object);
            var command = new CreateOrderCommand
            {
                UserId = "user-001",
                Items = new()
                {
                    new() { ProductId = 1, Quantity = 2 }, // 10M
                    new() { ProductId = 2, Quantity = 3 }  // 9M
                }
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.TotalAmount.Should().Be(19000000); // 10M + 9M
            result.Items.Should().HaveCount(2);
            product1.StockQuantity.Should().Be(8);
            product2.StockQuantity.Should().Be(2);
        }
    }

    // ===== Infrastructure for async IQueryable mocking =====
    internal class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        private readonly IEnumerator<T> _inner;
        public TestAsyncEnumerator(IEnumerator<T> inner) => _inner = inner;
        public T Current => _inner.Current;
        public ValueTask DisposeAsync() { _inner.Dispose(); return ValueTask.CompletedTask; }
        public ValueTask<bool> MoveNextAsync() => new(_inner.MoveNext());
    }

    internal class TestAsyncQueryProvider<TEntity> : IQueryProvider
    {
        private readonly IQueryProvider _inner;
        internal TestAsyncQueryProvider(IQueryProvider inner) => _inner = inner;
        public IQueryable CreateQuery(Expression expression) => new TestAsyncEnumerable<TEntity>(expression);
        public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => new TestAsyncEnumerable<TElement>(expression);
        public object? Execute(Expression expression) => _inner.Execute(expression);
        public TResult Execute<TResult>(Expression expression) => _inner.Execute<TResult>(expression);
    }

    internal class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public TestAsyncEnumerable(IEnumerable<T> enumerable) : base(enumerable) { }
        public TestAsyncEnumerable(Expression expression) : base(expression) { }
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken token = default)
            => new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
        IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
    }
}
