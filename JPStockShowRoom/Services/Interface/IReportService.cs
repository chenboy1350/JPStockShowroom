using JPStockShowRoom.Models;

namespace JPStockShowRoom.Services.Interface
{
    public interface IReportService
    {
        byte[] GenerateStockReport(List<StockItemModel> model);
        byte[] GenerateBreakReport(List<LostAndRepairModel> model);
    }
}
