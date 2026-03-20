using JPStockShowRoom.Data.SPDbContext.Entities;
using JPStockShowRoom.Models;

namespace JPStockShowRoom.Services.Interface
{
    public interface IBreakService
    {
        Task<List<LostAndRepairModel>> GetBreakAsync(BreakAndLostFilterModel filter);
        Task<BaseResponseModel> AddBreakAsync(string groupKey, double breakQty, int breakDes);
        Task<List<BreakDescription>> GetBreakDescriptionsAsync();
        Task<List<BreakDescription>> AddNewBreakDescription(string breakDescription);
        Task PintedBreakReport(int[]? breakIDs);
        Task<List<BreakHeaderModel>> GetBreakHeadersAsync(string? article = null, string? edesArt = null, string? breakNo = null);
        Task<List<LostAndRepairModel>> GetBreakDetailsByNoAsync(string breakNo);
        Task<List<LostAndRepairModel>> GetPendingBreakDetailsAsync(string? article = null, string? edesArt = null);
        Task<string> CreateBreakDocumentAsync(int[] detailIds, int userId);
    }
}
