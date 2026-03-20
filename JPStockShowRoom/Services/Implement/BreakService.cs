using JPStockShowRoom.Data.JPDbContext;
using JPStockShowRoom.Data.SPDbContext;
using JPStockShowRoom.Data.SPDbContext.Entities;
using JPStockShowRoom.Data.SWDbContext;
using JPStockShowRoom.Data.SWDbContext.Entities;
using JPStockShowRoom.Models;
using JPStockShowRoom.Services.Helper;
using JPStockShowRoom.Services.Interface;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace JPStockShowRoom.Services.Implement
{
    public class BreakService(SWDbContext sWDbContext, SPDbContext sPDbContext, JPDbContext jPDbContext, StockGroupKeyHelper groupKeyHelper) : IBreakService
    {
        private readonly SWDbContext _sWDbContext = sWDbContext;
        private readonly SPDbContext _sPDbContext = sPDbContext;
        private readonly JPDbContext _jPDbContext = jPDbContext;
        private readonly StockGroupKeyHelper _groupKeyHelper = groupKeyHelper;

        public async Task<List<LostAndRepairModel>> GetBreakAsync(BreakAndLostFilterModel breakAndLostFilterModel)
        {
            var query = _sWDbContext.BreakDetail.Where(b => b.IsActive).AsQueryable();

            if (!string.IsNullOrEmpty(breakAndLostFilterModel.GroupKey))
            {
                var filterStockIds = await _groupKeyHelper.ResolveGroupKeyToStockIdsAsync(breakAndLostFilterModel.GroupKey);
                query = query.Where(b => filterStockIds.Contains(b.StockId));
            }
            else if (breakAndLostFilterModel.ReceivedId.HasValue)
                query = query.Where(b => b.StockId == breakAndLostFilterModel.ReceivedId.Value);

            if (breakAndLostFilterModel.BreakIDs != null && breakAndLostFilterModel.BreakIDs.Length > 0)
                query = query.Where(b => breakAndLostFilterModel.BreakIDs.Contains(b.BreakDetailId));

            var breaks = await query.OrderByDescending(b => b.CreateDate).Take(100).ToListAsync();
            if (breaks.Count == 0) return [];

            return await MapBreakDetailsAsync(breaks);
        }

        public async Task<BaseResponseModel> AddBreakAsync(string groupKey, double breakQty, int breakDes)
        {
            if (string.IsNullOrEmpty(groupKey))
                throw new ArgumentException("groupKey ไม่ถูกต้อง", nameof(groupKey));

            if (breakQty <= 0)
                throw new ArgumentException("BreakQty ต้องมากกว่า 0", nameof(breakQty));

            var sources = await _groupKeyHelper.ResolveGroupKeyAsync(groupKey);
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
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return await GetBreakDescriptionsAsync();
        }

        public async Task PintedBreakReport(int[]? breakIDs)
        {
            var breaks = await _sWDbContext.BreakDetail
                .Where(b => breakIDs!.Contains(b.BreakDetailId) && b.IsActive)
                .ToListAsync();

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

        private async Task UpdateJobBillSendStockAndSpdreceive(int billnumber, int id, string receiveNo, decimal ttQty, decimal ttWg)
        {
            using var transaction = await _jPDbContext.Database.BeginTransactionAsync();
            try
            {
                var spdreceive = await _jPDbContext.Spdreceive
                    .OrderByDescending(o => o.Ttqty)
                    .FirstOrDefaultAsync(o => o.Billnumber == billnumber && o.Id == id && o.ReceiveNo == receiveNo);

                if (spdreceive != null)
                {
                    spdreceive.Ttqty = ttQty;
                    spdreceive.Ttwg = ttWg;
                }
                else
                {
                    var a = _sPDbContext.Received.Where(o => o.ReceiveId == id && o.BillNumber == billnumber).FirstOrDefault();
                    if (a != null)
                    {
                        spdreceive = await _jPDbContext.Spdreceive
                            .OrderByDescending(o => o.Ttqty)
                            .FirstOrDefaultAsync(o => o.Billnumber == a.BillNumber && o.ReceiveNo == a.ReceiveNo);
                        if (spdreceive != null)
                        {
                            spdreceive.Ttqty = ttQty;
                            spdreceive.Ttwg = ttWg;
                        }
                    }
                }

                var jobBillSendStock = await _jPDbContext.JobBillSendStock
                    .FirstOrDefaultAsync(o => o.Billnumber == billnumber);
                if (jobBillSendStock != null)
                {
                    jobBillSendStock.Ttqty = ttQty;
                    jobBillSendStock.Ttwg = ttWg;
                }

                await _jPDbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task<List<LostAndRepairModel>> MapBreakDetailsAsync(List<BreakDetail> details)
        {
            if (details.Count == 0) return [];
            var stockIds = details.Select(d => d.StockId).Distinct().ToList();
            var stocks = await _sWDbContext.Stock
                .Where(s => stockIds.Contains(s.StockId))
                .ToDictionaryAsync(s => s.StockId);
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
                    TtQty = stock?.TtQty ?? 0,
                    TtWg = Math.Round(stock?.TtWg ?? 0, 2),
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
    }
}
