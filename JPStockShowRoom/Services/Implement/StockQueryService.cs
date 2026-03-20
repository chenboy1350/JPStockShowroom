using JPStockShowRoom.Data.JPDbContext;
using JPStockShowRoom.Data.SPDbContext;
using JPStockShowRoom.Data.SWDbContext;
using JPStockShowRoom.Models;
using JPStockShowRoom.Services.Helper;
using JPStockShowRoom.Services.Interface;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace JPStockShowRoom.Services.Implement
{
    public class StockQueryService(SWDbContext sWDbContext, SPDbContext sPDbContext, JPDbContext jPDbContext) : IStockQueryService
    {
        private readonly SWDbContext _sWDbContext = sWDbContext;
        private readonly SPDbContext _sPDbContext = sPDbContext;
        private readonly JPDbContext _jPDbContext = jPDbContext;

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
                    var groupKey = StockGroupKeyHelper.BuildGroupKey(g.Key.Article, g.Key.Barcode, g.Key.ListGem, g.Key.EdesFn);

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

            return stocks.Select(s =>
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

        public async Task<List<string>> GetProductTypesAsync()
        {
            return await _sPDbContext.ProductType
                .Where(p => p.IsActive && p.Name != null && p.Name != "")
                .OrderBy(p => p.Name)
                .Select(p => p.Name!)
                .ToListAsync();
        }

        public async Task SyncArticlesAsync()
        {
            var lots = from a in _sWDbContext.Stock
                       where a.TempArticle != null && a.Article == null
                       select a.LotNo;

            var lotNos = await lots.Distinct().ToListAsync();
            foreach (var lotNo in lotNos)
                await UpdateArticleAsync(lotNo);
        }

        private async Task UpdateArticleAsync(string lotNo)
        {
            if (string.IsNullOrWhiteSpace(lotNo)) return;

            var baseData = await (
                from a in _jPDbContext.OrdLotno.AsNoTracking()
                join c in _jPDbContext.CpriceSale.AsNoTracking()
                    on a.Barcode equals c.Barcode into bc
                from c in bc.DefaultIfEmpty()
                where a.LotNo == lotNo && !string.IsNullOrEmpty(a.LotNo)
                orderby a.ListNo
                select new { a.LotNo, Article = c.Article ?? string.Empty }
            ).ToListAsync();

            if (baseData.Count == 0) return;

            var spLots = await _sWDbContext.Stock.Where(x => x.LotNo == lotNo).ToListAsync();
            var spLotDict = spLots.ToDictionary(x => x.LotNo);

            foreach (var item in baseData)
            {
                if (!spLotDict.TryGetValue(item.LotNo, out var spLot)) continue;
                if (spLot.Article != item.Article)
                    spLot.Article = item.Article;
            }

            await _sWDbContext.SaveChangesAsync();
        }
    }
}
