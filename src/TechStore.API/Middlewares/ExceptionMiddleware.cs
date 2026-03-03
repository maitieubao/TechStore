using System.Net;
using System.Text.Json;
using FluentValidation;

namespace TechStore.API.Middlewares
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            var (statusCode, message) = exception switch
            {
                UnauthorizedAccessException => ((int)HttpStatusCode.Unauthorized, "Unauthorized access"),
                KeyNotFoundException => ((int)HttpStatusCode.NotFound, "Resource not found"),
                ArgumentException => ((int)HttpStatusCode.BadRequest, exception.Message),
                ValidationException validationEx => ((int)HttpStatusCode.BadRequest,
                    string.Join("; ", validationEx.Errors.Select(e => e.ErrorMessage))),
                InvalidOperationException => ((int)HttpStatusCode.Conflict, exception.Message),
                NotImplementedException => ((int)HttpStatusCode.NotImplemented, "This feature is not yet implemented"),
                TimeoutException => ((int)HttpStatusCode.RequestTimeout, "The request timed out"),
                _ => ((int)HttpStatusCode.InternalServerError, "An internal server error occurred")
            };

            context.Response.StatusCode = statusCode;

            var response = new
            {
                Success = false,
                Message = message,
                StatusCode = statusCode,
                Timestamp = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(json);
        }
    }
}
