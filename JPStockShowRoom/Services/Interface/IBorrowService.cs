using JPStockShowRoom.Models;

namespace JPStockShowRoom.Services.Interface
{
    public interface IBorrowService
    {
        Task BorrowFromStockAsync(string groupKey, decimal borrowQty, int borrowedBy);
        Task ReturnBorrowAsync(int borrowDetailId, int userId);
        Task<List<BorrowModel>> GetBorrowListAsync(string? groupKey);
        Task<List<BorrowModel>> GetBorrowsByStockIdAsync(string groupKey);
        Task<List<BorrowHeaderModel>> GetBorrowHeadersAsync(string? article = null, string? edesArt = null, string? borrowNo = null, bool? isReturned = null);
        Task<List<BorrowModel>> GetBorrowDetailsByNoAsync(string borrowNo);
        Task<List<BorrowModel>> GetPendingBorrowDetailsAsync(string? article = null, string? edesArt = null, bool? isReturned = null);
        Task<string> CreateBorrowDocumentAsync(int[] detailIds, int userId);
        Task CancelPendingBorrowAsync(int borrowDetailId, int userId);
    }
}
