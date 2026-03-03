using TechStore.Domain.Entities;

namespace TechStore.Application.Common.Interfaces
{
    public interface IOrderRepository : IGenericRepository<Order>
    {
        Task<List<Order>> GetAllWithItemsAsync();
        Task<Order?> GetByIdWithItemsAsync(int id);
        Task<List<Order>> GetByUserIdWithItemsAsync(string userId);
    }
}
