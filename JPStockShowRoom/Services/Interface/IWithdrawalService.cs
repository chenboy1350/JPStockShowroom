using JPStockShowRoom.Models;

namespace JPStockShowRoom.Services.Interface
{
    public interface IWithdrawalService
    {
        Task WithdrawFromStockAsync(string groupKey, decimal withdrawQty, string? remark, int userId, bool isAdminAdded = false);
        Task<List<WithdrawalModel>> GetWithdrawalListAsync();
        Task<List<WithdrawalHeaderModel>> GetWithdrawalHeadersAsync(string? article = null, string? edesArt = null, string? withdrawalNo = null);
        Task<List<WithdrawalModel>> GetWithdrawalDetailsByNoAsync(string withdrawalNo);
        Task<List<WithdrawalModel>> GetPendingWithdrawalDetailsAsync(string? article = null, string? edesArt = null);
        Task<string> CreateWithdrawalDocumentAsync(int[] detailIds, int userId);
        Task CancelPendingWithdrawalAsync(int withdrawalDetailId, int userId);
    }
}
