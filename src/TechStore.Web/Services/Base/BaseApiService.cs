using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TechStore.Shared.Responses;

namespace TechStore.Web.Services.Base
{
    public abstract class BaseApiService
    {
        protected readonly HttpClient _httpClient;
        protected readonly IHttpContextAccessor _httpContextAccessor;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        protected BaseApiService(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
        {
            _httpClient = httpClient;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Attach JWT token from cookie to outgoing request
        /// </summary>
        protected void AttachToken()
        {
            var token = _httpContextAccessor.HttpContext?.Request.Cookies["TechStore_Token"];
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            }
        }

        /// <summary>
        /// GET with deserialization
        /// </summary>
        protected async Task<ApiResponse<T>?> GetAsync<T>(string url)
        {
            AttachToken();
            var response = await _httpClient.GetAsync(url);
            return await DeserializeResponse<T>(response);
        }

        /// <summary>
        /// POST with JSON body
        /// </summary>
        protected async Task<ApiResponse<T>?> PostAsync<T>(string url, object data)
        {
            AttachToken();
            var json = JsonSerializer.Serialize(data, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);
            return await DeserializeResponse<T>(response);
        }

        /// <summary>
        /// PUT with JSON body
        /// </summary>
        protected async Task<ApiResponse<T>?> PutAsync<T>(string url, object data)
        {
            AttachToken();
            var json = JsonSerializer.Serialize(data, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync(url, content);
            return await DeserializeResponse<T>(response);
        }

        /// <summary>
        /// DELETE
        /// </summary>
        protected async Task<ApiResponse<T>?> DeleteAsync<T>(string url)
        {
            AttachToken();
            var response = await _httpClient.DeleteAsync(url);
            return await DeserializeResponse<T>(response);
        }

        /// <summary>
        /// POST with multipart form data (file upload)
        /// </summary>
        protected async Task<ApiResponse<T>?> PostFormAsync<T>(string url, MultipartFormDataContent formData)
        {
            AttachToken();
            var response = await _httpClient.PostAsync(url, formData);
            return await DeserializeResponse<T>(response);
        }

        private static async Task<ApiResponse<T>?> DeserializeResponse<T>(HttpResponseMessage response)
        {
            var responseBody = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrEmpty(responseBody))
            {
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    return new ApiResponse<T> { Success = false, Message = "Unauthorized" };

                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    return new ApiResponse<T> { Success = false, Message = "Forbidden" };

                return new ApiResponse<T> { Success = false, Message = $"Empty response ({response.StatusCode})" };
            }

            try
            {
                return JsonSerializer.Deserialize<ApiResponse<T>>(responseBody, _jsonOptions);
            }
            catch
            {
                return new ApiResponse<T>
                {
                    Success = false,
                    Message = $"API Error: {response.StatusCode}"
                };
            }
        }
    }
}
