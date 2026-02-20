using JPStockShowRoom.Data.JPDbContext;
using JPStockShowRoom.Data.SPDbContext;
using JPStockShowRoom.Data.SWDbContext;
using JPStockShowRoom.Models;
using JPStockShowRoom.Services.Helper;
using JPStockShowRoom.Services.Implement;
using JPStockShowRoom.Services.Interface;
using JPStockShowRoom.Services.Middleware;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

builder.Services.AddDbContext<JPDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("JPDBEntries")));
builder.Services.AddDbContext<SPDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("SPDBEntries")));
builder.Services.AddDbContext<SWDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("SWDBEntries")));

builder.Services.Configure<AppSettingModel>(builder.Configuration.GetSection("AppSettings"));

builder.Services.AddTransient<TokenHandler>();
builder.Services.AddHttpClient("ApiClient").AddHttpMessageHandler<TokenHandler>();

builder.Host.UseSerilog((context, configuration) => configuration.ReadFrom.Configuration(context.Configuration));

builder.Services.AddMemoryCache();
builder.Services.AddScoped<ICacheService, CacheService>();
builder.Services.AddScoped<ICookieAuthService, CookieAuthService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IApiClientService, ApiClientService>();
builder.Services.AddScoped<IPISService, PISService>();
builder.Services.AddScoped<IReceiveManagementService, ReceiveManagementService>();
builder.Services.AddScoped<IPermissionManagement, PermissionManagement>();
builder.Services.AddScoped<IStockManagementService, StockManagementService>();

builder.Services.AddAuthentication("AppCookieAuth")
    .AddCookie("AppCookieAuth", options =>
    {
        options.Cookie.Name = "JPApp.Auth";
        options.LoginPath = "/Auth/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<AutoRefreshTokenMiddleware>();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

