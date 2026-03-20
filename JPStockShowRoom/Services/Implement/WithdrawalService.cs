using JPStockShowRoom.Data.SWDbContext;
using JPStockShowRoom.Data.SWDbContext.Entities;
using JPStockShowRoom.Models;
using JPStockShowRoom.Services.Helper;
using JPStockShowRoom.Services.Interface;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace JPStockShowRoom.Services.Implement
{
    public class WithdrawalService(SWDbContext sWDbContext, StockGroupKeyHelper groupKeyHelper) : IWithdrawalService
    {
        private readonly SWDbContext _sWDbContext = sWDbContext;
        private readonly StockGroupKeyHelper _groupKeyHelper = groupKeyHelper;

        public async Task WithdrawFromStockAsync(string groupKey, decimal withdrawQty, string? remark, int userId, bool isAdminAdded = false)
        {
            if (int.TryParse(groupKey, out int singleStockId))
            {
                await WithdrawFromSingleStockAsync(singleStockId, withdrawQty, remark, userId);
                return;
            }

            var sources = await _groupKeyHelper.ResolveGroupKeyAsync(groupKey, isAdminAdded);
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

            return withdrawals.Select(w =>
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
                    nextSeq = lastSeq + 1;
            }

            return $"{basePrefix}{nextSeq:D4}";
        }
    }
}
