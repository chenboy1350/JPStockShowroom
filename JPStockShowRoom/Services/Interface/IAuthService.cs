using JPStockShowRoom.Models;
using static JPStockShowRoom.Services.Implement.AuthService;

namespace JPStockShowRoom.Services.Interface
{
    public interface IAuthService
    {
        Task<LoginResult> LoginUserAsync(string username, string password, bool rememberMe);
        Task<RefreshTokenResult> RefreshTokenAsync();
        Task<bool> LogoutAsync();
    }
}

