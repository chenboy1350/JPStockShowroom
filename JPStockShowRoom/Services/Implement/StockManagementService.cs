using ClosedXML.Excel;
using JPStockShowRoom.Data.JPDbContext;
using JPStockShowRoom.Data.SPDbContext;
using JPStockShowRoom.Data.SPDbContext.Entities;
using JPStockShowRoom.Data.SWDbContext;
using JPStockShowRoom.Data.SWDbContext.Entities;
using JPStockShowRoom.Models;
using JPStockShowRoom.Services.Interface;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace JPStockShowRoom.Services.Implement
{
    public class StockManagementService(SWDbContext sWDbContext, SPDbContext sPDbContext, JPDbContext jPDbContext, IPISService pISService) : IStockManagementService
    {
        private readonly SWDbContext _sWDbContext = sWDbContext;
        private readonly SPDbContext _sPDbContext = sPDbContext;
        private readonly JPDbContext _jPDbContext = jPDbContext;
        private readonly IPISService _pISService = pISService;


        public async Task<PagedResult<StockItemModel>> GetStockListAsync(string? article, string? edesArt = null, string? unit = null, int? registrationStatus = null, int page = 1, int pageSize = 20)
        {
            var query = _sWDbContext.Stock.AsQueryable();

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

            var withdrawnStockIds = await _sWDbContext.WithdrawalDetail
                .Where(wd => wd.IsActive)
                .Select(wd => wd.StockId)
                .Distinct()
                .ToListAsync();

            var stockBorrowData = await _sWDbContext.BorrowDetail
                .Where(b => !b.IsReturned && stockIds.Contains(b.StockId))
                .GroupBy(b => b.StockId)
                .Select(g => new { StockId = g.Key, Count = g.Count(), TotalQty = g.Sum(b => b.BorrowQty) })
                .ToDictionaryAsync(x => x.StockId, x => new { x.Count, x.TotalQty });

            var receiveNos = stocks.Select(s => s.ReceiveNo).Distinct().ToList();
            var spReceiveNos = await _sPDbContext.SendShowroom
                .Where(s => receiveNos.Contains(s.Doc))
                .Select(s => s.Doc)
                .ToHashSetAsync();

            var withdrawnSet = withdrawnStockIds.ToHashSet();

            var list = stocks
                .GroupBy(s => new { s.Article, s.Barcode, s.ListGem, s.EdesFn })
                .Select(g =>
                {
                    var sources = g.OrderBy(s => s.CreateDate).ToList();
                    var groupKey = BuildGroupKey(g.Key.Article, g.Key.Barcode, g.Key.ListGem, g.Key.EdesFn);

                    decimal totalQty      = sources.Sum(s => s.TtQty);
                    double  totalWg       = sources.Sum(s => s.TtWg);
                    decimal totalInTray   = sources.Sum(s => inTrayQtys.GetValueOrDefault(s.StockId));
                    decimal totalBorrowed = sources.Sum(s => stockBorrowData.TryGetValue(s.StockId, out var bdA) ? bdA.TotalQty : 0);
                    int totalBorrowCount  = sources.Sum(s => stockBorrowData.TryGetValue(s.StockId, out var bdB) ? bdB.Count : 0);

                    var allTrayNos = sources
                        .SelectMany(s => stockToTrayNos.TryGetValue(s.StockId, out var tn) ? tn.Split(", ") : Array.Empty<string>())
                        .Distinct().OrderBy(n => n).ToList();

                    var rep = sources.First();
                    return new StockItemModel
                    {
                        ReceivedId   = 0,
                        GroupKey     = groupKey,
                        Article      = g.Key.Article  ?? string.Empty,
                        TempArticle  = sources.Select(s => s.TempArticle).FirstOrDefault(t => t != null),
                        Barcode      = g.Key.Barcode  ?? string.Empty,
                        ListGem      = g.Key.ListGem  ?? string.Empty,
                        EDesFn       = g.Key.EdesFn   ?? string.Empty,
                        EDesArt      = rep.EdesArt    ?? string.Empty,
                        Unit         = rep.Unit,
                        OrderNo      = rep.OrderNo,
                        CustCode     = rep.CustCode,
                        LotNo        = rep.LotNo,
                        ReceiveNo    = rep.ReceiveNo,
                        TtQty        = totalQty,
                        TtWg         = totalWg,
                        InTrayQty    = totalInTray,
                        AvailableQty = totalQty - totalInTray - totalBorrowed,
                        BorrowedQty  = totalBorrowed,
                        BorrowCount  = totalBorrowCount,
                        IsInTray     = totalInTray > 0,
                        TrayNo       = string.Join(", ", allTrayNos),
                        IsWithdrawn  = sources.Any(s => withdrawnSet.Contains(s.StockId)),
                        IsRepairing  = sources.Any(s => s.IsRepairing),
                        IsActive     = sources.Any(s => s.IsActive && s.TtQty > 0),
                        ImgPath      = rep.ImgPath ?? string.Empty,
                        FileName     = (rep.ImgPath ?? string.Empty).Split("\\", StringSplitOptions.None).LastOrDefault() ?? string.Empty,
                        CreateDate   = rep.CreateDate?.ToString("dd-MM-yyyy", new CultureInfo("th-TH")) ?? string.Empty,
                        IsFromSP     = sources.Any(s => spReceiveNos.Contains(s.ReceiveNo)),
                        IsAdminAdded = sources.All(s => s.ReceiveNo == "ADMIN")
                    };
                }).ToList();

            int totalGeneralCount = list.Count(x => !string.IsNullOrEmpty(x.Article));
            int totalPendingCount = list.Count(x => string.IsNullOrEmpty(x.Article));

            if (registrationStatus.HasValue)
            {
                list = registrationStatus == (int)RegistrationStatus.Pending
                    ? list.Where(x => string.IsNullOrEmpty(x.Article)).ToList()
                    : list.Where(x => !string.IsNullOrEmpty(x.Article)).ToList();
            }

            int totalCount = list.Count;
            var items = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return new PagedResult<StockItemModel>
            {
                Items             = items,
                TotalCount        = totalCount,
                Page              = page,
                PageSize          = pageSize,
                TotalGeneralCount = totalGeneralCount,
                TotalPendingCount = totalPendingCount,
            };
        }

        public async Task<List<StockItemModel>> GetReportStockListAsync(string? article, string? edesArt = null, string? unit = null)
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

            var list = stocks.Select(s =>
            {
                var inTrayQty = inTrayQtys.GetValueOrDefault(s.StockId);
                var trayNos = stockToTrayNos.GetValueOrDefault(s.StockId) ?? string.Empty;

                return new StockItemModel
                {
                    Article = s.Article ?? string.Empty,
                    TempArticle = s.TempArticle?.Trim(),
                    OrderNo = s.OrderNo,
                    ListGem = s.ListGem ?? string.Empty,
                    TtQty = s.TtQty,
                    AvailableQty = s.TtQty - inTrayQty,
                    EDesFn = s.EdesFn ?? string.Empty,
                    IsInTray = inTrayQty > 0,
                    TrayNo = trayNos,
                    IsRepairing = s.IsRepairing,
                    CreateDate = s.CreateDate?.ToString("dd-MM-yyyy", new CultureInfo("th-TH")) ?? string.Empty,
                    ImgPath = s.ImgPath ?? string.Empty,
                    IsActive = s.IsActive,
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
                .ToListAsync();

            var tempArticles = await _sWDbContext.Stock
                .Where(s => s.IsActive && s.TempArticle != null && s.TempArticle != "")
                .Select(s => s.TempArticle!)
                .Distinct()
                .ToListAsync();

            return articles.Union(tempArticles).OrderBy(a => a).ToList();
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

            var result = items.Select(ti =>
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

            return result;
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
                    var sources = await ResolveGroupKeyAsync(keyStr);
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

            // block removal of TrayItems that are currently borrowed
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

        public async Task BorrowFromStockAsync(string groupKey, decimal borrowQty, int borrowedBy)
        {
            if (int.TryParse(groupKey, out int singleStockId))
            {
                await BorrowFromSingleStockAsync(singleStockId, borrowQty, borrowedBy);
                return;
            }

            var sources = await ResolveGroupKeyAsync(groupKey);
            decimal remaining = borrowQty;
            foreach (var src in sources)
            {
                if (remaining <= 0) break;
                var currentBorrowed = await _sWDbContext.BorrowDetail
                    .Where(b => b.StockId == src.StockId && !b.IsReturned)
                    .SumAsync(b => b.BorrowQty);
                var borrowable = src.TtQty - currentBorrowed;
                if (borrowable <= 0) continue;
                var deduct = Math.Min(remaining, borrowable);
                await BorrowFromSingleStockAsync(src.StockId, deduct, borrowedBy);
                remaining -= deduct;
            }
        }

        private async Task BorrowFromSingleStockAsync(int stockId, decimal borrowQty, int borrowedBy)
        {
            var stock = await _sWDbContext.Stock
                .FirstOrDefaultAsync(s => s.StockId == stockId && s.IsActive);
            if (stock == null) return;

            var currentBorrowed = await _sWDbContext.BorrowDetail
                .Where(b => b.StockId == stockId && !b.IsReturned)
                .SumAsync(b => b.BorrowQty);

            // borrowable = ทั้งหมดที่ยังไม่ถูกยืม (รวม inTray)
            var borrowable = stock.TtQty - currentBorrowed;
            if (borrowQty <= 0 || borrowQty > borrowable) return;

            var now = DateTime.Now;

            // freeQty = ที่อยู่นอกถาดและไม่ถูกยืม
            var inTrayQty = await _sWDbContext.TrayItem
                .Where(ti => ti.IsActive && ti.StockId == stockId)
                .SumAsync(ti => ti.Qty);
            var freeQty = stock.TtQty - inTrayQty - currentBorrowed;

            // ถ้ายืมเกิน freeQty ให้หักส่วนที่เกินออกจาก TrayItem (FIFO)
            if (borrowQty > freeQty)
            {
                var excess = borrowQty - freeQty;
                var trayItems = await _sWDbContext.TrayItem
                    .Where(ti => ti.IsActive && ti.StockId == stockId)
                    .OrderBy(ti => ti.TrayItemId)
                    .ToListAsync();

                var remaining = excess;
                var deductions = new List<(int TrayItemId, decimal Qty, double Wg)>();

                foreach (var ti in trayItems)
                {
                    if (remaining <= 0) break;
                    decimal deductedQty;
                    double deductedWg;
                    if (ti.Qty <= remaining)
                    {
                        deductedQty = ti.Qty;
                        deductedWg = ti.Wg;
                        remaining -= ti.Qty;
                        ti.Wg = 0;
                        ti.Qty = 0;
                    }
                    else
                    {
                        deductedQty = remaining;
                        deductedWg = ti.Qty > 0 ? ti.Wg * (double)(remaining / ti.Qty) : 0;
                        ti.Wg -= deductedWg;
                        ti.Qty -= remaining;
                        remaining = 0;
                    }
                    ti.UpdateDate = now;
                    ti.UpdatedBy = borrowedBy;
                    deductions.Add((ti.TrayItemId, deductedQty, deductedWg));
                }

                // บันทึก deductions ไว้สำหรับ restore ตอนคืน
                // ต้อง SaveChanges ก่อนเพื่อให้ BorrowDetail มี ID
                double borrowWgEarly = stock.TtQty > 0
                    ? (double)(borrowQty / stock.TtQty) * stock.TtWg
                    : 0;

                var borrowDetail = new BorrowDetail
                {
                    StockId = stockId,
                    BorrowQty = borrowQty,
                    BorrowWg = borrowWgEarly,
                    BorrowedBy = borrowedBy,
                    IsReturned = false,
                    BorrowDate = now,
                    UpdateDate = now,
                    UpdatedBy = borrowedBy
                };
                _sWDbContext.BorrowDetail.Add(borrowDetail);
                await _sWDbContext.SaveChangesAsync();

                foreach (var (trayItemId, qty, wg) in deductions)
                {
                    _sWDbContext.BorrowTrayDeduction.Add(new BorrowTrayDeduction
                    {
                        BorrowDetailId = borrowDetail.BorrowDetailId,
                        TrayItemId = trayItemId,
                        DeductedQty = qty,
                        DeductedWg = wg
                    });
                }
                await _sWDbContext.SaveChangesAsync();
                return;
            }

            double borrowWg = stock.TtQty > 0
                ? (double)(borrowQty / stock.TtQty) * stock.TtWg
                : 0;

            _sWDbContext.BorrowDetail.Add(new BorrowDetail
            {
                StockId = stockId,
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

        public async Task ReturnBorrowAsync(int borrowDetailId, int userId)
        {
            var borrow = await _sWDbContext.BorrowDetail
                .FirstOrDefaultAsync(b => b.BorrowDetailId == borrowDetailId && !b.IsReturned);
            if (borrow == null) return;

            var now = DateTime.Now;

            // restore TrayItems ที่ถูกหักตอนยืม
            var deductions = await _sWDbContext.BorrowTrayDeduction
                .Where(d => d.BorrowDetailId == borrowDetailId)
                .ToListAsync();

            if (deductions.Count > 0)
            {
                var trayItemIds = deductions.Select(d => d.TrayItemId).ToList();
                var trayItems = await _sWDbContext.TrayItem
                    .Where(ti => trayItemIds.Contains(ti.TrayItemId))
                    .ToListAsync();
                var trayItemDict = trayItems.ToDictionary(ti => ti.TrayItemId);

                foreach (var d in deductions)
                {
                    if (!trayItemDict.TryGetValue(d.TrayItemId, out var ti)) continue;
                    ti.Qty += d.DeductedQty;
                    ti.Wg += d.DeductedWg;
                    ti.UpdateDate = now;
                    ti.UpdatedBy = userId;
                }

                _sWDbContext.BorrowTrayDeduction.RemoveRange(deductions);
            }

            borrow.IsReturned = true;
            borrow.ReturnDate = now;
            borrow.UpdateDate = now;
            borrow.UpdatedBy = userId;

            await _sWDbContext.SaveChangesAsync();
        }

        public async Task<List<BorrowModel>> GetBorrowListAsync(string? groupKey)
        {
            var query = _sWDbContext.BorrowDetail.Where(b => !b.IsReturned).AsQueryable();

            if (!string.IsNullOrEmpty(groupKey))
            {
                var stockIds = await ResolveGroupKeyToStockIdsAsync(groupKey);
                query = query.Where(b => stockIds.Contains(b.StockId));
            }

            var borrows = await query.ToListAsync();
            return await MapBorrowDetailsAsync(borrows);
        }

        public async Task WithdrawFromStockAsync(string groupKey, decimal withdrawQty, string? remark, int userId, bool isAdminAdded = false)
        {
            if (int.TryParse(groupKey, out int singleStockId))
            {
                await WithdrawFromSingleStockAsync(singleStockId, withdrawQty, remark, userId);
                return;
            }

            var sources = await ResolveGroupKeyAsync(groupKey, isAdminAdded);
            decimal remaining = withdrawQty;
            foreach (var src in sources)
            {
                if (remaining <= 0) break;
                var inTrayQty = await _sWDbContext.TrayItem
                    .Where(ti => ti.StockId == src.StockId && ti.IsActive)
                    .SumAsync(ti => ti.Qty);
                var borrowedQty = await _sWDbContext.BorrowDetail
                    .Where(b => b.StockId == src.StockId && !b.IsReturned)
                    .SumAsync(b => b.BorrowQty);
                var available = src.TtQty - inTrayQty - borrowedQty;
                if (available <= 0) continue;
                var deduct = Math.Min(remaining, available);
                await WithdrawFromSingleStockAsync(src.StockId, deduct, remark, userId);
                remaining -= deduct;
            }
        }

        private async Task WithdrawFromSingleStockAsync(int receivedId, decimal withdrawQty, string? remark, int userId)
        {
            var stock = await _sWDbContext.Stock.FirstOrDefaultAsync(s => s.StockId == receivedId && s.IsActive);
            if (stock == null) return;

            var inTrayQty = await _sWDbContext.TrayItem
                .Where(ti => ti.StockId == receivedId && ti.IsActive)
                .SumAsync(ti => ti.Qty);

            var borrowedQty = await _sWDbContext.BorrowDetail
                .Where(b => b.StockId == receivedId && !b.IsReturned)
                .SumAsync(b => b.BorrowQty);

            var availableQty = stock.TtQty - inTrayQty - borrowedQty;
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

            _sWDbContext.WithdrawalDetail.Add(new WithdrawalDetail
            {
                StockId = receivedId,
                Qty = withdrawQty,
                Wg = withdrawWg,
                Remark = remark,
                WithdrawnBy = userId,
                IsActive = true,
                CreateDate = now,
                UpdateDate = now,
                UpdatedBy = userId
            });

            await _sWDbContext.SaveChangesAsync();
        }

        public async Task DeleteAdminStockAsync(string groupKey)
        {
            var sources = await ResolveGroupKeyAsync(groupKey, isAdminAdded: true);
            if (sources.Count == 0)
                throw new KeyNotFoundException("ไม่พบสินค้า ADMIN สำหรับ groupKey นี้");

            _sWDbContext.Stock.RemoveRange(sources);
            await _sWDbContext.SaveChangesAsync();
        }

        public async Task<List<WithdrawalModel>> GetWithdrawalListAsync()
        {
            var withdrawals = await _sWDbContext.WithdrawalDetail
                .Where(wd => wd.IsActive)
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
                    WithdrawalId = w.WithdrawalDetailId,
                    WithdrawalNo = w.WithdrawalNo,
                    ReceivedId = w.StockId,
                    LotNo = stock?.LotNo ?? string.Empty,
                    Barcode = stock?.Barcode ?? string.Empty,
                    Article = stock?.Article ?? string.Empty,
                    TempArticle = stock?.TempArticle,
                    OrderNo = stock?.OrderNo ?? string.Empty,
                    CustCode = stock?.CustCode ?? string.Empty,
                    EDesFn = stock?.EdesFn,
                    ListGem = stock?.ListGem,
                    ImgPath = stock?.ImgPath ?? string.Empty,
                    EDesArt = stock?.EdesArt ?? string.Empty,
                    Unit = stock?.Unit ?? string.Empty,
                    Qty = w.Qty,
                    Wg = w.Wg,
                    Remark = w.Remark,
                    WithdrawnBy = w.WithdrawnBy,
                    WithdrawnDate = w.CreateDate?.ToString("dd-MM-yyyy", new CultureInfo("th-TH")) ?? string.Empty
                };
            }).ToList();

            return result;
        }

        public async Task<List<LostAndRepairModel>> GetBreakAsync(BreakAndLostFilterModel breakAndLostFilterModel)
        {
            var query = _sWDbContext.BreakDetail.Where(b => b.IsActive).AsQueryable();

            if (!string.IsNullOrEmpty(breakAndLostFilterModel.GroupKey))
            {
                var filterStockIds = await ResolveGroupKeyToStockIdsAsync(breakAndLostFilterModel.GroupKey);
                query = query.Where(b => filterStockIds.Contains(b.StockId));
            }
            else if (breakAndLostFilterModel.ReceivedId.HasValue)
                query = query.Where(b => b.StockId == breakAndLostFilterModel.ReceivedId.Value);

            if (breakAndLostFilterModel.BreakIDs != null && breakAndLostFilterModel.BreakIDs.Length > 0)
                query = query.Where(b => breakAndLostFilterModel.BreakIDs.Contains(b.BreakDetailId));

            var breaks = await query.OrderByDescending(b => b.CreateDate).Take(100).ToListAsync();

            if (breaks.Count == 0) return [];

            var stockIds = breaks.Select(b => b.StockId).Distinct().ToList();
            var stocks = await _sWDbContext.Stock
                .Where(s => stockIds.Contains(s.StockId))
                .ToDictionaryAsync(s => s.StockId);

            var descIds = breaks.Select(b => b.BreakDescriptionId).Distinct().ToList();
            var descriptions = await _sPDbContext.BreakDescription
                .Where(d => descIds.Contains(d.BreakDescriptionId))
                .ToDictionaryAsync(d => d.BreakDescriptionId, d => d.Name ?? string.Empty);

            return breaks.Select(b =>
            {
                stocks.TryGetValue(b.StockId, out var stock);
                descriptions.TryGetValue(b.BreakDescriptionId, out var descName);

                return new LostAndRepairModel
                {
                    BreakID = b.BreakDetailId,
                    BreakNo = b.BreakNo,
                    ReceivedID = b.StockId,
                    ReceiveNo = stock?.ReceiveNo ?? string.Empty,
                    LotNo = stock?.LotNo ?? string.Empty,
                    TtQty = stock?.TtQty ?? 0,
                    TtWg = Math.Round(stock?.TtWg ?? 0, 2),
                    Barcode = stock?.Barcode ?? string.Empty,
                    Article = stock?.Article ?? stock?.TempArticle ?? string.Empty,
                    OrderNo = stock?.OrderNo ?? string.Empty,
                    CustCode = stock?.CustCode ?? string.Empty,
                    EdesFn = stock?.EdesFn ?? string.Empty,
                    ListGem = stock?.ListGem ?? string.Empty,
                    ImgPath = stock?.ImgPath ?? string.Empty,
                    BreakQty = b.BreakQty,
                    IsReported = b.IsReported,
                    BreakDescription = descName ?? string.Empty,
                    CreateDateTH = b.CreateDate.GetValueOrDefault().ToString("dd MMMM yyyy", new CultureInfo("th-TH")),
                    CreateDate = b.CreateDate.GetValueOrDefault()
                };
            }).ToList();
        }

        public async Task<BaseResponseModel> AddBreakAsync(string groupKey, double breakQty, int breakDes)
        {
            if (string.IsNullOrEmpty(groupKey))
                throw new ArgumentException("groupKey ไม่ถูกต้อง", nameof(groupKey));

            if (breakQty <= 0)
                throw new ArgumentException("BreakQty ต้องมากกว่า 0", nameof(breakQty));

            var sources = await ResolveGroupKeyAsync(groupKey);
            if (sources.Count == 0)
                throw new KeyNotFoundException($"ไม่พบสินค้าสำหรับ groupKey: {groupKey}");

            var totalQty = sources.Sum(s => (double)s.TtQty);
            if (totalQty < breakQty)
                throw new InvalidOperationException($"จำนวน Break ({breakQty}) มากกว่ายอดรวม ({totalQty})");

            double remaining = breakQty;

            await using var transaction = await _sWDbContext.Database.BeginTransactionAsync();
            try
            {
                foreach (var stock in sources)
                {
                    if (remaining <= 0) break;

                    double deduct = Math.Min(remaining, (double)stock.TtQty);
                    double oldQty = (double)stock.TtQty;
                    double oldWg  = stock.TtWg;
                    double newQty = oldQty - deduct;
                    double newWg  = oldQty > 0 ? (oldWg / oldQty) * newQty : 0;

                    _sWDbContext.BreakDetail.Add(new BreakDetail
                    {
                        StockId            = stock.StockId,
                        BreakQty           = (decimal)deduct,
                        BreakDescriptionId = breakDes,
                        IsReported         = false,
                        IsActive           = true,
                        CreateDate         = DateTime.Now,
                        UpdateDate         = DateTime.Now
                    });

                    stock.TtQty      = (decimal)newQty;
                    stock.TtWg       = Math.Round(newWg, 2);
                    stock.IsRepairing = true;
                    stock.UpdateDate  = DateTime.Now;
                    _sWDbContext.Stock.Update(stock);

                    await _sWDbContext.SaveChangesAsync();

                    await UpdateJobBillSendStockAndSpdreceive(
                        stock.BillNumber, stock.ReceiveId, stock.ReceiveNo,
                        (decimal)newQty, (decimal)Math.Round(newWg, 2));

                    remaining -= deduct;
                }

                await transaction.CommitAsync();

                return new BaseResponseModel { IsSuccess = true, Message = "เพิ่มรายการชำรุดเรียบร้อย" };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task UpdateJobBillSendStockAndSpdreceive(int Billnumber, int ID, string ReceiveNo, decimal TtQty, decimal TtWg)
        {
            using var transaction = await _jPDbContext.Database.BeginTransactionAsync();
            try
            {
                var spdreceive = await _jPDbContext.Spdreceive.OrderByDescending(o => o.Ttqty).FirstOrDefaultAsync(o => o.Billnumber == Billnumber && o.Id == ID && o.ReceiveNo == ReceiveNo);
                if (spdreceive != null)
                {
                    spdreceive.Ttqty = TtQty;
                    spdreceive.Ttwg = TtWg;
                }
                else
                {
                    var a = _sPDbContext.Received.Where(o => o.ReceiveId == ID && o.BillNumber == Billnumber).FirstOrDefault();
                    if (a != null)
                    {
                        spdreceive = await _jPDbContext.Spdreceive.OrderByDescending(o => o.Ttqty).FirstOrDefaultAsync(o => o.Billnumber == a.BillNumber && o.ReceiveNo == a.ReceiveNo);
                        if (spdreceive != null)
                        {
                            spdreceive.Ttqty = TtQty;
                            spdreceive.Ttwg = TtWg;
                        }
                    }
                }

                var jobBillSendStock = await _jPDbContext.JobBillSendStock.FirstOrDefaultAsync(o => o.Billnumber == Billnumber);
                if (jobBillSendStock != null)
                {
                    jobBillSendStock.Ttqty = TtQty;
                    jobBillSendStock.Ttwg = TtWg;
                }

                await _jPDbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<BreakDescription>> GetBreakDescriptionsAsync()
        {
            var des = await _sPDbContext.BreakDescription
                .Where(x => x.IsActive)
                .Select(x => new BreakDescription
                {
                    BreakDescriptionId = x.BreakDescriptionId,
                    Name = x.Name,
                })
                .ToListAsync();

            return [.. des];
        }

        public async Task<List<BreakDescription>> AddNewBreakDescription(string breakDescription)
        {
            using var transaction = await _sPDbContext.Database.BeginTransactionAsync();
            try
            {
                BreakDescription newDescription = new()
                {
                    Name = breakDescription,
                    IsActive = true,
                    CreateDate = DateTime.Now,
                    UpdateDate = DateTime.Now
                };

                _sPDbContext.BreakDescription.Add(newDescription);
                await _sPDbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }

            return await GetBreakDescriptionsAsync();
        }


        public async Task PintedBreakReport(int[]? BreakIDs)
        {
            var breaks = await _sWDbContext.BreakDetail.Where(b => BreakIDs!.Contains(b.BreakDetailId) && b.IsActive).ToListAsync();

            using var transaction = await _sWDbContext.Database.BeginTransactionAsync();
            try
            {
                foreach (var bek in breaks)
                {
                    if (bek.IsReported) continue;

                    bek.IsReported = true;
                    bek.UpdateDate = DateTime.Now;
                }

                await _sWDbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
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

        public async Task<List<string>> GetProductTypesAsync()
        {
            return await _sPDbContext.ProductType
                .Where(p => p.IsActive && p.Name != null && p.Name != "")
                .OrderBy(p => p.Name)
                .Select(p => p.Name!)
                .ToListAsync();
        }

        public async Task<List<WithdrawalHeaderModel>> GetWithdrawalHeadersAsync(string? article = null, string? edesArt = null, string? withdrawalNo = null)
        {
            List<string>? matchingNos = null;
            if (!string.IsNullOrWhiteSpace(article) || !string.IsNullOrWhiteSpace(edesArt))
            {
                var stockQuery = _sWDbContext.Stock.AsQueryable();
                if (!string.IsNullOrWhiteSpace(article))
                    stockQuery = stockQuery.Where(s =>
                        (s.Article != null && s.Article.Contains(article)) ||
                        (s.TempArticle != null && s.TempArticle.Contains(article)));
                if (!string.IsNullOrWhiteSpace(edesArt))
                    stockQuery = stockQuery.Where(s => s.EdesArt != null && (
                        s.EdesArt == edesArt ||
                        s.EdesArt.StartsWith(edesArt + " ") ||
                        s.EdesArt.EndsWith(" " + edesArt) ||
                        s.EdesArt.Contains(" " + edesArt + " ")));
                var matchingStockIds = await stockQuery.Select(s => s.StockId).ToListAsync();
                matchingNos = await _sWDbContext.WithdrawalDetail
                    .Where(d => d.IsActive && d.WithdrawalNo != null && matchingStockIds.Contains(d.StockId))
                    .Select(d => d.WithdrawalNo!)
                    .Distinct()
                    .ToListAsync();
            }

            var headerQuery = _sWDbContext.Withdrawal.Where(w => w.IsActive);
            if (matchingNos != null)
                headerQuery = headerQuery.Where(w => matchingNos.Contains(w.WithdrawalNo));
            if (!string.IsNullOrWhiteSpace(withdrawalNo))
                headerQuery = headerQuery.Where(w => w.WithdrawalNo.Contains(withdrawalNo));

            var headers = await headerQuery.OrderByDescending(w => w.CreateDate).ToListAsync();

            var withdrawalNos = headers.Select(h => h.WithdrawalNo).ToList();

            var detailStats = await _sWDbContext.WithdrawalDetail
                .Where(d => d.IsActive && d.WithdrawalNo != null && withdrawalNos.Contains(d.WithdrawalNo))
                .GroupBy(d => d.WithdrawalNo!)
                .Select(g => new
                {
                    WithdrawalNo = g.Key,
                    ItemCount = g.Count(),
                    TotalQty = g.Sum(d => d.Qty),
                    TotalWg = g.Sum(d => d.Wg)
                })
                .ToDictionaryAsync(x => x.WithdrawalNo);

            return headers.Select(h =>
            {
                detailStats.TryGetValue(h.WithdrawalNo, out var stats);
                return new WithdrawalHeaderModel
                {
                    WithdrawalNo = h.WithdrawalNo,
                    CreateDate = h.CreateDate?.ToString("dd-MM-yyyy", new CultureInfo("th-TH")) ?? string.Empty,
                    ItemCount = stats?.ItemCount ?? 0,
                    TotalQty = stats?.TotalQty ?? 0,
                    TotalWg = stats?.TotalWg ?? 0
                };
            }).ToList();
        }

        public async Task<List<WithdrawalModel>> GetWithdrawalDetailsByNoAsync(string withdrawalNo)
        {
            var details = await _sWDbContext.WithdrawalDetail
                .Where(d => d.WithdrawalNo == withdrawalNo && d.IsActive)
                .OrderBy(d => d.CreateDate)
                .ToListAsync();

            var stockIds = details.Select(d => d.StockId).Distinct().ToList();
            var stocks = await _sWDbContext.Stock
                .Where(s => stockIds.Contains(s.StockId))
                .ToDictionaryAsync(s => s.StockId);

            return details.Select(d =>
            {
                stocks.TryGetValue(d.StockId, out var stock);
                return new WithdrawalModel
                {
                    WithdrawalId = d.WithdrawalDetailId,
                    ReceivedId = d.StockId,
                    LotNo = stock?.LotNo ?? string.Empty,
                    Barcode = stock?.Barcode ?? string.Empty,
                    Article = stock?.Article ?? string.Empty,
                    TempArticle = stock?.TempArticle,
                    OrderNo = stock?.OrderNo ?? string.Empty,
                    CustCode = stock?.CustCode ?? string.Empty,
                    EDesFn = stock?.EdesFn,
                    ListGem = stock?.ListGem,
                    ImgPath = stock?.ImgPath ?? string.Empty,
                    EDesArt = stock?.EdesArt ?? string.Empty,
                    Unit = stock?.Unit ?? string.Empty,
                    Qty = d.Qty,
                    Wg = d.Wg,
                    Remark = d.Remark,
                    WithdrawnBy = d.WithdrawnBy,
                    WithdrawnDate = d.CreateDate?.ToString("dd-MM-yyyy", new CultureInfo("th-TH")) ?? string.Empty
                };
            }).ToList();
        }

        public async Task CancelPendingWithdrawalAsync(int withdrawalDetailId, int userId)
        {
            var detail = await _sWDbContext.WithdrawalDetail
                .FirstOrDefaultAsync(d => d.WithdrawalDetailId == withdrawalDetailId && d.WithdrawalNo == null && d.IsActive);
            if (detail == null) throw new InvalidOperationException("ไม่พบรายการ หรือรายการนี้มีเลขที่เอกสารแล้ว");

            var stock = await _sWDbContext.Stock.FirstOrDefaultAsync(s => s.StockId == detail.StockId);
            if (stock != null)
            {
                stock.TtQty += detail.Qty;
                stock.TtWg += detail.Wg;
                if (!stock.IsActive) stock.IsActive = true;
                stock.UpdateDate = DateTime.Now;
            }

            _sWDbContext.WithdrawalDetail.Remove(detail);
            await _sWDbContext.SaveChangesAsync();
        }

        public async Task<List<WithdrawalModel>> GetPendingWithdrawalDetailsAsync(string? article = null, string? edesArt = null)
        {
            var detailQuery = _sWDbContext.WithdrawalDetail.Where(d => d.WithdrawalNo == null && d.IsActive);
            if (!string.IsNullOrWhiteSpace(article) || !string.IsNullOrWhiteSpace(edesArt))
            {
                var stockQuery = _sWDbContext.Stock.AsQueryable();
                if (!string.IsNullOrWhiteSpace(article))
                    stockQuery = stockQuery.Where(s =>
                        (s.Article != null && s.Article.Contains(article)) ||
                        (s.TempArticle != null && s.TempArticle.Contains(article)));
                if (!string.IsNullOrWhiteSpace(edesArt))
                    stockQuery = stockQuery.Where(s => s.EdesArt != null && (
                        s.EdesArt == edesArt ||
                        s.EdesArt.StartsWith(edesArt + " ") ||
                        s.EdesArt.EndsWith(" " + edesArt) ||
                        s.EdesArt.Contains(" " + edesArt + " ")));
                var matchingStockIds = await stockQuery.Select(s => s.StockId).ToListAsync();
                detailQuery = detailQuery.Where(d => matchingStockIds.Contains(d.StockId));
            }

            var details = await detailQuery.OrderByDescending(d => d.CreateDate).ToListAsync();

            var stockIds = details.Select(d => d.StockId).Distinct().ToList();
            var stocks = await _sWDbContext.Stock
                .Where(s => stockIds.Contains(s.StockId))
                .ToDictionaryAsync(s => s.StockId);

            return details.Select(d =>
            {
                stocks.TryGetValue(d.StockId, out var stock);
                return new WithdrawalModel
                {
                    WithdrawalId = d.WithdrawalDetailId,
                    ReceivedId = d.StockId,
                    LotNo = stock?.LotNo ?? string.Empty,
                    Barcode = stock?.Barcode ?? string.Empty,
                    Article = stock?.Article ?? string.Empty,
                    TempArticle = stock?.TempArticle,
                    OrderNo = stock?.OrderNo ?? string.Empty,
                    CustCode = stock?.CustCode ?? string.Empty,
                    EDesFn = stock?.EdesFn,
                    ListGem = stock?.ListGem,
                    ImgPath = stock?.ImgPath ?? string.Empty,
                    EDesArt = stock?.EdesArt ?? string.Empty,
                    Unit = stock?.Unit ?? string.Empty,
                    Qty = d.Qty,
                    Wg = d.Wg,
                    Remark = d.Remark,
                    WithdrawnBy = d.WithdrawnBy,
                    WithdrawnDate = d.CreateDate?.ToString("dd-MM-yyyy HH:mm", new CultureInfo("th-TH")) ?? string.Empty
                };
            }).ToList();
        }

        private async Task<string> GenerateSWWDReceiveNoAsync()
        {
            string prefix = "SW/WD";
            string year = DateTime.Now.ToString("yy");
            string month = DateTime.Now.ToString("MM");
            string basePrefix = $"{year}{prefix}{month}";

            string? lastDoc = await _sWDbContext.Withdrawal
                .Where(r => r.WithdrawalNo != null && r.WithdrawalNo.StartsWith(basePrefix))
                .OrderByDescending(r => r.WithdrawalNo)
                .Select(r => r.WithdrawalNo)
                .FirstOrDefaultAsync();

            int nextSeq = 1;

            if (!string.IsNullOrWhiteSpace(lastDoc) && lastDoc.Length >= 10)
            {
                string seqPart = lastDoc.Substring(9, 4);
                if (int.TryParse(seqPart, out int lastSeq))
                {
                    nextSeq = lastSeq + 1;
                }
            }

            string newDoc = $"{basePrefix}{nextSeq:D4}";

            return newDoc;
        }

        private async Task<string> GenerateSWBorrowNoAsync()
        {
            string prefix = "SW/BR";
            string year = DateTime.Now.ToString("yy");
            string month = DateTime.Now.ToString("MM");
            string basePrefix = $"{year}{prefix}{month}";

            string? lastDoc = await _sWDbContext.Borrow
                .Where(r => r.BorrowNo.StartsWith(basePrefix))
                .OrderByDescending(r => r.BorrowNo)
                .Select(r => r.BorrowNo)
                .FirstOrDefaultAsync();

            int nextSeq = 1;

            if (!string.IsNullOrWhiteSpace(lastDoc) && lastDoc.Length >= basePrefix.Length)
            {
                string seqPart = lastDoc[basePrefix.Length..];
                if (int.TryParse(seqPart, out int lastSeq))
                    nextSeq = lastSeq + 1;
            }

            return $"{basePrefix}{nextSeq:D4}";
        }

        public async Task<List<BorrowHeaderModel>> GetBorrowHeadersAsync(string? article = null, string? edesArt = null, string? borrowNo = null, bool? isReturned = null)
        {
            List<string>? matchingNos = null;
            if (!string.IsNullOrWhiteSpace(article) || !string.IsNullOrWhiteSpace(edesArt))
            {
                var stockQuery = _sWDbContext.Stock.AsQueryable();
                if (!string.IsNullOrWhiteSpace(article))
                    stockQuery = stockQuery.Where(s =>
                        (s.Article != null && s.Article.Contains(article)) ||
                        (s.TempArticle != null && s.TempArticle.Contains(article)));
                if (!string.IsNullOrWhiteSpace(edesArt))
                    stockQuery = stockQuery.Where(s => s.EdesArt != null && (
                        s.EdesArt == edesArt ||
                        s.EdesArt.StartsWith(edesArt + " ") ||
                        s.EdesArt.EndsWith(" " + edesArt) ||
                        s.EdesArt.Contains(" " + edesArt + " ")));
                var matchingStockIds = await stockQuery.Select(s => s.StockId).ToListAsync();
                matchingNos = await _sWDbContext.BorrowDetail
                    .Where(d => d.BorrowNo != null && matchingStockIds.Contains(d.StockId))
                    .Select(d => d.BorrowNo!)
                    .Distinct()
                    .ToListAsync();
            }

            var headerQuery = _sWDbContext.Borrow.Where(b => b.IsActive);
            if (matchingNos != null)
                headerQuery = headerQuery.Where(b => matchingNos.Contains(b.BorrowNo));
            if (!string.IsNullOrWhiteSpace(borrowNo))
                headerQuery = headerQuery.Where(b => b.BorrowNo.Contains(borrowNo));
            if (isReturned.HasValue)
            {
                var notReturnedNos = _sWDbContext.BorrowDetail
                    .Where(d => !d.IsReturned && d.BorrowNo != null)
                    .Select(d => d.BorrowNo!);
                if (isReturned.Value)
                    headerQuery = headerQuery.Where(b => !notReturnedNos.Contains(b.BorrowNo));
                else
                    headerQuery = headerQuery.Where(b => notReturnedNos.Contains(b.BorrowNo));
            }

            var headers = await headerQuery.OrderByDescending(b => b.CreateDate).ToListAsync();
            var borrowNos = headers.Select(h => h.BorrowNo).ToList();

            var detailStats = await _sWDbContext.BorrowDetail
                .Where(d => d.BorrowNo != null && borrowNos.Contains(d.BorrowNo))
                .GroupBy(d => d.BorrowNo!)
                .Select(g => new { BorrowNo = g.Key, ItemCount = g.Count(), ReturnedCount = g.Count(d => d.IsReturned) })
                .ToListAsync();

            var statDict = detailStats.ToDictionary(s => s.BorrowNo);

            return headers.Select(h =>
            {
                statDict.TryGetValue(h.BorrowNo, out var stat);
                return new BorrowHeaderModel
                {
                    BorrowNo = h.BorrowNo,
                    CreateDate = h.CreateDate?.ToString("dd-MM-yyyy HH:mm", new CultureInfo("th-TH")) ?? string.Empty,
                    ItemCount = stat?.ItemCount ?? 0,
                    ReturnedCount = stat?.ReturnedCount ?? 0
                };
            }).ToList();
        }

        public async Task<List<BorrowModel>> GetBorrowDetailsByNoAsync(string borrowNo)
        {
            var details = await _sWDbContext.BorrowDetail
                .Where(d => d.BorrowNo == borrowNo)
                .ToListAsync();

            return await MapBorrowDetailsAsync(details);
        }

        public async Task<List<BorrowModel>> GetPendingBorrowDetailsAsync(string? article = null, string? edesArt = null, bool? isReturned = null)
        {
            var query = _sWDbContext.BorrowDetail.Where(d => d.BorrowNo == null);
            if (isReturned.HasValue)
                query = query.Where(d => d.IsReturned == isReturned.Value);

            if (!string.IsNullOrWhiteSpace(article) || !string.IsNullOrWhiteSpace(edesArt))
            {
                var stockQuery = _sWDbContext.Stock.AsQueryable();
                if (!string.IsNullOrWhiteSpace(article))
                    stockQuery = stockQuery.Where(s =>
                        (s.Article != null && s.Article.Contains(article)) ||
                        (s.TempArticle != null && s.TempArticle.Contains(article)));
                if (!string.IsNullOrWhiteSpace(edesArt))
                    stockQuery = stockQuery.Where(s => s.EdesArt != null && (
                        s.EdesArt == edesArt ||
                        s.EdesArt.StartsWith(edesArt + " ") ||
                        s.EdesArt.EndsWith(" " + edesArt) ||
                        s.EdesArt.Contains(" " + edesArt + " ")));
                var matchingStockIds = await stockQuery.Select(s => s.StockId).ToListAsync();
                query = query.Where(d => matchingStockIds.Contains(d.StockId));
            }

            var details = await query.ToListAsync();
            return await MapBorrowDetailsAsync(details);
        }

        public async Task CancelPendingBorrowAsync(int borrowDetailId, int userId)
        {
            var detail = await _sWDbContext.BorrowDetail
                .FirstOrDefaultAsync(d => d.BorrowDetailId == borrowDetailId && d.BorrowNo == null && !d.IsReturned);
            if (detail == null) throw new InvalidOperationException("ไม่พบรายการ หรือรายการนี้มีเลขที่เอกสารแล้ว");

            var deductions = await _sWDbContext.BorrowTrayDeduction
                .Where(d => d.BorrowDetailId == borrowDetailId)
                .ToListAsync();

            if (deductions.Count > 0)
            {
                var trayItemIds = deductions.Select(d => d.TrayItemId).ToList();
                var trayItems = await _sWDbContext.TrayItem
                    .Where(ti => trayItemIds.Contains(ti.TrayItemId))
                    .ToDictionaryAsync(ti => ti.TrayItemId);

                var now = DateTime.Now;
                foreach (var d in deductions)
                {
                    if (!trayItems.TryGetValue(d.TrayItemId, out var ti)) continue;
                    ti.Qty += d.DeductedQty;
                    ti.Wg += d.DeductedWg;
                    ti.UpdateDate = now;
                    ti.UpdatedBy = userId;
                }

                _sWDbContext.BorrowTrayDeduction.RemoveRange(deductions);
            }

            _sWDbContext.BorrowDetail.Remove(detail);
            await _sWDbContext.SaveChangesAsync();
        }

        public async Task<string> CreateBorrowDocumentAsync(int[] detailIds, int userId)
        {
            var today = DateTime.Now;
            var borrowNo = await GenerateSWBorrowNoAsync();

            _sWDbContext.Borrow.Add(new Borrow
            {
                BorrowNo = borrowNo,
                IsActive = true,
                CreateBy = userId,
                CreateDate = today
            });

            var details = await _sWDbContext.BorrowDetail
                .Where(d => detailIds.Contains(d.BorrowDetailId) && d.BorrowNo == null && !d.IsReturned)
                .ToListAsync();

            foreach (var d in details)
            {
                d.BorrowNo = borrowNo;
                d.UpdatedBy = userId;
                d.UpdateDate = today;
            }

            await _sWDbContext.SaveChangesAsync();
            return borrowNo;
        }

        public async Task<List<BorrowModel>> GetBorrowsByStockIdAsync(string groupKey)
        {
            List<int> stockIds;
            if (int.TryParse(groupKey, out int singleId))
                stockIds = [singleId];
            else
                stockIds = await ResolveGroupKeyToStockIdsAsync(groupKey);

            var details = await _sWDbContext.BorrowDetail
                .Where(d => !d.IsReturned && stockIds.Contains(d.StockId))
                .ToListAsync();

            return await MapBorrowDetailsAsync(details);
        }

        private async Task<List<BorrowModel>> MapBorrowDetailsAsync(List<BorrowDetail> details)
        {
            var stockIds = details.Select(d => d.StockId).Distinct().ToList();
            var stocks = await _sWDbContext.Stock
                .Where(s => stockIds.Contains(s.StockId))
                .ToDictionaryAsync(s => s.StockId);

            // ถาด: BorrowTrayDeduction → TrayItem → Tray
            var detailIds = details.Select(d => d.BorrowDetailId).ToList();
            var trayNosRaw = await _sWDbContext.BorrowTrayDeduction
                .Where(btd => detailIds.Contains(btd.BorrowDetailId))
                .Join(_sWDbContext.TrayItem, btd => btd.TrayItemId, ti => ti.TrayItemId, (btd, ti) => new { btd.BorrowDetailId, ti.TrayId })
                .Join(_sWDbContext.Tray, x => x.TrayId, t => t.TrayId, (x, t) => new { x.BorrowDetailId, t.TrayNo })
                .ToListAsync();
            var trayNosDict = trayNosRaw
                .GroupBy(x => x.BorrowDetailId)
                .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(x => x.TrayNo).Distinct().OrderBy(n => n)));

            return details.Select(d =>
            {
                stocks.TryGetValue(d.StockId, out var stock);

                return new BorrowModel
                {
                    BorrowDetailId = d.BorrowDetailId,
                    StockId = d.StockId,
                    BorrowNo = d.BorrowNo,
                    LotNo = stock?.LotNo ?? string.Empty,
                    Barcode = stock?.Barcode ?? string.Empty,
                    Article = stock?.Article ?? stock?.TempArticle ?? string.Empty,
                    EDesFn = stock?.EdesFn,
                    ListGem = stock?.ListGem,
                    ImgPath = stock?.ImgPath ?? string.Empty,
                    BorrowQty = d.BorrowQty,
                    BorrowWg = d.BorrowWg,
                    BorrowedBy = d.BorrowedBy,
                    BorrowedDate = d.BorrowDate?.ToString("dd-MM-yyyy", new CultureInfo("th-TH")) ?? string.Empty,
                    ReturnedDate = d.ReturnDate?.ToString("dd-MM-yyyy", new CultureInfo("th-TH")),
                    IsReturned = d.IsReturned,
                    TrayNo = trayNosDict.GetValueOrDefault(d.BorrowDetailId)
                };
            }).ToList();
        }

        private static string BuildGroupKey(string? article, string? barcode, string? listGem, string? edesFn)
            => $"{article ?? ""}|{barcode ?? ""}|{listGem ?? ""}|{edesFn ?? ""}";

        private async Task<List<Data.SWDbContext.Entities.Stock>> ResolveGroupKeyAsync(string groupKey, bool? isAdminAdded = null)
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

        private async Task<List<int>> ResolveGroupKeyToStockIdsAsync(string groupKey)
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

        private async Task<string> GenerateSWBreakNoAsync()
        {
            var now = DateTime.Now;
            var prefix = $"{now:yy}SW/BK{now:MM}";
            var lastNo = await _sWDbContext.Break
                .Where(b => b.BreakNo.StartsWith(prefix))
                .OrderByDescending(b => b.BreakNo)
                .Select(b => b.BreakNo)
                .FirstOrDefaultAsync();
            int seq = 1;
            if (!string.IsNullOrEmpty(lastNo) && lastNo.Length >= prefix.Length + 4)
                if (int.TryParse(lastNo[^4..], out var lastSeq)) seq = lastSeq + 1;
            return $"{prefix}{seq:D4}";
        }

        private async Task<List<LostAndRepairModel>> MapBreakDetailsAsync(List<BreakDetail> details)
        {
            if (details.Count == 0) return [];
            var stockIds = details.Select(d => d.StockId).Distinct().ToList();
            var stocks = await _sWDbContext.Stock.Where(s => stockIds.Contains(s.StockId)).ToDictionaryAsync(s => s.StockId);
            var descIds = details.Select(d => d.BreakDescriptionId).Distinct().ToList();
            var descriptions = await _sPDbContext.BreakDescription
                .Where(d => descIds.Contains(d.BreakDescriptionId))
                .ToDictionaryAsync(d => d.BreakDescriptionId, d => d.Name ?? string.Empty);
            return details.Select(d =>
            {
                stocks.TryGetValue(d.StockId, out var stock);
                descriptions.TryGetValue(d.BreakDescriptionId, out var descName);
                return new LostAndRepairModel
                {
                    BreakID = d.BreakDetailId,
                    BreakNo = d.BreakNo,
                    ReceivedID = d.StockId,
                    ReceiveNo = stock?.ReceiveNo ?? string.Empty,
                    LotNo = stock?.LotNo ?? string.Empty,
                    Barcode = stock?.Barcode ?? string.Empty,
                    Article = stock?.Article ?? stock?.TempArticle ?? string.Empty,
                    OrderNo = stock?.OrderNo ?? string.Empty,
                    CustCode = stock?.CustCode ?? string.Empty,
                    EdesFn = stock?.EdesFn ?? string.Empty,
                    ListGem = stock?.ListGem ?? string.Empty,
                    ImgPath = stock?.ImgPath ?? string.Empty,
                    BreakQty = d.BreakQty,
                    IsReported = d.IsReported,
                    BreakDescription = descName ?? string.Empty,
                    CreateDateTH = d.CreateDate.GetValueOrDefault().ToString("dd MMMM yyyy", new CultureInfo("th-TH")),
                    CreateDate = d.CreateDate.GetValueOrDefault()
                };
            }).ToList();
        }

        public async Task<List<BreakHeaderModel>> GetBreakHeadersAsync(string? article = null, string? edesArt = null, string? breakNo = null)
        {
            List<string>? matchingNos = null;
            if (!string.IsNullOrWhiteSpace(article) || !string.IsNullOrWhiteSpace(edesArt))
            {
                var stockQuery = _sWDbContext.Stock.AsQueryable();
                if (!string.IsNullOrWhiteSpace(article))
                    stockQuery = stockQuery.Where(s =>
                        (s.Article != null && s.Article.Contains(article)) ||
                        (s.TempArticle != null && s.TempArticle.Contains(article)));
                if (!string.IsNullOrWhiteSpace(edesArt))
                    stockQuery = stockQuery.Where(s => s.EdesArt != null && (
                        s.EdesArt == edesArt ||
                        s.EdesArt.StartsWith(edesArt + " ") ||
                        s.EdesArt.EndsWith(" " + edesArt) ||
                        s.EdesArt.Contains(" " + edesArt + " ")));
                var matchingStockIds = await stockQuery.Select(s => s.StockId).ToListAsync();
                matchingNos = await _sWDbContext.BreakDetail
                    .Where(d => d.BreakNo != null && matchingStockIds.Contains(d.StockId) && d.IsActive)
                    .Select(d => d.BreakNo!)
                    .Distinct()
                    .ToListAsync();
            }

            var headerQuery = _sWDbContext.Break.Where(b => b.IsActive);
            if (matchingNos != null)
                headerQuery = headerQuery.Where(b => matchingNos.Contains(b.BreakNo));
            if (!string.IsNullOrWhiteSpace(breakNo))
                headerQuery = headerQuery.Where(b => b.BreakNo.Contains(breakNo));

            var headers = await headerQuery.OrderByDescending(b => b.CreateDate).ToListAsync();
            var breakNos = headers.Select(h => h.BreakNo).ToList();

            var detailStats = await _sWDbContext.BreakDetail
                .Where(d => d.BreakNo != null && breakNos.Contains(d.BreakNo) && d.IsActive)
                .GroupBy(d => d.BreakNo!)
                .Select(g => new { BreakNo = g.Key, ItemCount = g.Count() })
                .ToListAsync();

            var statDict = detailStats.ToDictionary(s => s.BreakNo);
            return headers.Select(h =>
            {
                statDict.TryGetValue(h.BreakNo, out var stat);
                return new BreakHeaderModel
                {
                    BreakNo = h.BreakNo,
                    CreateDate = h.CreateDate?.ToString("dd-MM-yyyy HH:mm", new CultureInfo("th-TH")) ?? string.Empty,
                    ItemCount = stat?.ItemCount ?? 0
                };
            }).ToList();
        }

        public async Task<List<LostAndRepairModel>> GetBreakDetailsByNoAsync(string breakNo)
        {
            var details = await _sWDbContext.BreakDetail
                .Where(d => d.BreakNo == breakNo && d.IsActive)
                .OrderByDescending(d => d.CreateDate)
                .ToListAsync();
            return await MapBreakDetailsAsync(details);
        }

        public async Task<List<LostAndRepairModel>> GetPendingBreakDetailsAsync(string? article = null, string? edesArt = null)
        {
            var query = _sWDbContext.BreakDetail.Where(d => d.BreakNo == null && d.IsActive);
            if (!string.IsNullOrWhiteSpace(article) || !string.IsNullOrWhiteSpace(edesArt))
            {
                var stockQuery = _sWDbContext.Stock.AsQueryable();
                if (!string.IsNullOrWhiteSpace(article))
                    stockQuery = stockQuery.Where(s =>
                        (s.Article != null && s.Article.Contains(article)) ||
                        (s.TempArticle != null && s.TempArticle.Contains(article)));
                if (!string.IsNullOrWhiteSpace(edesArt))
                    stockQuery = stockQuery.Where(s => s.EdesArt != null && (
                        s.EdesArt == edesArt ||
                        s.EdesArt.StartsWith(edesArt + " ") ||
                        s.EdesArt.EndsWith(" " + edesArt) ||
                        s.EdesArt.Contains(" " + edesArt + " ")));
                var matchingStockIds = await stockQuery.Select(s => s.StockId).ToListAsync();
                query = query.Where(d => matchingStockIds.Contains(d.StockId));
            }
            var details = await query.OrderByDescending(d => d.CreateDate).ToListAsync();
            return await MapBreakDetailsAsync(details);
        }

        public async Task<string> CreateBreakDocumentAsync(int[] detailIds, int userId)
        {
            var breakNo = await GenerateSWBreakNoAsync();
            _sWDbContext.Break.Add(new Data.SWDbContext.Entities.Break
            {
                BreakNo = breakNo,
                IsActive = true,
                CreateBy = userId,
                CreateDate = DateTime.Now,
                UpdateDate = DateTime.Now
            });
            var details = await _sWDbContext.BreakDetail
                .Where(d => detailIds.Contains(d.BreakDetailId) && d.IsActive)
                .ToListAsync();
            foreach (var d in details)
            {
                d.BreakNo = breakNo;
                d.UpdateDate = DateTime.Now;
            }
            await _sWDbContext.SaveChangesAsync();
            return breakNo;
        }

        public async Task<string> CreateWithdrawalDocumentAsync(int[] detailIds, int userId)
        {
            var today = DateTime.Now;

            var withdrawalNo = await GenerateSWWDReceiveNoAsync();

            _sWDbContext.Withdrawal.Add(new Withdrawal
            {
                WithdrawalNo = withdrawalNo,
                IsActive = true,
                CreateBy = userId,
                CreateDate = today
            });

            var details = await _sWDbContext.WithdrawalDetail
                .Where(d => detailIds.Contains(d.WithdrawalDetailId) && d.WithdrawalNo == null && d.IsActive)
                .ToListAsync();

            foreach (var d in details)
            {
                d.WithdrawalNo = withdrawalNo;
                d.UpdatedBy = userId;
                d.UpdateDate = today;
            }

            await _sWDbContext.SaveChangesAsync();
            return withdrawalNo;
        }

        public async Task<List<AddStockItemModel>> SearchAddStockItems(string article, string barcode)
        {
            var res = from cs in _jPDbContext.CpriceSale
                      join cp in _jPDbContext.Cprofile on cs.Article equals cp.Article
                      join fn in _jPDbContext.CfnCode on cs.FnCode equals fn.FnCode
                      select new AddStockItemModel
                      {
                          Article = cs.Article,
                          Barcode = cs.Barcode,
                          ListGem = cs.ListGem,
                          EdesArt = cp.EdesArt,
                          EdesFn = fn.EdesFn
                      };

            if (!string.IsNullOrWhiteSpace(article))
            {
                res = res.Where(r => r.Article != null && r.Article.Contains(article));
            }

            if (!string.IsNullOrWhiteSpace(barcode))
            {
                res = res.Where(r => r.Barcode != null && r.Barcode.Contains(barcode));
            }

            return await res.ToListAsync();
        }

        public async Task AddStockAsync(string barcode, decimal qty, int userId, string? orderNo = null)
        {
            var item = await (from cs in _jPDbContext.CpriceSale
                              join cp in _jPDbContext.Cprofile on cs.Article equals cp.Article
                              join fn in _jPDbContext.CfnCode on cs.FnCode equals fn.FnCode
                              where cs.Barcode == barcode
                              select new
                              {
                                  cs.Article,
                                  cs.ListGem,
                                  cp.EdesArt,
                                  fn.EdesFn,
                                  cs.Picture,
                                  cp.Eunit
                              }).FirstOrDefaultAsync() ?? throw new InvalidOperationException($"ไม่พบ Barcode: {barcode}");

            var user = await _pISService.GetUser(new ReqUserModel { UserID = userId }) ?? throw new InvalidOperationException($"ไม่พบผู้ใช้ ID: {userId}");
            if (user == null) throw new InvalidOperationException($"ไม่พบผู้ใช้ ID: {userId}");

            var now = DateTime.Now;
            var stock = new Stock
            {
                ReceiveId = 0,
                ReceiveNo = "ADMIN",
                CustCode = string.Empty,
                OrderNo = orderNo ?? $"Add By {user.FirstOrDefault()?.FirstName}",
                LotNo = string.Empty,
                TempArticle = string.Empty,
                Article = item.Article,
                Barcode = barcode,
                ListGem = item.ListGem,
                Unit = item.Eunit,
                EdesFn = item.EdesFn,
                EdesArt = item.EdesArt,
                BillNumber = 0,
                ImgPath = item.Picture,
                TtQty = qty,
                TtWg = 0,
                IsActive = true,
                CreateDate = now,
                UpdateDate = now,
            };

            await _sWDbContext.Stock.AddAsync(stock);
            await _sWDbContext.SaveChangesAsync();
        }

        public async Task<ExcelImportResultModel> ImportStockFromExcelAsync(Stream excelStream, int userId)
        {
            var result = new ExcelImportResultModel();
            using var workbook = new XLWorkbook(excelStream);
            var ws = workbook.Worksheets.First();

            foreach (var row in ws.RowsUsed().Skip(1))
            {
                var rowNum = row.RowNumber();
                var barcodeCell = row.Cell(2);
                var qtyCell = row.Cell(3);

                var article = row.Cell(1).GetString()?.Trim();
                var barcode = barcodeCell.DataType == XLDataType.Number
                    ? ((long)barcodeCell.GetDouble()).ToString()
                    : barcodeCell.GetString()?.Trim();

                var rowResult = new ExcelImportRowResult
                {
                    RowNumber = rowNum,
                    Article = article,
                    Barcode = barcode,
                };

                if (string.IsNullOrWhiteSpace(barcode))
                {
                    rowResult.IsSuccess = false;
                    rowResult.ErrorMessage = "ไม่พบ Barcode";
                    result.Rows.Add(rowResult);
                    continue;
                }

                decimal qty;
                if (qtyCell.DataType == XLDataType.Number)
                    qty = (decimal)qtyCell.GetDouble();
                else if (!decimal.TryParse(qtyCell.GetString()?.Trim(), out qty))
                {
                    rowResult.IsSuccess = false;
                    rowResult.ErrorMessage = "จำนวนไม่ถูกต้อง";
                    result.Rows.Add(rowResult);
                    continue;
                }

                if (qty <= 0)
                {
                    rowResult.IsSuccess = false;
                    rowResult.ErrorMessage = "จำนวนต้องมากกว่า 0";
                    result.Rows.Add(rowResult);
                    continue;
                }

                rowResult.Qty = qty;

                try
                {
                    await AddStockAsync(barcode, qty, userId, "Add By Excel");
                    rowResult.IsSuccess = true;
                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    rowResult.IsSuccess = false;
                    rowResult.ErrorMessage = ex.Message;
                }

                result.Rows.Add(rowResult);
            }

            return result;
        }
    }
}
