using Microsoft.AspNetCore.Authentication.Cookies;
using TechStore.Web.Services;
using TechStore.Web.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// --- Services ---
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

// API Base URL
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5019";

// Register HttpClient for each API service
void RegisterApiService<TInterface, TImplementation>(string? baseUrl = null)
    where TInterface : class
    where TImplementation : class, TInterface
{
    builder.Services.AddHttpClient<TInterface, TImplementation>(client =>
    {
        client.BaseAddress = new Uri(baseUrl ?? apiBaseUrl);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.Timeout = TimeSpan.FromSeconds(30);
    });
}

RegisterApiService<IProductApiService, ProductApiService>();
RegisterApiService<ICategoryApiService, CategoryApiService>();
RegisterApiService<IAuthApiService, AuthApiService>();
RegisterApiService<ICartApiService, CartApiService>();
RegisterApiService<IOrderApiService, OrderApiService>();
RegisterApiService<IPaymentApiService, PaymentApiService>();
RegisterApiService<IReviewApiService, ReviewApiService>();
RegisterApiService<IDashboardApiService, DashboardApiService>();
RegisterApiService<IWishlistApiService, WishlistApiService>();
RegisterApiService<IUserProfileApiService, UserProfileApiService>();
RegisterApiService<IContactApiService, ContactApiService>();
RegisterApiService<IUserApiService, UserApiService>();

// Cookie Authentication (stores JWT claims in cookie)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/account/login";
        options.LogoutPath = "/account/logout";
        options.AccessDeniedPath = "/account/accessdenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.Cookie.Name = "TechStore_Auth";
        options.Cookie.HttpOnly = true;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// --- Pipeline ---
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Admin area route
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

// SEO-friendly product detail route
app.MapControllerRoute(
    name: "productDetail",
    pattern: "product/{slug}",
    defaults: new { controller = "Product", action = "Detail" });

// Default route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
