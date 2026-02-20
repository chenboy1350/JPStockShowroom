using JPStockShowRoom.Data.SWDbContext;
using JPStockShowRoom.Services.Interface;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace JPStockShowRoom.Services.Implement
{
    public class CookieAuthService(SWDbContext sWDbContext) : ICookieAuthService
    {
        private readonly SWDbContext _db = sWDbContext;

        public async Task SignInAsync(HttpContext context, int id, string username, bool rememberMe)
        {
            var permissions = await (from mp in _db.MappingPermission
                                     join p in _db.Permission on mp.PermissionId equals p.PermissionId
                                     where mp.UserId == id && mp.IsActive && p.IsActive
                                     select new { p.Name, p.IsMenu }).ToListAsync();

            var claims = new List<Claim>
            {
                new (ClaimTypes.NameIdentifier, id.ToString()),
                new (ClaimTypes.Name, username),
            };

            foreach (var p in permissions)
            {
                claims.Add(new Claim("Permission", p.Name));

                if (p.IsMenu)
                {
                    claims.Add(new Claim("Menu", p.Name));
                }
            }

            var identity = new ClaimsIdentity(claims, "AppCookieAuth");
            var principal = new ClaimsPrincipal(identity);

            var authProps = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe
                    ? DateTimeOffset.UtcNow.AddDays(7)
                    : DateTimeOffset.UtcNow.AddHours(10)
            };

            await context.SignInAsync("AppCookieAuth", principal, authProps);
        }

        public async Task SignOutAsync(HttpContext context)
        {
            await context.SignOutAsync("AppCookieAuth");
        }
    }
}

