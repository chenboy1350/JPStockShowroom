using JPStockShowRoom.Models;

namespace JPStockShowRoom.Services.Interface
{
    public interface IStockQueryService
    {
        Task<PagedResult<StockItemModel>> GetStockListAsync(string? article, string? edesArt = null, string? unit = null, int? registrationStatus = null, int page = 1, int pageSize = 20);
        Task<List<StockItemModel>> GetReportStockListAsync(string? article, string? edesArt = null, string? unit = null);
        Task<List<string>> GetArticleListAsync();
        Task<List<string>> GetProductTypesAsync();
        Task SyncArticlesAsync();
    }
}
