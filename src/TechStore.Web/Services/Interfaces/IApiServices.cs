using TechStore.Shared.DTOs;
using TechStore.Shared.Responses;

namespace TechStore.Web.Services.Interfaces
{
    public interface IProductApiService
    {
        Task<PagedResultDto<ProductDto>?> GetProductsAsync(ProductFilterDto filter);
        Task<ProductDto?> GetProductByIdAsync(int id);
        Task<ProductDto?> GetProductBySlugAsync(string slug);
        Task<List<string>?> GetBrandsAsync();
        Task<(decimal Min, decimal Max)?> GetPriceRangeAsync();
        Task<int> CreateProductAsync(TechStore.Web.Models.ViewModels.CreateProductVM model);
        Task<bool> UpdateProductAsync(int id, TechStore.Web.Models.ViewModels.CreateProductVM model);
        Task<bool> DeleteProductAsync(int id);
        Task<bool> UploadImageAsync(int productId, Microsoft.AspNetCore.Http.IFormFile file, bool isPrimary = false);
    }

    public interface ICategoryApiService
    {
        Task<List<CategoryDto>?> GetCategoriesAsync();
        Task<CategoryDto?> GetByIdAsync(int id);
        Task<bool> CreateCategoryAsync(CreateCategoryDto dto);
        Task<bool> UpdateCategoryAsync(int id, CreateCategoryDto dto);
        Task<bool> DeleteCategoryAsync(int id);
    }

    public interface IAuthApiService
    {
        Task<AuthResponseDto?> LoginAsync(string userName, string password);
        Task<AuthResponseDto?> RegisterAsync(RegisterDto dto);
        Task<AuthResponseDto?> VerifyOtpAsync(string email, string otp);
        Task<string?> ForgotPasswordAsync(string email);
        Task<bool> ResetPasswordAsync(string email, string token, string newPassword);
        Task<AuthResponseDto?> GoogleLoginAsync(string email, string fullName, string providerKey, string idToken);
    }

    public interface ICartApiService
    {
        Task<CartSummaryDto?> GetCartAsync();
        Task<ApiResponse<string>> AddToCartAsync(int productId, int quantity);
        Task<bool> UpdateQuantityAsync(int cartItemId, int quantity);
        Task<bool> RemoveItemAsync(int cartItemId);
        Task<bool> ClearCartAsync();
    }

    public interface IOrderApiService
    {
        Task<OrderDto?> CreateOrderAsync(CreateOrderDto dto);
        Task<List<OrderDto>?> GetMyOrdersAsync();
        Task<OrderDto?> GetOrderByIdAsync(int id);
        Task<bool> CancelOrderAsync(int id);
    }

    public interface IPaymentApiService
    {
        Task<string?> CreatePaymentUrlAsync(PaymentRequestDto dto);
        Task<List<PaymentDto>?> GetPaymentsByOrderAsync(int orderId);
    }

    public interface IReviewApiService
    {
        Task<List<ReviewDto>?> GetByProductAsync(int productId);
        Task<bool> CreateReviewAsync(CreateReviewDto dto);
    }

    public interface IUserProfileApiService
    {
        Task<UserProfileDto?> GetProfileAsync();
        Task<bool> UpdateProfileAsync(UpdateProfileDto dto);
        Task<bool> ChangePasswordAsync(ChangePasswordDto dto);
        Task<bool> UploadAvatarAsync(Microsoft.AspNetCore.Http.IFormFile file);
    }

    // Admin
    public interface IDashboardApiService
    {
        Task<DashboardDto?> GetDashboardAsync();
        Task<List<OrderDto>?> GetAllOrdersAsync();
        Task<bool> UpdateOrderStatusAsync(int orderId, string status);
        Task<PagedResultDto<ProductDto>?> GetAllProductsAsync(int page, int pageSize);
    }

    public interface IUserApiService
    {
        Task<List<UserDto>?> GetAllUsersAsync();
        Task<UserDto?> GetUserByIdAsync(string id);
        Task<bool> LockUserAsync(string id);
        Task<bool> UnlockUserAsync(string id);
        Task<bool> DeleteUserAsync(string id);
    }
}
