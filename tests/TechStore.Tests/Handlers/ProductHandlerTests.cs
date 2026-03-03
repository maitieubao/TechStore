using Moq;
using FluentAssertions;
using TechStore.Application.Common.Interfaces;
using TechStore.Application.Features.Products.Commands;
using TechStore.Application.Features.Products.Handlers;
using TechStore.Domain.Entities;
using System.Linq.Expressions;

namespace TechStore.Tests.Handlers
{
    public class ProductHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;

        public ProductHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
        }

        [Fact]
        public async Task CreateProduct_ShouldReturnId_WhenSuccessful()
        {
            // Arrange
            var productRepo = new Mock<IGenericRepository<Product>>();
            productRepo.Setup(r => r.CountAsync(It.IsAny<Expression<Func<Product, bool>>>()))
                .ReturnsAsync(0);
            productRepo.Setup(r => r.AddAsync(It.IsAny<Product>()))
                .Returns(Task.CompletedTask);

            _mockUnitOfWork.Setup(u => u.Products).Returns(productRepo.Object);
            _mockUnitOfWork.Setup(u => u.CompleteAsync()).ReturnsAsync(1);

            var handler = new CreateProductHandler(_mockUnitOfWork.Object);
            var command = new CreateProductCommand
            {
                Name = "Test Laptop",
                Description = "A test laptop",
                Price = 15000000,
                StockQuantity = 10,
                Brand = "TestBrand",
                CategoryId = 1
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            productRepo.Verify(r => r.AddAsync(It.Is<Product>(p =>
                p.Name == "Test Laptop" &&
                p.Slug == "test-laptop" &&
                p.Brand == "TestBrand"
            )), Times.Once);
            _mockUnitOfWork.Verify(u => u.CompleteAsync(), Times.Once);
        }

        [Fact]
        public async Task CreateProduct_ShouldGenerateUniqueSlug_WhenDuplicate()
        {
            // Arrange
            var productRepo = new Mock<IGenericRepository<Product>>();
            productRepo.Setup(r => r.CountAsync(It.IsAny<Expression<Func<Product, bool>>>()))
                .ReturnsAsync(1); // Simulate existing slug
            productRepo.Setup(r => r.AddAsync(It.IsAny<Product>()))
                .Returns(Task.CompletedTask);

            _mockUnitOfWork.Setup(u => u.Products).Returns(productRepo.Object);
            _mockUnitOfWork.Setup(u => u.CompleteAsync()).ReturnsAsync(1);

            var handler = new CreateProductHandler(_mockUnitOfWork.Object);
            var command = new CreateProductCommand
            {
                Name = "Test Laptop",
                Description = "A duplicate test",
                Price = 10000000,
                StockQuantity = 5
            };

            // Act
            await handler.Handle(command, CancellationToken.None);

            // Assert - slug should have suffix
            productRepo.Verify(r => r.AddAsync(It.Is<Product>(p =>
                p.Slug == "test-laptop-2"
            )), Times.Once);
        }

        [Fact]
        public async Task UpdateProduct_ShouldReturnFalse_WhenProductNotFound()
        {
            // Arrange
            var productRepo = new Mock<IGenericRepository<Product>>();
            productRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Product?)null);

            _mockUnitOfWork.Setup(u => u.Products).Returns(productRepo.Object);

            var handler = new UpdateProductHandler(_mockUnitOfWork.Object);
            var command = new UpdateProductCommand
            {
                Id = 999,
                Name = "Non-existent",
                Description = "Not found",
                Price = 1000,
                StockQuantity = 0
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task UpdateProduct_ShouldUpdateSlug_WhenNameChanges()
        {
            // Arrange
            var existingProduct = new Product
            {
                Id = 1,
                Name = "Old Product",
                Slug = "old-product",
                Description = "Description",
                Price = 1000,
                StockQuantity = 5
            };

            var productRepo = new Mock<IGenericRepository<Product>>();
            productRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(existingProduct);
            productRepo.Setup(r => r.CountAsync(It.IsAny<Expression<Func<Product, bool>>>()))
                .ReturnsAsync(0);

            _mockUnitOfWork.Setup(u => u.Products).Returns(productRepo.Object);
            _mockUnitOfWork.Setup(u => u.CompleteAsync()).ReturnsAsync(1);

            var handler = new UpdateProductHandler(_mockUnitOfWork.Object);
            var command = new UpdateProductCommand
            {
                Id = 1,
                Name = "New Product Name",
                Description = "Updated description",
                Price = 2000,
                StockQuantity = 10
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().BeTrue();
            existingProduct.Slug.Should().Be("new-product-name");
            existingProduct.Name.Should().Be("New Product Name");
        }

        [Fact]
        public async Task DeleteProduct_ShouldReturnTrue_WhenProductExists()
        {
            // Arrange
            var product = new Product { Id = 1, Name = "Product to delete", Slug = "product-to-delete" };
            var productRepo = new Mock<IGenericRepository<Product>>();
            productRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(product);

            _mockUnitOfWork.Setup(u => u.Products).Returns(productRepo.Object);
            _mockUnitOfWork.Setup(u => u.CompleteAsync()).ReturnsAsync(1);

            var handler = new DeleteProductHandler(_mockUnitOfWork.Object);

            // Act
            var result = await handler.Handle(new DeleteProductCommand(1), CancellationToken.None);

            // Assert
            result.Should().BeTrue();
            productRepo.Verify(r => r.Delete(product), Times.Once);
        }

        [Fact]
        public async Task DeleteProduct_ShouldReturnFalse_WhenProductNotFound()
        {
            // Arrange
            var productRepo = new Mock<IGenericRepository<Product>>();
            productRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Product?)null);

            _mockUnitOfWork.Setup(u => u.Products).Returns(productRepo.Object);

            var handler = new DeleteProductHandler(_mockUnitOfWork.Object);

            // Act
            var result = await handler.Handle(new DeleteProductCommand(999), CancellationToken.None);

            // Assert
            result.Should().BeFalse();
        }
    }
}
