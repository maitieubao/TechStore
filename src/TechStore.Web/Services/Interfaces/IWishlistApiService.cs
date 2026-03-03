using TechStore.Shared.DTOs;

namespace TechStore.Web.Services.Interfaces
{
    public interface IWishlistApiService
    {
        Task<List<WishlistItemDto>?> GetWishlistAsync();
        Task<bool> AddToWishlistAsync(int productId);
        Task<bool> RemoveFromWishlistAsync(int productId);
        Task<bool> IsInWishlistAsync(int productId);
    }
}
