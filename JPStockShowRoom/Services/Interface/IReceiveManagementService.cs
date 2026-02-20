using JPStockShowRoom.Data.SPDbContext.Entities;
using JPStockShowRoom.Models;

namespace JPStockShowRoom.Services.Interface
{
    public interface IReceiveManagementService
    {
        Task<List<ReceivedListModel>> GetTopJPReceivedAsync(string? receiveNo, string? orderNo, string? lotNo);
        Task<List<ReceivedListModel>> GetJPReceivedByReceiveNoAsync(string receiveNo, string? orderNo, string? lotNo);
        Task UpdateLotItemsAsync(string receiveNo, List<string> orderNos, List<int> receiveIds, int userId);
    }
}

