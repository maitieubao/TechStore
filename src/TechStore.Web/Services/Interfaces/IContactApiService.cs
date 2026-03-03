using TechStore.Shared.DTOs;

namespace TechStore.Web.Services.Interfaces
{
    public interface IContactApiService
    {
        Task<bool> SendContactAsync(ContactDto dto);
    }
}
