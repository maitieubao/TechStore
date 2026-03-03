using TechStore.Shared.DTOs;
using TechStore.Web.Services.Interfaces;

namespace TechStore.Web.Services
{
    public class ContactApiService : IContactApiService
    {
        private readonly HttpClient _httpClient;

        public ContactApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<bool> SendContactAsync(ContactDto dto)
        {
            var response = await _httpClient.PostAsJsonAsync("api/contact", dto);
            return response.IsSuccessStatusCode;
        }
    }
}
