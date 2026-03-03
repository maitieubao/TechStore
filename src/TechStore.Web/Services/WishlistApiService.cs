using TechStore.Shared.DTOs;
using TechStore.Web.Services.Base;
using TechStore.Web.Services.Interfaces;

namespace TechStore.Web.Services
{
    public class WishlistApiService : BaseApiService, IWishlistApiService
    {
        public WishlistApiService(HttpClient httpClient, IHttpContextAccessor accessor)
            : base(httpClient, accessor) { }

        public async Task<List<WishlistItemDto>?> GetWishlistAsync()
        {
            var result = await GetAsync<List<WishlistItemDto>>("api/wishlist");
            return result?.Data;
        }

        public async Task<bool> AddToWishlistAsync(int productId)
        {
            var result = await PostAsync<string>($"api/wishlist/{productId}", new { });
            return result?.Success ?? false;
        }

        public async Task<bool> RemoveFromWishlistAsync(int productId)
        {
            var result = await DeleteAsync<string>($"api/wishlist/{productId}");
            return result?.Success ?? false;
        }

        public async Task<bool> IsInWishlistAsync(int productId)
        {
            var result = await GetAsync<bool>($"api/wishlist/check/{productId}");
            return result?.Success ?? false && result.Data;
        }
    }
}
