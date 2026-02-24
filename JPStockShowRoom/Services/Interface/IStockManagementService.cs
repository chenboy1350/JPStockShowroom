using JPStockShowRoom.Data.SPDbContext.Entities;
using JPStockShowRoom.Models;

namespace JPStockShowRoom.Services.Interface
{
    public interface IStockManagementService
    {
        Task<List<StockItemModel>> GetStockListAsync(string? article, string? edesArt = null, string? unit = null);
        Task<List<TrayModel>> GetTrayListAsync(string? article);
        Task<TrayModel> CreateTrayAsync(string trayNo, string? description, int createdBy);
        Task<List<TrayItemModel>> GetTrayItemsAsync(int trayId);
        Task<List<StockItemModel>> GetReceivedForTrayAsync(int trayId, string? article);
        Task AddToTrayAsync(int trayId, Dictionary<int, decimal> items, int userId);
        Task RemoveFromTrayAsync(List<int> trayItemIds, int userId);
        Task DeleteTrayAsync(int trayId, int userId);
        Task BorrowFromTrayAsync(int trayItemId, decimal borrowQty, int borrowedBy);
        Task ReturnToTrayAsync(int trayBorrowId, int userId);
        Task<List<TrayBorrowModel>> GetBorrowListAsync(int? trayId);
        Task<List<string>> GetArticleListAsync();
        Task WithdrawFromStockAsync(int receivedId, decimal withdrawQty, string? remark, int userId);
        Task<List<WithdrawalModel>> GetWithdrawalListAsync();
        Task SyncArticlesAsync();
        Task<List<LostAndRepairModel>> GetBreakAsync(BreakAndLostFilterModel filter);
        Task AddBreakAsync(int receivedId, double breakQty, int breakDes);
        Task PintedBreakReport(int[]? breakIDs);
        Task<List<BreakDescription>> GetBreakDescriptionsAsync();
        Task<List<BreakDescription>> AddNewBreakDescription(string breakDescription);
    }
}
