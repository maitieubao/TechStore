using TechStore.Shared.DTOs;
using TechStore.Shared.Responses;
using TechStore.Web.Services.Base;
using TechStore.Web.Services.Interfaces;

namespace TechStore.Web.Services
{
    public class AuthApiService : BaseApiService, IAuthApiService
    {
        public AuthApiService(HttpClient httpClient, IHttpContextAccessor accessor)
            : base(httpClient, accessor) { }

        public async Task<AuthResponseDto?> LoginAsync(string userName, string password)
        {
            var result = await PostAsync<AuthResponseDto>("api/auth/login", new { userName, password });
            
            if (result != null && !result.Success)
            {
                return new AuthResponseDto { IsSuccess = false, Message = result.Message };
            }

            return result?.Data;
        }

        public async Task<AuthResponseDto?> RegisterAsync(RegisterDto dto)
        {
            var result = await PostAsync<AuthResponseDto>("api/auth/register", dto);
            
            if (result != null && !result.Success)
            {
                return new AuthResponseDto { IsSuccess = false, Message = result.Message };
            }

            return result?.Data;
        }

        public async Task<AuthResponseDto?> VerifyOtpAsync(string email, string otp)
        {
            var result = await PostAsync<AuthResponseDto>("api/auth/verify-otp",
                new { email, otp });
            
            if (result != null && !result.Success)
            {
                return new AuthResponseDto { IsSuccess = false, Message = result.Message };
            }

            return result?.Data;
        }

        public async Task<string?> ForgotPasswordAsync(string email)
        {
            // Note: Our API returns ApiResponse<string> with the message/token in Data
            var result = await PostAsync<string>("api/auth/forgot-password", new { email });
            return result?.Success == true ? result.Data : null;
        }

        public async Task<bool> ResetPasswordAsync(string email, string token, string newPassword)
        {
            var result = await PostAsync<bool>("api/auth/reset-password", new { email, token, newPassword });
            return result?.Success ?? false;
        }

        public async Task<AuthResponseDto?> GoogleLoginAsync(string email, string fullName, string providerKey, string idToken)
        {
            var result = await PostAsync<AuthResponseDto>("api/auth/google-login",
                new { email, fullName, providerKey, idToken });

            if (result != null && !result.Success)
                return new AuthResponseDto { IsSuccess = false, Message = result.Message };

            return result?.Data;
        }
    }

    public class CategoryApiService : BaseApiService, ICategoryApiService
    {
        public CategoryApiService(HttpClient httpClient, IHttpContextAccessor accessor)
            : base(httpClient, accessor) { }

        public async Task<List<CategoryDto>?> GetCategoriesAsync()
        {
            var result = await GetAsync<List<CategoryDto>>("api/categories");
            return result?.Data;
        }

        public async Task<CategoryDto?> GetByIdAsync(int id)
        {
            var result = await GetAsync<CategoryDto>($"api/categories/{id}");
            return result?.Data;
        }

        public async Task<bool> CreateCategoryAsync(CreateCategoryDto dto)
        {
            var result = await PostAsync<int>("api/categories", dto);
            return result?.Success ?? false;
        }

        public async Task<bool> UpdateCategoryAsync(int id, CreateCategoryDto dto)
        {
            var result = await PutAsync<bool>($"api/categories/{id}", dto);
            return result?.Success ?? false;
        }

        public async Task<bool> DeleteCategoryAsync(int id)
        {
            var result = await DeleteAsync<bool>($"api/categories/{id}");
            return result?.Success ?? false;
        }
    }

    public class CartApiService : BaseApiService, ICartApiService
    {
        public CartApiService(HttpClient httpClient, IHttpContextAccessor accessor)
            : base(httpClient, accessor) { }

        public async Task<CartSummaryDto?> GetCartAsync()
        {
            var result = await GetAsync<CartSummaryDto>("api/cart");
            return result?.Data;
        }

        public async Task<ApiResponse<string>> AddToCartAsync(int productId, int quantity)
        {
            var result = await PostAsync<string>("api/cart", new { productId, quantity });
            // If result is null (deserialization error?), return generic error
            return result ?? new ApiResponse<string> { Success = false, Message = "Connection Error" };
        }

        public async Task<bool> UpdateQuantityAsync(int cartItemId, int quantity)
        {
            var result = await PutAsync<string>($"api/cart/{cartItemId}", new { quantity });
            return result?.Success ?? false;
        }

        public async Task<bool> RemoveItemAsync(int cartItemId)
        {
            var result = await DeleteAsync<string>($"api/cart/{cartItemId}");
            return result?.Success ?? false;
        }

        public async Task<bool> ClearCartAsync()
        {
            var result = await DeleteAsync<string>("api/cart/clear");
            return result?.Success ?? false;
        }
    }

    public class OrderApiService : BaseApiService, IOrderApiService
    {
        public OrderApiService(HttpClient httpClient, IHttpContextAccessor accessor)
            : base(httpClient, accessor) { }

        public async Task<OrderDto?> CreateOrderAsync(CreateOrderDto dto)
        {
            var result = await PostAsync<OrderDto>("api/orders", dto);
            return result?.Data;
        }

        public async Task<List<OrderDto>?> GetMyOrdersAsync()
        {
            var result = await GetAsync<List<OrderDto>>("api/orders/my-orders");
            return result?.Data;
        }

        public async Task<OrderDto?> GetOrderByIdAsync(int id)
        {
            var result = await GetAsync<OrderDto>($"api/orders/{id}");
            return result?.Data;
        }

        public async Task<bool> CancelOrderAsync(int id)
        {
            var result = await PutAsync<bool>($"api/orders/{id}/cancel", new { });
            return result?.Success ?? false;
        }
    }

    public class PaymentApiService : BaseApiService, IPaymentApiService
    {
        public PaymentApiService(HttpClient httpClient, IHttpContextAccessor accessor)
            : base(httpClient, accessor) { }

        public async Task<string?> CreatePaymentUrlAsync(PaymentRequestDto dto)
        {
            var result = await PostAsync<string>("api/payments/create-payment-url", dto);
            return result?.Data;
        }

        public async Task<List<PaymentDto>?> GetPaymentsByOrderAsync(int orderId)
        {
            var result = await GetAsync<List<PaymentDto>>($"api/payments/order/{orderId}");
            return result?.Data;
        }
    }

    public class ReviewApiService : BaseApiService, IReviewApiService
    {
        public ReviewApiService(HttpClient httpClient, IHttpContextAccessor accessor)
            : base(httpClient, accessor) { }

        public async Task<List<ReviewDto>?> GetByProductAsync(int productId)
        {
            var result = await GetAsync<List<ReviewDto>>($"api/reviews/product/{productId}");
            return result?.Data;
        }

        public async Task<bool> CreateReviewAsync(CreateReviewDto dto)
        {
            var result = await PostAsync<ReviewDto>("api/reviews", dto);
            return result?.Success ?? false;
        }
    }

    public class UserProfileApiService : BaseApiService, IUserProfileApiService
    {
        public UserProfileApiService(HttpClient httpClient, IHttpContextAccessor accessor)
            : base(httpClient, accessor) { }

        public async Task<UserProfileDto?> GetProfileAsync()
        {
            var result = await GetAsync<UserProfileDto>("api/userprofile");
            return result?.Data;
        }

        public async Task<bool> UpdateProfileAsync(UpdateProfileDto dto)
        {
            var result = await PutAsync<string>("api/userprofile", dto);
            return result?.Success ?? false;
        }

        public async Task<bool> ChangePasswordAsync(ChangePasswordDto dto)
        {
            var result = await PutAsync<string>("api/userprofile/change-password", dto);
            return result?.Success ?? false;
        }

        public async Task<bool> UploadAvatarAsync(Microsoft.AspNetCore.Http.IFormFile file)
        {
            using var content = new MultipartFormDataContent();
            var stream = file.OpenReadStream();
            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
            content.Add(fileContent, "file", file.FileName);

            var result = await PostFormAsync<string>("api/userprofile/avatar", content);
            return result?.Success ?? false;
        }
    }

    public class DashboardApiService : BaseApiService, IDashboardApiService
    {
        public DashboardApiService(HttpClient httpClient, IHttpContextAccessor accessor)
            : base(httpClient, accessor) { }

        public async Task<DashboardDto?> GetDashboardAsync()
        {
            var result = await GetAsync<DashboardDto>("api/dashboard");
            return result?.Data;
        }

        public async Task<List<OrderDto>?> GetAllOrdersAsync()
        {
            var result = await GetAsync<List<OrderDto>>("api/orders");
            return result?.Data;
        }

        public async Task<bool> UpdateOrderStatusAsync(int orderId, string status)
        {
            var result = await PutAsync<bool>($"api/orders/{orderId}/status", new { status });
            return result?.Success ?? false;
        }

        public async Task<PagedResultDto<ProductDto>?> GetAllProductsAsync(int page, int pageSize)
        {
            var result = await GetAsync<PagedResultDto<ProductDto>>($"api/products?page={page}&pageSize={pageSize}");
            return result?.Data;
        }
    }

    public class UserApiService : BaseApiService, IUserApiService
    {
        public UserApiService(HttpClient httpClient, IHttpContextAccessor accessor)
            : base(httpClient, accessor) { }

        public async Task<List<UserDto>?> GetAllUsersAsync()
        {
            var result = await GetAsync<List<UserDto>>("api/users");
            return result?.Data;
        }

        public async Task<UserDto?> GetUserByIdAsync(string id)
        {
            var result = await GetAsync<UserDto>($"api/users/{id}");
            return result?.Data;
        }

        public async Task<bool> LockUserAsync(string id)
        {
            var result = await PostAsync<bool>($"api/users/{id}/lock", new { });
            return result?.Success ?? false;
        }

        public async Task<bool> UnlockUserAsync(string id)
        {
            var result = await PostAsync<bool>($"api/users/{id}/unlock", new { });
            return result?.Success ?? false;
        }

        public async Task<bool> DeleteUserAsync(string id)
        {
            var result = await DeleteAsync<bool>($"api/users/{id}");
            return result?.Success ?? false;
        }
    }
}
