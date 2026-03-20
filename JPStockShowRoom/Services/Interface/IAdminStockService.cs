using JPStockShowRoom.Models;

namespace JPStockShowRoom.Services.Interface
{
    public interface IAdminStockService
    {
        Task<List<AddStockItemModel>> SearchAddStockItems(string article, string barcode);
        Task AddStockAsync(string barcode, decimal qty, int userId, string? orderNo = null);
        Task<ExcelImportResultModel> ImportStockFromExcelAsync(Stream excelStream, int userId);
        Task DeleteAdminStockAsync(string groupKey);
    }
}
