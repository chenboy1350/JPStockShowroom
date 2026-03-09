using JPStockShowRoom.Models;

namespace JPStockShowRoom.Services.Interface
{
    public interface IReportService
    {
        byte[] GenerateStockReport(List<StockItemModel> model);
        byte[] GenerateStockNoIMGReport(List<StockItemModel> model);
        byte[] GenerateBreakReport(List<LostAndRepairModel> model);
        byte[] GenerateWithdrawalReport(List<WithdrawalModel> model);
        byte[] GenerateBorrowReport(List<BorrowModel> model);
    }
}
