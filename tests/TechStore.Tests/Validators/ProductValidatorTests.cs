using FluentAssertions;
using FluentValidation.TestHelper;
using TechStore.Application.Features.Products.Commands;
using TechStore.Application.Validators;

namespace TechStore.Tests.Validators
{
    public class ProductValidatorTests
    {
        private readonly CreateProductCommandValidator _createValidator;
        private readonly UpdateProductCommandValidator _updateValidator;

        public ProductValidatorTests()
        {
            _createValidator = new CreateProductCommandValidator();
            _updateValidator = new UpdateProductCommandValidator();
        }

        [Fact]
        public void CreateProduct_ShouldFail_WhenNameIsEmpty()
        {
            var command = new CreateProductCommand
            {
                Name = "",
                Description = "Valid description",
                Price = 1000,
                StockQuantity = 10
            };

            var result = _createValidator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void CreateProduct_ShouldFail_WhenPriceIsZero()
        {
            var command = new CreateProductCommand
            {
                Name = "Valid Product",
                Description = "Valid description",
                Price = 0,
                StockQuantity = 10
            };

            var result = _createValidator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.Price);
        }

        [Fact]
        public void CreateProduct_ShouldFail_WhenStockIsNegative()
        {
            var command = new CreateProductCommand
            {
                Name = "Valid Product",
                Description = "Valid description",
                Price = 1000,
                StockQuantity = -1
            };

            var result = _createValidator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.StockQuantity);
        }

        [Fact]
        public void CreateProduct_ShouldPass_WhenAllFieldsValid()
        {
            var command = new CreateProductCommand
            {
                Name = "Valid Product Name",
                Description = "A valid product description here",
                Price = 15000000,
                StockQuantity = 50,
                Brand = "TestBrand"
            };

            var result = _createValidator.TestValidate(command);
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void UpdateProduct_ShouldFail_WhenIdIsZero()
        {
            var command = new UpdateProductCommand
            {
                Id = 0,
                Name = "Valid Name",
                Description = "Valid description",
                Price = 1000,
                StockQuantity = 10
            };

            var result = _updateValidator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.Id);
        }

        [Fact]
        public void UpdateProduct_ShouldPass_WhenAllFieldsValid()
        {
            var command = new UpdateProductCommand
            {
                Id = 1,
                Name = "Updated Product",
                Description = "Updated product description",
                Price = 20000000,
                StockQuantity = 25
            };

            var result = _updateValidator.TestValidate(command);
            result.ShouldNotHaveAnyValidationErrors();
        }
    }
}
