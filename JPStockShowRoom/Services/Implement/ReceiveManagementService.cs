using JPStockShowRoom.Data.JPDbContext;
using JPStockShowRoom.Data.SPDbContext;
using JPStockShowRoom.Data.SPDbContext.Entities;
using JPStockShowRoom.Data.SWDbContext;
using JPStockShowRoom.Data.SWDbContext.Entities;
using JPStockShowRoom.Models;
using JPStockShowRoom.Services.Interface;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using static JPStockShowRoom.Services.Helper.Enum;

namespace JPStockShowRoom.Services.Implement
{
    public class ReceiveManagementService(JPDbContext jPDbContext, SPDbContext sPDbContext, SWDbContext sWDbContext) : IReceiveManagementService
    {
        private readonly JPDbContext _jPDbContext = jPDbContext;
        private readonly SPDbContext _sPDbContext = sPDbContext;
        private readonly SWDbContext _sWDbContext = sWDbContext;

        public async Task<List<ReceivedListModel>> GetTopJPReceivedAsync(string? receiveNo, string? orderNo, string? lotNo)
        {
            var query =
                from a in _jPDbContext.Sphreceive
                select new
                {
                    a.ReceiveNo,
                    a.Mdate,
                    a.Mupdate,
                    TotalDetail = _jPDbContext.Spdreceive.Count(d => d.ReceiveNo == a.ReceiveNo)
                };

            if (!string.IsNullOrWhiteSpace(receiveNo))
            {
                query = query.Where(b => b.ReceiveNo.Contains(receiveNo));
            }

            if (!string.IsNullOrWhiteSpace(orderNo))
            {
                query = query.Where(b => _jPDbContext.Spdreceive
                    .Join(_jPDbContext.OrdLotno, sr => sr.Lotno, ol => ol.LotNo, (sr, ol) => new { sr, ol })
                    .Any(joined => joined.ol.OrderNo.Contains(orderNo) && joined.sr.ReceiveNo == b.ReceiveNo));

            }

            if (!string.IsNullOrWhiteSpace(lotNo))
            {
                query = query.Where(b => _jPDbContext.Spdreceive.Any(sr => sr.Lotno.Contains(lotNo) && sr.ReceiveNo == b.ReceiveNo));
            }

            var receives = await query.OrderByDescending(o => o.Mdate).Take(100).ToListAsync();

            var receiveNos = receives.Select(r => r.ReceiveNo).ToList();

            var swReceivedLookup = await _sWDbContext.Stock
                .Where(r => receiveNos.Contains(r.ReceiveNo))
                .GroupBy(r => r.ReceiveNo)
                .Select(g => new { ReceiveNo = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ReceiveNo, x => x.Count);

            var spReceivedLookup = await _sPDbContext.Received
                .Where(r => receiveNos.Contains(r.ReceiveNo) && r.IsReceived)
                .GroupBy(r => r.ReceiveNo)
                .Select(g => new { ReceiveNo = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ReceiveNo, x => x.Count);

            var result = receives.Select(r =>
            {
                var swCount = swReceivedLookup.TryGetValue(r.ReceiveNo, out var swc) ? swc : 0;
                var spCount = spReceivedLookup.TryGetValue(r.ReceiveNo, out var spc) ? spc : 0;
                var totalReceived = swCount + spCount;

                return new ReceivedListModel
                {
                    ReceiveNo = r.ReceiveNo,
                    Mdate = r.Mdate.ToString("dd MMMM yyyy", new CultureInfo("th-TH")),
                    IsReceived = r.Mupdate,
                    HasRevButNotAll = totalReceived > 0 && totalReceived < r.TotalDetail
                };
            }).ToList();

            return result;
        }

        public async Task<List<ReceivedListModel>> GetJPReceivedByReceiveNoAsync(string receiveNo, string? orderNo, string? lotNo)
        {
            var allReceived = await (
                from a in _jPDbContext.Spdreceive
                join c in _jPDbContext.OrdLotno on a.Lotno equals c.LotNo
                join d in _jPDbContext.OrdHorder on c.OrderNo equals d.OrderNo
                where a.ReceiveNo == receiveNo && !string.IsNullOrEmpty(a.Lotno)
                select new
                {
                    a.Id,
                    a.ReceiveNo,
                    a.Lotno,
                    a.Ttqty,
                    a.Ttwg,
                    a.Barcode,
                    c.EdesFn,
                    a.Article,
                    d.OrderNo,
                    c.ListNo,
                    d.CustCode
                }
            ).ToListAsync();

            if (!string.IsNullOrWhiteSpace(orderNo))
            {
                allReceived = [.. allReceived.Where(x => x.OrderNo.Contains(orderNo))];
            }

            if (!string.IsNullOrWhiteSpace(lotNo))
            {
                allReceived = [.. allReceived.Where(x => x.Lotno.Contains(lotNo))];
            }

            var existingIds = await _sWDbContext.Stock
                .Where(x => x.ReceiveNo == receiveNo)
                .Select(x => x.ReceiveId)
                .ToHashSetAsync();

            var result = allReceived.Select(x => new ReceivedListModel
            {
                ReceivedID = x.Id,
                ReceiveNo = x.ReceiveNo,
                CustCode = x.CustCode,
                LotNo = x.Lotno,
                TtQty = x.Ttqty,
                TtWg = (double)x.Ttwg,
                EdesFn = x.EdesFn,
                Article = x.Article,
                OrderNo = x.OrderNo,
                ListNo = x.ListNo,
                IsReceived = existingIds.Contains(x.Id)
            }).ToList();

            result = [.. result.OrderBy(x => x.LotNo).ThenBy(x => x.OrderNo).ThenBy(x => x.ListNo)];

            return result;
        }

        //BL
        public async Task UpdateLotItemsAsync(string receiveNo, List<string> orderNos, List<int> receiveIds, int userId)
        {
            if (receiveIds == null || receiveIds.Count == 0) return;

            var itemsToReceive = await (
                from a in _jPDbContext.Spdreceive
                join c in _jPDbContext.OrdLotno on a.Lotno equals c.LotNo
                join d in _jPDbContext.OrdHorder on c.OrderNo equals d.OrderNo
                join e in _jPDbContext.CpriceSale on new { a.Article, a.Barcode } equals new { e.Article, e.Barcode }
                join f in _jPDbContext.Cprofile on e.Article  equals f.Article

                where a.ReceiveNo == receiveNo && d.CustCode == "ZJP"
                select new
                {
                    a.Id,
                    a.ReceiveNo,
                    a.Lotno,
                    a.Ttqty,
                    a.Ttwg,
                    a.Barcode,
                    c.EdesFn,
                    a.Article,
                    d.OrderNo,
                    c.ListNo,
                    d.CustCode,
                    c.Unit,
                    f.EdesArt,
                    a.Billnumber,
                    e.ListGem,
                    e.Picture
                }
            ).ToListAsync();

            if (itemsToReceive.Count == 0) return;

            itemsToReceive = [.. itemsToReceive.Where(x => orderNos.Contains(x.OrderNo))];

            var existingReceiveIds = await _sWDbContext.Stock
                .Where(x => receiveIds.Contains(x.ReceiveId))
                .Select(x => x.ReceiveId)
                .ToListAsync();

            var newReceiveIds = receiveIds.Except(existingReceiveIds).ToList();
            if (newReceiveIds.Count == 0) return;

            var newItems = itemsToReceive.Where(x => newReceiveIds.Contains(x.Id)).ToList();

            var incomingBillNumbers = newItems.Select(x => x.Billnumber).Distinct().ToList();
            var incomingOrderNos = newItems.Select(x => x.OrderNo).Distinct().ToList();

            var repairingStocks = await _sWDbContext.Stock
                .Where(s => s.IsRepairing && incomingBillNumbers.Contains(s.BillNumber) && incomingOrderNos.Contains(s.OrderNo))
                .ToListAsync();

            var now = DateTime.Now;
            var stockEntitiesToAdd = new List<Stock>();

            foreach (var item in newItems)
            {
                var repairingStock = repairingStocks.FirstOrDefault(s =>
                    s.BillNumber == item.Billnumber && s.OrderNo == (item.OrderNo ?? ""));

                if (repairingStock != null)
                {
                    repairingStock.TtQty += item.Ttqty;
                    repairingStock.TtWg += (double)item.Ttwg;
                    repairingStock.IsRepairing = false;
                    repairingStock.UpdateDate = now;

                    await _sWDbContext.SaveChangesAsync();
                }
                else
                {
                    stockEntitiesToAdd.Add(new Stock
                    {
                        ReceiveId = item.Id,
                        ReceiveNo = item.ReceiveNo,
                        ReceiveFrom = (int)ReceiveFrom.BL,
                        CustCode = item.CustCode ?? "",
                        OrderNo = item.OrderNo ?? "",
                        LotNo = item.Lotno ?? "",
                        TempArticle = item.Article != null && item.Article.StartsWith('Z') ? item.Article : string.Empty,
                        Article = item.Article != null && !item.Article.StartsWith('Z') ? item.Article : string.Empty,
                        ListGem = item.ListGem,
                        Unit = item.Unit ?? "",
                        EdesFn = item.EdesFn,
                        EdesArt = item.EdesArt,
                        Barcode = item.Barcode ?? "",
                        BillNumber = item.Billnumber,
                        ImgPath = item.Picture ?? "",
                        TtQty = item.Ttqty,
                        TtWg = (double)item.Ttwg,
                        IsActive = true,
                        CreateDate = now,
                        UpdateDate = now,
                    });
                }
            }

            if (stockEntitiesToAdd.Count > 0)
                await _sWDbContext.Stock.AddRangeAsync(stockEntitiesToAdd);

            await _sWDbContext.SaveChangesAsync();
        }
    }
}
