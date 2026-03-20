using JPStockShowRoom.Data.SWDbContext;
using Microsoft.EntityFrameworkCore;

namespace JPStockShowRoom.Services.Helper
{
    public class StockGroupKeyHelper(SWDbContext sWDbContext)
    {
        private readonly SWDbContext _sWDbContext = sWDbContext;

        public static string BuildGroupKey(string? article, string? barcode, string? listGem, string? edesFn)
            => $"{article ?? ""}|{barcode ?? ""}|{listGem ?? ""}|{edesFn ?? ""}";

        public async Task<List<Data.SWDbContext.Entities.Stock>> ResolveGroupKeyAsync(string groupKey, bool? isAdminAdded = null)
        {
            var parts = groupKey.Split('|');
            string article = parts.Length > 0 ? parts[0] : "";
            string barcode  = parts.Length > 1 ? parts[1] : "";
            string listGem  = parts.Length > 2 ? parts[2] : "";
            string edesFn   = parts.Length > 3 ? parts[3] : "";

            var query = _sWDbContext.Stock
                .Where(s => s.IsActive
                    && (s.Article  ?? "") == article
                    && (s.Barcode  ?? "") == barcode
                    && (s.ListGem  ?? "") == listGem
                    && (s.EdesFn   ?? "") == edesFn);

            if (isAdminAdded == true)
                query = query.Where(s => s.ReceiveNo == "ADMIN");
            else if (isAdminAdded == false)
                query = query.Where(s => s.ReceiveNo != "ADMIN");

            return await query.OrderBy(s => s.CreateDate).ToListAsync();
        }

        public async Task<List<int>> ResolveGroupKeyToStockIdsAsync(string groupKey)
        {
            var parts = groupKey.Split('|');
            string article = parts.Length > 0 ? parts[0] : "";
            string barcode  = parts.Length > 1 ? parts[1] : "";
            string listGem  = parts.Length > 2 ? parts[2] : "";
            string edesFn   = parts.Length > 3 ? parts[3] : "";

            return await _sWDbContext.Stock
                .Where(s => (s.Article  ?? "") == article
                         && (s.Barcode  ?? "") == barcode
                         && (s.ListGem  ?? "") == listGem
                         && (s.EdesFn   ?? "") == edesFn)
                .Select(s => s.StockId)
                .ToListAsync();
        }
    }
}
