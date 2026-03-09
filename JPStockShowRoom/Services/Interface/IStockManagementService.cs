using JPStockShowRoom.Data.SPDbContext.Entities;
using JPStockShowRoom.Models;

namespace JPStockShowRoom.Services.Interface
{
    public interface IStockManagementService
    {
        Task<List<StockItemModel>> GetStockListAsync(string? article, string? edesArt = null, string? unit = null);
        Task<List<StockItemModel>> GetReportStockListAsync(string? article, string? edesArt = null, string? unit = null);

        Task<List<TrayModel>> GetTrayListAsync(string? article);
        Task<TrayModel> CreateTrayAsync(string trayNo, string? description, int createdBy);
        Task<List<TrayItemModel>> GetTrayItemsAsync(int trayId);
        Task<List<StockItemModel>> GetReceivedForTrayAsync(int trayId, string? article);
        Task AddToTrayAsync(int trayId, Dictionary<int, decimal> items, int userId);
        Task RemoveFromTrayAsync(List<int> trayItemIds, int userId);
        Task DeleteTrayAsync(int trayId, int userId);
        Task BorrowFromStockAsync(int stockId, decimal borrowQty, int borrowedBy);
        Task ReturnBorrowAsync(int borrowDetailId, int userId);
        Task<List<BorrowModel>> GetBorrowListAsync(int? stockId);
        Task<List<string>> GetArticleListAsync();
        Task WithdrawFromStockAsync(int receivedId, decimal withdrawQty, string? remark, int userId);
        Task<List<WithdrawalModel>> GetWithdrawalListAsync();
        Task SyncArticlesAsync();
        Task<List<LostAndRepairModel>> GetBreakAsync(BreakAndLostFilterModel filter);
        Task<BaseResponseModel> AddBreakAsync(int receivedId, double breakQty, int breakDes);
        Task PintedBreakReport(int[]? breakIDs);
        Task<List<BreakDescription>> GetBreakDescriptionsAsync();
        Task<List<BreakDescription>> AddNewBreakDescription(string breakDescription);
        Task<List<string>> GetProductTypesAsync();
        Task<List<WithdrawalHeaderModel>> GetWithdrawalHeadersAsync(string? article = null, string? edesArt = null, string? withdrawalNo = null);
        Task<List<WithdrawalModel>> GetWithdrawalDetailsByNoAsync(string withdrawalNo);
        Task<List<WithdrawalModel>> GetPendingWithdrawalDetailsAsync(string? article = null, string? edesArt = null);
        Task<string> CreateWithdrawalDocumentAsync(int[] detailIds, int userId);
        Task CancelPendingWithdrawalAsync(int withdrawalDetailId, int userId);
        Task<List<BreakHeaderModel>> GetBreakHeadersAsync(string? article = null, string? edesArt = null, string? breakNo = null);
        Task<List<LostAndRepairModel>> GetBreakDetailsByNoAsync(string breakNo);
        Task<List<LostAndRepairModel>> GetPendingBreakDetailsAsync(string? article = null, string? edesArt = null);
        Task<string> CreateBreakDocumentAsync(int[] detailIds, int userId);
        Task<List<BorrowHeaderModel>> GetBorrowHeadersAsync(string? article = null, string? edesArt = null, string? borrowNo = null, bool? isReturned = null);
        Task<List<BorrowModel>> GetBorrowDetailsByNoAsync(string borrowNo);
        Task<List<BorrowModel>> GetPendingBorrowDetailsAsync(string? article = null, string? edesArt = null, bool? isReturned = null);
        Task<string> CreateBorrowDocumentAsync(int[] detailIds, int userId);
        Task CancelPendingBorrowAsync(int borrowDetailId, int userId);
        Task<List<BorrowModel>> GetBorrowsByStockIdAsync(int stockId);
        Task<List<AddStockItemModel>> SearchAddStockItems(string article, string barcode);
    }
}
