using JPStockShowRoom.Data.JPDbContext;
using JPStockShowRoom.Data.SPDbContext;
using JPStockShowRoom.Data.SWDbContext;
using JPStockShowRoom.Data.SWDbContext.Entities;
using JPStockShowRoom.Models;
using JPStockShowRoom.Services.Interface;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace JPStockShowRoom.Services.Implement
{
    public class StockManagementService(SWDbContext sWDbContext, SPDbContext sPDbContext, JPDbContext jPDbContext) : IStockManagementService
    {
        private readonly SWDbContext _sWDbContext = sWDbContext;
        private readonly SPDbContext _sPDbContext = sPDbContext;
        private readonly JPDbContext _jPDbContext = jPDbContext;

        public async Task<List<StockItemModel>> GetStockListAsync(string? article, string? edesArt = null, string? unit = null)
        {
            var query = _sWDbContext.Stock.Where(s => s.IsActive).AsQueryable();

            if (!string.IsNullOrWhiteSpace(article))
                query = query.Where(r => (r.Article != null && r.Article.Contains(article)) || (r.TempArticle != null && r.TempArticle.Contains(article)));

            if (edesArt != null)
                query = query.Where(r => r.EdesArt != null && (
                    r.EdesArt == edesArt ||
                    r.EdesArt.StartsWith(edesArt + " ") ||
                    r.EdesArt.EndsWith(" " + edesArt) ||
                    r.EdesArt.Contains(" " + edesArt + " ")
                ));

            if (unit != null)
                query = query.Where(r => r.Unit == unit);

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

            var withdrawnStockIds = await _sWDbContext.Withdrawal
                .Select(w => w.StockId)
                .Distinct()
                .ToListAsync();

            // TrayItems for these stocks that have active borrows
            var trayItemsForStocks = await _sWDbContext.TrayItem
                .Where(ti => ti.IsActive && stockIds.Contains(ti.StockId))
                .Select(ti => new { ti.TrayItemId, ti.StockId })
                .ToListAsync();

            var trayItemIdList = trayItemsForStocks.Select(x => x.TrayItemId).ToList();
            var borrowedTrayItemIds = await _sWDbContext.TrayBorrow
                .Where(b => !b.IsReturned && trayItemIdList.Contains(b.TrayItemId))
                .Select(b => b.TrayItemId)
                .Distinct()
                .ToListAsync();

            var borrowedTrayItemSet = borrowedTrayItemIds.ToHashSet();
            var stockBorrowCounts = trayItemsForStocks
                .Where(x => borrowedTrayItemSet.Contains(x.TrayItemId))
                .GroupBy(x => x.StockId)
                .ToDictionary(g => g.Key, g => g.Count());

            var list = stocks.Select(s =>
            {
                var inTrayQty = inTrayQtys.GetValueOrDefault(s.StockId);
                var trayNos = stockToTrayNos.GetValueOrDefault(s.StockId) ?? string.Empty;

                return new StockItemModel
                {
                    ReceivedId = s.StockId,
                    ReceiveNo = s.ReceiveNo,
                    LotNo = s.LotNo,
                    Barcode = s.Barcode,
                    Article = s.Article ?? string.Empty,
                    TempArticle = s.TempArticle,
                    OrderNo = s.OrderNo,
                    CustCode = s.CustCode,
                    ListGem = s.ListGem ?? string.Empty,
                    TtQty = s.TtQty,
                    AvailableQty = s.TtQty - inTrayQty,
                    TtWg = s.TtWg,
                    EDesFn = s.EdesFn ?? string.Empty,
                    IsInTray = inTrayQty > 0,
                    TrayNo = trayNos,
                    TrayId = 0,
                    IsWithdrawn = withdrawnStockIds.Contains(s.StockId),
                    BorrowCount = stockBorrowCounts.GetValueOrDefault(s.StockId, 0),
                    CreateDate = s.CreateDate?.ToString("dd MMMM yyyy", new CultureInfo("th-TH")) ?? string.Empty,
                    FileName = (s.ImgPath ?? "").Split("\\", StringSplitOptions.None).LastOrDefault() ?? string.Empty,
                    EDesArt = s.EdesArt ?? string.Empty,
                    Unit = s.Unit ?? string.Empty
                };
            }).ToList();

            return list;
        }

        public async Task<List<string>> GetArticleListAsync()
        {
            var articles = await _sWDbContext.Stock
                .Where(s => s.IsActive && s.Article != null && s.Article != "")
                .Select(s => s.Article!)
                .Distinct()
                .OrderBy(a => a)
                .ToListAsync();

            return articles;
        }

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

            var trayItemIds = activeTrayItems.Select(ti => ti.TrayItemId).ToList();
            var borrowCounts = await _sWDbContext.TrayBorrow
                .Where(b => !b.IsReturned && trayItemIds.Contains(b.TrayItemId))
                .GroupBy(b => _sWDbContext.TrayItem.Where(ti => ti.TrayItemId == b.TrayItemId).Select(ti => ti.TrayId).FirstOrDefault())
                .Select(g => new { TrayId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.TrayId, x => x.Count);

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

            var stockIds = items.Select(ti => ti.StockId).Distinct().ToList();
            var stocks = await _sWDbContext.Stock
                .Where(s => stockIds.Contains(s.StockId))
                .ToListAsync();
            var stockDict = stocks.ToDictionary(s => s.StockId);

            var trayItemIds = items.Select(ti => ti.TrayItemId).ToList();
            var borrowedQtys = await _sWDbContext.TrayBorrow
                .Where(b => !b.IsReturned && trayItemIds.Contains(b.TrayItemId))
                .GroupBy(b => b.TrayItemId)
                .Select(g => new { TrayItemId = g.Key, Qty = g.Sum(b => b.BorrowQty) })
                .ToDictionaryAsync(x => x.TrayItemId, x => x.Qty);

            var result = items.Select(ti =>
            {
                stockDict.TryGetValue(ti.StockId, out var stock);
                var borrowedQty = borrowedQtys.GetValueOrDefault(ti.TrayItemId);

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
                    IsBorrowed = borrowedQty > 0,
                    BorrowedQty = borrowedQty,
                    CreatedDate = ti.CreateDate?.ToString("dd MMMM yyyy", new CultureInfo("th-TH")) ?? ""
                };
            }).ToList();

            return result;
        }

        public async Task<List<StockItemModel>> GetReceivedForTrayAsync(int trayId, string? article)
        {
            var query = _sWDbContext.Stock
                .Where(s => s.IsActive && s.Article != null && !s.Article.StartsWith("Z"))
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

            var stockToTrayNosRaw2 = await _sWDbContext.TrayItem
                .Where(ti => ti.IsActive && stockIds.Contains(ti.StockId))
                .Join(_sWDbContext.Tray.Where(t => t.IsActive), ti => ti.TrayId, t => t.TrayId, (ti, t) => new { ti.StockId, t.TrayNo })
                .ToListAsync();
            var stockToTrayNos = stockToTrayNosRaw2
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
                        CreateDate = s.CreateDate?.ToString("dd MMMM yyyy", new CultureInfo("th-TH")) ?? ""
                    });
                }
            }

            return list;
        }

        public async Task AddToTrayAsync(int trayId, Dictionary<int, decimal> items, int userId)
        {
            var tray = await _sWDbContext.Tray.FirstOrDefaultAsync(t => t.TrayId == trayId && t.IsActive);
            if (tray == null) return;

            var now = DateTime.Now;
            var stockIds = items.Keys.ToList();

            var stocks = await _sWDbContext.Stock
                .Where(s => stockIds.Contains(s.StockId) && s.IsActive)
                .ToListAsync();

            var inTrayQtys = await _sWDbContext.TrayItem
                .Where(ti => ti.IsActive && stockIds.Contains(ti.StockId))
                .GroupBy(ti => ti.StockId)
                .Select(g => new { StockId = g.Key, Qty = g.Sum(ti => ti.Qty) })
                .ToDictionaryAsync(x => x.StockId, x => x.Qty);

            foreach (var item in items)
            {
                var stockId = item.Key;
                var qtyToAdd = item.Value;

                var stock = stocks.FirstOrDefault(s => s.StockId == stockId);
                if (stock == null) continue;

                var inTrayQty = inTrayQtys.GetValueOrDefault(stockId);
                var availableQty = stock.TtQty - inTrayQty;

                if (qtyToAdd > availableQty) qtyToAdd = availableQty;
                if (qtyToAdd <= 0) continue;

                double wgToAdd = stock.TtQty > 0
                    ? (double)(qtyToAdd / stock.TtQty) * stock.TtWg
                    : 0;

                _sWDbContext.TrayItem.Add(new TrayItem
                {
                    TrayId = trayId,
                    StockId = stockId,
                    Qty = qtyToAdd,
                    Wg = wgToAdd,
                    IsActive = true,
                    CreatedBy = userId,
                    CreateDate = now,
                    UpdateDate = now,
                    UpdatedBy = userId
                });
            }

            tray.UpdateDate = now;
            tray.UpdatedBy = userId;

            await _sWDbContext.SaveChangesAsync();
        }

        public async Task RemoveFromTrayAsync(List<int> trayItemIds, int userId)
        {
            var now = DateTime.Now;
            var items = await _sWDbContext.TrayItem
                .Where(ti => trayItemIds.Contains(ti.TrayItemId) && ti.IsActive)
                .ToListAsync();

            var hasBorrowSet = await _sWDbContext.TrayBorrow
                .Where(b => !b.IsReturned && trayItemIds.Contains(b.TrayItemId))
                .Select(b => b.TrayItemId)
                .ToHashSetAsync();

            var trayIds = items.Select(ti => ti.TrayId).Distinct().ToList();
            var trays = await _sWDbContext.Tray
                .Where(t => trayIds.Contains(t.TrayId))
                .ToListAsync();

            foreach (var item in items)
            {
                if (hasBorrowSet.Contains(item.TrayItemId)) continue;

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

        public async Task BorrowFromTrayAsync(int trayItemId, decimal borrowQty, int borrowedBy)
        {
            var item = await _sWDbContext.TrayItem
                .FirstOrDefaultAsync(ti => ti.TrayItemId == trayItemId && ti.IsActive);
            if (item == null) return;

            var currentBorrowed = await _sWDbContext.TrayBorrow
                .Where(b => b.TrayItemId == trayItemId && !b.IsReturned)
                .SumAsync(b => b.BorrowQty);

            var availableQty = item.Qty - currentBorrowed;
            if (borrowQty <= 0 || borrowQty > availableQty) return;

            double borrowWg = item.Qty > 0
                ? (double)(borrowQty / item.Qty) * item.Wg
                : 0;

            var now = DateTime.Now;

            _sWDbContext.TrayBorrow.Add(new TrayBorrow
            {
                TrayItemId = trayItemId,
                BorrowQty = borrowQty,
                BorrowWg = borrowWg,
                BorrowedBy = borrowedBy,
                IsReturned = false,
                BorrowDate = now,
                UpdateDate = now,
                UpdatedBy = borrowedBy
            });

            await _sWDbContext.SaveChangesAsync();
        }

        public async Task ReturnToTrayAsync(int trayBorrowId, int userId)
        {
            var borrow = await _sWDbContext.TrayBorrow
                .FirstOrDefaultAsync(b => b.TrayBorrowId == trayBorrowId && !b.IsReturned);
            if (borrow == null) return;

            var now = DateTime.Now;
            borrow.IsReturned = true;
            borrow.ReturnDate = now;
            borrow.UpdateDate = now;
            borrow.UpdatedBy = userId;

            await _sWDbContext.SaveChangesAsync();
        }

        public async Task<List<TrayBorrowModel>> GetBorrowListAsync(int? trayId)
        {
            var query = _sWDbContext.TrayBorrow.Where(b => !b.IsReturned).AsQueryable();

            if (trayId.HasValue)
            {
                var trayItemIds = await _sWDbContext.TrayItem
                    .Where(ti => ti.TrayId == trayId.Value && ti.IsActive)
                    .Select(ti => ti.TrayItemId)
                    .ToListAsync();

                query = query.Where(b => trayItemIds.Contains(b.TrayItemId));
            }

            var borrows = await query.ToListAsync();
            var trayItemIdList = borrows.Select(b => b.TrayItemId).Distinct().ToList();

            var trayItems = await _sWDbContext.TrayItem
                .Where(ti => trayItemIdList.Contains(ti.TrayItemId))
                .ToListAsync();

            var trayIdList = trayItems.Select(ti => ti.TrayId).Distinct().ToList();
            var trays = await _sWDbContext.Tray
                .Where(t => trayIdList.Contains(t.TrayId))
                .ToDictionaryAsync(t => t.TrayId);

            var stockIdList = trayItems.Select(ti => ti.StockId).Distinct().ToList();
            var stocks = await _sWDbContext.Stock
                .Where(s => stockIdList.Contains(s.StockId))
                .ToDictionaryAsync(s => s.StockId);

            var trayItemDict = trayItems.ToDictionary(ti => ti.TrayItemId);

            var result = borrows.Select(b =>
            {
                trayItemDict.TryGetValue(b.TrayItemId, out var trayItem);
                var tray = trayItem != null && trays.TryGetValue(trayItem.TrayId, out var t) ? t : null;
                var stock = trayItem != null && stocks.TryGetValue(trayItem.StockId, out var s) ? s : null;

                return new TrayBorrowModel
                {
                    TrayBorrowId = b.TrayBorrowId,
                    TrayItemId = b.TrayItemId,
                    TrayNo = tray?.TrayNo ?? "",
                    LotNo = stock?.LotNo ?? "",
                    Barcode = stock?.Barcode ?? "",
                    Article = stock?.Article ?? stock?.TempArticle ?? "",
                    EDesFn = stock?.EdesFn,
                    ListGem = stock?.ListGem,
                    ImgPath = (stock?.ImgPath ?? "").Split("\\", StringSplitOptions.None).LastOrDefault(),
                    BorrowQty = b.BorrowQty,
                    BorrowWg = b.BorrowWg,
                    BorrowedBy = b.BorrowedBy,
                    BorrowedDate = b.BorrowDate?.ToString("dd MMMM yyyy", new CultureInfo("th-TH")) ?? "",
                    ReturnedDate = b.ReturnDate?.ToString("dd MMMM yyyy", new CultureInfo("th-TH")),
                    IsReturned = b.IsReturned
                };
            }).ToList();

            return result;
        }

        public async Task WithdrawFromStockAsync(int receivedId, decimal withdrawQty, string? remark, int userId)
        {
            var stock = await _sWDbContext.Stock.FirstOrDefaultAsync(s => s.StockId == receivedId && s.IsActive);
            if (stock == null) return;

            var inTrayQty = await _sWDbContext.TrayItem
                .Where(ti => ti.StockId == receivedId && ti.IsActive)
                .SumAsync(ti => ti.Qty);

            var availableQty = stock.TtQty - inTrayQty;
            if (withdrawQty <= 0 || withdrawQty > availableQty) return;

            double withdrawWg = stock.TtQty > 0
                ? (double)(withdrawQty / stock.TtQty) * stock.TtWg
                : 0;

            var now = DateTime.Now;

            stock.TtQty -= withdrawQty;
            stock.TtWg -= withdrawWg;

            if (stock.TtQty <= 0)
            {
                stock.IsActive = false;
                stock.TtQty = 0;
                stock.TtWg = 0;
            }

            stock.UpdateDate = now;

            _sWDbContext.Withdrawal.Add(new Withdrawal
            {
                StockId = receivedId,
                Qty = withdrawQty,
                Wg = withdrawWg,
                Remark = remark,
                WithdrawnBy = userId,
                CreateDate = now,
                UpdateDate = now,
                UpdatedBy = userId
            });

            await _sWDbContext.SaveChangesAsync();
        }

        public async Task<List<WithdrawalModel>> GetWithdrawalListAsync()
        {
            var withdrawals = await _sWDbContext.Withdrawal
                .OrderByDescending(w => w.CreateDate)
                .ToListAsync();

            var stockIds = withdrawals.Select(w => w.StockId).Distinct().ToList();
            var stocks = await _sWDbContext.Stock
                .Where(s => stockIds.Contains(s.StockId))
                .ToDictionaryAsync(s => s.StockId);

            var result = withdrawals.Select(w =>
            {
                stocks.TryGetValue(w.StockId, out var stock);

                return new WithdrawalModel
                {
                    WithdrawalId = w.WithdrawalId,
                    ReceivedId = w.StockId,
                    LotNo = stock?.LotNo ?? "",
                    Barcode = stock?.Barcode ?? "",
                    Article = stock?.Article ?? stock?.TempArticle ?? "",
                    OrderNo = stock?.OrderNo ?? "",
                    CustCode = stock?.CustCode ?? "",
                    ListNo = "",
                    Qty = w.Qty,
                    Wg = w.Wg,
                    Remark = w.Remark,
                    WithdrawnBy = w.WithdrawnBy,
                    WithdrawnDate = w.CreateDate?.ToString("dd MMMM yyyy", new CultureInfo("th-TH")) ?? ""
                };
            }).ToList();

            return result;
        }

        public async Task SyncArticlesAsync()
        {
            var lots = from a in _sWDbContext.Stock
                      where a.TempArticle != null && a.Article == null
                      select a.LotNo;

            var lotNos = await lots.Distinct().ToListAsync();

            foreach (var lotNo in lotNos)
            {
                await UpdateArticleAsync(lotNo);
            }
        }

        private async Task UpdateArticleAsync(string lotNo)
        {
            if (string.IsNullOrWhiteSpace(lotNo))
                return;

            var baseData = await (
                from a in _jPDbContext.OrdLotno.AsNoTracking()
                join c in _jPDbContext.CpriceSale.AsNoTracking()
                    on a.Barcode equals c.Barcode into bc
                from c in bc.DefaultIfEmpty()
                where a.LotNo == lotNo
                      && !string.IsNullOrEmpty(a.LotNo)
                orderby a.ListNo
                select new
                {
                    a.LotNo,
                    Article = c.Article ?? string.Empty,
                }
            ).ToListAsync();

            if (baseData.Count == 0) return;

            var spLots = await _sWDbContext.Stock
                .Where(x => x.LotNo == lotNo)
                .ToListAsync();

            var spLotDict = spLots.ToDictionary(x => x.LotNo);

            foreach (var item in baseData)
            {
                if (!spLotDict.TryGetValue(item.LotNo, out var spLot)) continue;

                if (spLot.Article != item.Article)
                {
                    spLot.Article = item.Article;
                }
            }

            await _sWDbContext.SaveChangesAsync();
        }
    }
}
