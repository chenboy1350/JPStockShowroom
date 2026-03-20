using JPStockShowRoom.Models;

namespace JPStockShowRoom.Services.Interface
{
    public interface ITrayService
    {
        Task<List<TrayModel>> GetTrayListAsync(string? article);
        Task<TrayModel> CreateTrayAsync(string trayNo, string? description, int createdBy);
        Task<List<TrayItemModel>> GetTrayItemsAsync(int trayId);
        Task<List<StockItemModel>> GetReceivedForTrayAsync(int trayId, string? article);
        Task AddToTrayAsync(int trayId, Dictionary<string, decimal> items, int userId);
        Task RemoveFromTrayAsync(List<int> trayItemIds, int userId);
        Task DeleteTrayAsync(int trayId, int userId);
    }
}
