using JPStockShowRoom.Data.SPDbContext.Entities;
using JPStockShowRoom.Models;

namespace JPStockShowRoom.Services.Interface
{
    public interface IReceiveManagementService
    {
        Task<List<ReceivedListModel>> GetTopJPReceivedAsync(string? receiveNo, string? orderNo, string? lotNo);
        Task<List<ReceivedListModel>> GetTopSPReceivedAsync(string? receiveNo, string? lotNo);
        Task<List<ReceivedListModel>> GetJPReceivedByReceiveNoAsync(string receiveNo, string? orderNo, string? lotNo);
        Task<List<ReceivedListModel>> GetSPReceivedByReceiveNoAsync(string receiveNo, string? lotNo);
        Task UpdateLotItemsAsync(string receiveNo, List<string> orderNos, List<int> receiveIds, int userId);
        Task UpdateSPLotItemsAsync(string receiveNo, List<int> receiveIds, int userId);
        Task UpdateJPReceiveHeaderStatusAsync(string receiveNo);
        Task UpdateSPReceiveHeaderStatusAsync(string receiveNo);
        Task<List<ConvertedArticleModel>> ConvertZArticlesAsync();
        Task CancelLotItemsAsync(string receiveNo, List<string> orderNos, List<int> receiveIds, int userId);
        Task CancelSPLotItemsAsync(string receiveNo, List<int> receiveIds, int userId);
        Task SyncAllReceiveHeaderStatusAsync();
    }
}

