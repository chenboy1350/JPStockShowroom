using JPStockShowRoom.Data.SWDbContext;
using JPStockShowRoom.Data.SWDbContext.Entities;
using JPStockShowRoom.Models;
using JPStockShowRoom.Services.Helper;
using JPStockShowRoom.Services.Interface;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace JPStockShowRoom.Services.Implement
{
    public class BorrowService(SWDbContext sWDbContext, StockGroupKeyHelper groupKeyHelper) : IBorrowService
    {
        private readonly SWDbContext _sWDbContext = sWDbContext;
        private readonly StockGroupKeyHelper _groupKeyHelper = groupKeyHelper;

        public async Task BorrowFromStockAsync(string groupKey, decimal borrowQty, int borrowedBy)
        {
            if (int.TryParse(groupKey, out int singleStockId))
            {
                await BorrowFromSingleStockAsync(singleStockId, borrowQty, borrowedBy);
                return;
            }

            var sources = await _groupKeyHelper.ResolveGroupKeyAsync(groupKey);
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

            var borrowable = stock.TtQty - currentBorrowed;
            if (borrowQty <= 0 || borrowQty > borrowable) return;

            var now = DateTime.Now;

            var inTrayQty = await _sWDbContext.TrayItem
                .Where(ti => ti.IsActive && ti.StockId == stockId)
                .SumAsync(ti => ti.Qty);
            var freeQty = stock.TtQty - inTrayQty - currentBorrowed;

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
                var stockIds = await _groupKeyHelper.ResolveGroupKeyToStockIdsAsync(groupKey);
                query = query.Where(b => stockIds.Contains(b.StockId));
            }

            var borrows = await query.ToListAsync();
            return await MapBorrowDetailsAsync(borrows);
        }

        public async Task<List<BorrowModel>> GetBorrowsByStockIdAsync(string groupKey)
        {
            List<int> stockIds;
            if (int.TryParse(groupKey, out int singleId))
                stockIds = [singleId];
            else
                stockIds = await _groupKeyHelper.ResolveGroupKeyToStockIdsAsync(groupKey);

            var details = await _sWDbContext.BorrowDetail
                .Where(d => !d.IsReturned && stockIds.Contains(d.StockId))
                .ToListAsync();

            return await MapBorrowDetailsAsync(details);
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

        private async Task<List<BorrowModel>> MapBorrowDetailsAsync(List<BorrowDetail> details)
        {
            var stockIds = details.Select(d => d.StockId).Distinct().ToList();
            var stocks = await _sWDbContext.Stock
                .Where(s => stockIds.Contains(s.StockId))
                .ToDictionaryAsync(s => s.StockId);

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
    }
}
