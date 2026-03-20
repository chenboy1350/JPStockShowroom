using JPStockShowRoom.Data.SWDbContext;
using JPStockShowRoom.Data.SWDbContext.Entities;
using JPStockShowRoom.Models;
using JPStockShowRoom.Services.Helper;
using JPStockShowRoom.Services.Interface;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace JPStockShowRoom.Services.Implement
{
    public class TrayService(SWDbContext sWDbContext, StockGroupKeyHelper groupKeyHelper) : ITrayService
    {
        private readonly SWDbContext _sWDbContext = sWDbContext;
        private readonly StockGroupKeyHelper _groupKeyHelper = groupKeyHelper;

        public async Task<List<TrayModel>> GetTrayListAsync(string? article)
        {
            var trays = await _sWDbContext.Tray.Where(t => t.IsActive).ToListAsync();
            var trayIds = trays.Select(t => t.TrayId).ToList();

            var activeTrayItems = await _sWDbContext.TrayItem
                .Where(ti => ti.IsActive && trayIds.Contains(ti.TrayId))
                .ToListAsync();

            var relevantStockIds = activeTrayItems.Select(ti => ti.StockId).Distinct().ToList();
            var stocks = await _sWDbContext.Stock
                .Where(s => relevantStockIds.Contains(s.StockId))
                .Select(s => new { s.StockId, Summary = s.Article ?? s.TempArticle ?? "" })
                .ToListAsync();

            var stockDict = stocks.ToDictionary(s => s.StockId, s => s.Summary);

            var trayItemStockPairs = activeTrayItems.Select(ti => new { ti.TrayId, ti.StockId }).ToList();
            var trayStockIds = trayItemStockPairs.Select(x => x.StockId).Distinct().ToList();
            var borrowedStockIds = await _sWDbContext.BorrowDetail
                .Where(b => !b.IsReturned && trayStockIds.Contains(b.StockId))
                .Select(b => b.StockId)
                .Distinct()
                .ToListAsync();
            var borrowedStockSet = borrowedStockIds.ToHashSet();
            var borrowCounts = trayItemStockPairs
                .Where(x => borrowedStockSet.Contains(x.StockId))
                .GroupBy(x => x.TrayId)
                .ToDictionary(g => g.Key, g => g.Count());

            var result = new List<TrayModel>();

            foreach (var tray in trays)
            {
                var items = activeTrayItems.Where(ti => ti.TrayId == tray.TrayId).ToList();

                var articleNames = items
                    .Select(ti => stockDict.TryGetValue(ti.StockId, out var summary) ? summary : "")
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Distinct()
                    .OrderBy(a => a)
                    .ToList();

                var totalQty = items.Sum(ti => ti.Qty);
                var totalWg = items.Sum(ti => ti.Wg);
                var borrowCount = borrowCounts.GetValueOrDefault(tray.TrayId, 0);

                var model = new TrayModel
                {
                    TrayId = tray.TrayId,
                    TrayNo = tray.TrayNo,
                    Description = tray.Description,
                    ItemCount = items.Count,
                    TotalQty = totalQty,
                    TotalWg = totalWg,
                    BorrowCount = borrowCount,
                    CreatedDate = tray.CreateDate?.ToString("dd MMMM yyyy", new CultureInfo("th-TH")) ?? "",
                    ArticleSummary = string.Join(", ", articleNames),
                    IsActive = tray.IsActive
                };

                if (!string.IsNullOrWhiteSpace(article))
                {
                    if (model.ItemCount == 0 ||
                        tray.TrayNo.Contains(article, StringComparison.OrdinalIgnoreCase) ||
                        model.ArticleSummary.Contains(article, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add(model);
                    }
                }
                else
                {
                    result.Add(model);
                }
            }

            return result;
        }

        public async Task<TrayModel> CreateTrayAsync(string trayNo, string? description, int createdBy)
        {
            var now = DateTime.Now;
            var tray = new Tray
            {
                TrayNo = trayNo,
                Description = description,
                CreatedBy = createdBy,
                UpdatedBy = createdBy,
                CreateDate = now,
                UpdateDate = now,
                IsActive = true
            };

            _sWDbContext.Tray.Add(tray);
            await _sWDbContext.SaveChangesAsync();

            return new TrayModel
            {
                TrayId = tray.TrayId,
                TrayNo = tray.TrayNo,
                Description = tray.Description,
                ItemCount = 0,
                TotalQty = 0,
                TotalWg = 0,
                BorrowCount = 0,
                CreatedDate = tray.CreateDate?.ToString("dd MMMM yyyy", new CultureInfo("th-TH")) ?? "",
                IsActive = true
            };
        }

        public async Task<List<TrayItemModel>> GetTrayItemsAsync(int trayId)
        {
            var tray = await _sWDbContext.Tray.FirstOrDefaultAsync(t => t.TrayId == trayId);
            var items = await _sWDbContext.TrayItem
                .Where(ti => ti.TrayId == trayId && ti.IsActive)
                .ToListAsync();

            var trayItemIds = items.Select(ti => ti.TrayItemId).ToList();
            var borrowedTrayItemIds = await _sWDbContext.BorrowTrayDeduction
                .Where(d => trayItemIds.Contains(d.TrayItemId))
                .Select(d => d.TrayItemId)
                .Distinct()
                .ToListAsync();
            var borrowedSet = borrowedTrayItemIds.ToHashSet();

            var stockIds = items.Select(ti => ti.StockId).Distinct().ToList();
            var stocks = await _sWDbContext.Stock
                .Where(s => stockIds.Contains(s.StockId))
                .ToListAsync();
            var stockDict = stocks.ToDictionary(s => s.StockId);

            return items.Select(ti =>
            {
                stockDict.TryGetValue(ti.StockId, out var stock);
                return new TrayItemModel
                {
                    TrayItemId = ti.TrayItemId,
                    TrayId = ti.TrayId,
                    TrayNo = tray?.TrayNo ?? "",
                    ReceivedId = ti.StockId,
                    LotNo = stock?.LotNo ?? "",
                    Barcode = stock?.Barcode ?? "",
                    Article = stock?.Article ?? stock?.TempArticle ?? "",
                    TempArticle = stock?.TempArticle,
                    OrderNo = stock?.OrderNo ?? "",
                    EDesFn = stock?.EdesFn,
                    ListGem = stock?.ListGem,
                    CustCode = stock?.CustCode ?? "",
                    ImgPath = stock?.ImgPath,
                    ListNo = "",
                    Qty = ti.Qty,
                    Wg = ti.Wg,
                    IsBorrowed = borrowedSet.Contains(ti.TrayItemId),
                    BorrowedQty = 0,
                    CreatedDate = ti.CreateDate?.ToString("dd MMMM yyyy", new CultureInfo("th-TH")) ?? ""
                };
            }).ToList();
        }

        public async Task<List<StockItemModel>> GetReceivedForTrayAsync(int trayId, string? article)
        {
            var query = _sWDbContext.Stock
                .Where(s => s.IsActive && !string.IsNullOrEmpty(s.Article) && !s.Article.StartsWith("Z"))
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(article))
                query = query.Where(r => r.Article != null && r.Article.Contains(article));

            var stocks = await query.OrderByDescending(s => s.CreateDate).ToListAsync();
            var stockIds = stocks.Select(s => s.StockId).ToList();

            var inTrayQtys = await _sWDbContext.TrayItem
                .Where(ti => ti.IsActive && stockIds.Contains(ti.StockId))
                .GroupBy(ti => ti.StockId)
                .Select(g => new { StockId = g.Key, Qty = g.Sum(ti => ti.Qty) })
                .ToDictionaryAsync(x => x.StockId, x => x.Qty);

            var stockToTrayNosRaw = await _sWDbContext.TrayItem
                .Where(ti => ti.IsActive && stockIds.Contains(ti.StockId))
                .Join(_sWDbContext.Tray.Where(t => t.IsActive), ti => ti.TrayId, t => t.TrayId, (ti, t) => new { ti.StockId, t.TrayNo })
                .ToListAsync();
            var stockToTrayNos = stockToTrayNosRaw
                .GroupBy(x => x.StockId)
                .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(x => x.TrayNo).Distinct().OrderBy(n => n)));

            var list = new List<StockItemModel>();
            foreach (var s in stocks)
            {
                var inTrayQty = inTrayQtys.GetValueOrDefault(s.StockId);
                var availableQty = s.TtQty - inTrayQty;

                if (availableQty > 0)
                {
                    list.Add(new StockItemModel
                    {
                        ReceivedId = s.StockId,
                        ReceiveNo = s.ReceiveNo,
                        LotNo = s.LotNo,
                        Barcode = s.Barcode,
                        Article = s.Article ?? "",
                        TempArticle = s.TempArticle,
                        OrderNo = s.OrderNo,
                        CustCode = s.CustCode,
                        ListNo = "",
                        ListGem = s.ListGem ?? "",
                        TtQty = s.TtQty,
                        AvailableQty = availableQty,
                        TtWg = s.TtWg,
                        EDesFn = s.EdesFn ?? "",
                        IsInTray = inTrayQty > 0,
                        TrayNo = stockToTrayNos.GetValueOrDefault(s.StockId) ?? "",
                        FileName = (s.ImgPath ?? "").Split("\\", StringSplitOptions.None).LastOrDefault() ?? "",
                        TrayId = 0,
                        IsWithdrawn = false,
                        CreateDate = s.CreateDate?.ToString("dd-MM-yyyy", new CultureInfo("th-TH")) ?? ""
                    });
                }
            }

            return list;
        }

        public async Task AddToTrayAsync(int trayId, Dictionary<string, decimal> items, int userId)
        {
            var tray = await _sWDbContext.Tray.FirstOrDefaultAsync(t => t.TrayId == trayId && t.IsActive);
            if (tray == null) return;

            var now = DateTime.Now;

            foreach (var (keyStr, requestedQty) in items)
            {
                if (int.TryParse(keyStr, out int directStockId))
                {
                    await AddQtyToTrayForStockAsync(trayId, directStockId, requestedQty, userId, now);
                }
                else
                {
                    var sources = await _groupKeyHelper.ResolveGroupKeyAsync(keyStr);
                    decimal remaining = requestedQty;
                    foreach (var src in sources)
                    {
                        if (remaining <= 0) break;
                        var inTrayQty = await _sWDbContext.TrayItem
                            .Where(ti => ti.IsActive && ti.StockId == src.StockId)
                            .SumAsync(ti => ti.Qty);
                        var available = src.TtQty - inTrayQty;
                        if (available <= 0) continue;
                        var deduct = Math.Min(remaining, available);
                        await AddQtyToTrayForStockAsync(trayId, src.StockId, deduct, userId, now);
                        remaining -= deduct;
                    }
                }
            }

            tray.UpdateDate = now;
            tray.UpdatedBy = userId;
            await _sWDbContext.SaveChangesAsync();
        }

        private async Task AddQtyToTrayForStockAsync(int trayId, int stockId, decimal qty, int userId, DateTime now)
        {
            var stock = await _sWDbContext.Stock.FirstOrDefaultAsync(s => s.StockId == stockId && s.IsActive);
            if (stock == null) return;

            var inTrayQty = await _sWDbContext.TrayItem
                .Where(ti => ti.IsActive && ti.StockId == stockId)
                .SumAsync(ti => ti.Qty);

            var availableQty = stock.TtQty - inTrayQty;
            if (qty > availableQty) qty = availableQty;
            if (qty <= 0) return;

            double wgToAdd = stock.TtQty > 0 ? (double)(qty / stock.TtQty) * stock.TtWg : 0;

            _sWDbContext.TrayItem.Add(new TrayItem
            {
                TrayId = trayId, StockId = stockId, Qty = qty, Wg = wgToAdd,
                IsActive = true, CreatedBy = userId, CreateDate = now, UpdateDate = now, UpdatedBy = userId
            });
        }

        public async Task RemoveFromTrayAsync(List<int> trayItemIds, int userId)
        {
            var now = DateTime.Now;

            var borrowedTrayItemIds = await _sWDbContext.BorrowTrayDeduction
                .Where(d => trayItemIds.Contains(d.TrayItemId))
                .Select(d => d.TrayItemId)
                .Distinct()
                .ToListAsync();
            var safeIds = trayItemIds.Except(borrowedTrayItemIds).ToList();

            var items = await _sWDbContext.TrayItem
                .Where(ti => safeIds.Contains(ti.TrayItemId) && ti.IsActive)
                .ToListAsync();

            var trayIds = items.Select(ti => ti.TrayId).Distinct().ToList();
            var trays = await _sWDbContext.Tray
                .Where(t => trayIds.Contains(t.TrayId))
                .ToListAsync();

            foreach (var item in items)
            {
                item.IsActive = false;
                item.UpdateDate = now;
                item.UpdatedBy = userId;

                var tray = trays.FirstOrDefault(t => t.TrayId == item.TrayId);
                if (tray != null)
                {
                    tray.UpdateDate = now;
                    tray.UpdatedBy = userId;
                }
            }

            await _sWDbContext.SaveChangesAsync();
        }

        public async Task DeleteTrayAsync(int trayId, int userId)
        {
            var tray = await _sWDbContext.Tray.FirstOrDefaultAsync(t => t.TrayId == trayId);
            if (tray == null) return;

            var items = await _sWDbContext.TrayItem
                .Where(ti => ti.TrayId == trayId && ti.IsActive)
                .ToListAsync();

            var now = DateTime.Now;

            foreach (var item in items)
            {
                item.IsActive = false;
                item.UpdateDate = now;
                item.UpdatedBy = userId;
            }

            tray.IsActive = false;
            tray.UpdateDate = now;
            tray.UpdatedBy = userId;

            await _sWDbContext.SaveChangesAsync();
        }
    }
}
