using FluentAssertions;
using FluentValidation.TestHelper;
using TechStore.Application.Features.Orders.Commands;
using TechStore.Application.Validators;
using TechStore.Shared.DTOs;

namespace TechStore.Tests.Validators
{
    public class OrderValidatorTests
    {
        private readonly CreateOrderValidator _validator;

        public OrderValidatorTests()
        {
            _validator = new CreateOrderValidator();
        }

        [Fact]
        public void Should_HaveError_WhenItemsEmpty()
        {
            var command = new CreateOrderCommand
            {
                UserId = "user-001",
                Items = new()
            };

            var result = _validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.Items);
        }

        [Fact]
        public void Should_HaveError_WhenProductIdInvalid()
        {
            var command = new CreateOrderCommand
            {
                UserId = "user-001",
                Items = new() { new CreateOrderItemDto { ProductId = 0, Quantity = 1 } }
            };

            var result = _validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor("Items[0].ProductId");
        }

        [Fact]
        public void Should_HaveError_WhenQuantityZero()
        {
            var command = new CreateOrderCommand
            {
                UserId = "user-001",
                Items = new() { new CreateOrderItemDto { ProductId = 1, Quantity = 0 } }
            };

            var result = _validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor("Items[0].Quantity");
        }

        [Fact]
        public void Should_HaveError_WhenQuantityNegative()
        {
            var command = new CreateOrderCommand
            {
                UserId = "user-001",
                Items = new() { new CreateOrderItemDto { ProductId = 1, Quantity = -5 } }
            };

            var result = _validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor("Items[0].Quantity");
        }

        [Fact]
        public void Should_NotHaveError_WhenValidCommand()
        {
            var command = new CreateOrderCommand
            {
                UserId = "user-001",
                Items = new()
                {
                    new CreateOrderItemDto { ProductId = 1, Quantity = 2 },
                    new CreateOrderItemDto { ProductId = 5, Quantity = 1 }
                },
                ShippingAddress = "123 Main St",
                PhoneNumber = "0987654321"
            };

            var result = _validator.TestValidate(command);
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Should_NotHaveError_WhenOptionalFieldsMissing()
        {
            var command = new CreateOrderCommand
            {
                UserId = "user-001",
                Items = new() { new CreateOrderItemDto { ProductId = 1, Quantity = 1 } }
                // ShippingAddress, PhoneNumber, Note, CouponCode are all null — should be OK
            };

            var result = _validator.TestValidate(command);
            result.ShouldNotHaveAnyValidationErrors();
        }
    }
}
