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

            var receives = await query.OrderByDescending(o => o.Mdate).Take(300).ToListAsync();

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

        public async Task<List<ReceivedListModel>> GetTopSPReceivedAsync(string? receiveNo, string? lotNo)
        {
            var query = from a in _sPDbContext.SendShowroom
                        where a.IsActive
                        select new
                        {
                            a.Doc,
                            a.CreateDate,
                            a.IsReceived,
                            TotalDetail = _sPDbContext.SendShowroomDetail.Count(d => d.Doc == a.Doc && d.IsActive)
                        };

            if (!string.IsNullOrWhiteSpace(receiveNo))
                query = query.Where(b => b.Doc.Contains(receiveNo));

            if (!string.IsNullOrWhiteSpace(lotNo))
                query = query.Where(b => _sPDbContext.SendShowroomDetail.Any(d => d.Doc == b.Doc && d.LotNo.Contains(lotNo)));

            var receives = await query.OrderByDescending(o => o.CreateDate).Take(100).ToListAsync();

            if (receives.Count == 0) return [];

            var docs = receives.Select(r => r.Doc).ToList();

            var swReceivedLookup = await _sWDbContext.Stock
                .Where(s => docs.Contains(s.ReceiveNo))
                .GroupBy(s => s.ReceiveNo)
                .Select(g => new { ReceiveNo = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ReceiveNo, x => x.Count);

            return receives.Select(r =>
            {
                var swCount = swReceivedLookup.TryGetValue(r.Doc, out var swc) ? swc : 0;
                return new ReceivedListModel
                {
                    ReceiveNo = r.Doc,
                    Mdate = r.CreateDate?.ToString("dd MMMM yyyy", new CultureInfo("th-TH")) ?? string.Empty,
                    IsReceived = r.IsReceived,
                    HasRevButNotAll = swCount > 0 && swCount < r.TotalDetail
                };
            }).ToList();
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

            var swExistingIds = await _sWDbContext.Stock
                .Where(x => x.ReceiveNo == receiveNo)
                .Select(x => x.ReceiveId)
                .ToHashSetAsync();

            var spExistingIds = await _sPDbContext.Received
                .Where(x => x.ReceiveNo == receiveNo && x.IsReceived)
                .Select(x => x.ReceiveId)
                .ToHashSetAsync();

            var existingIds = swExistingIds.Union(spExistingIds).ToHashSet();

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
                IsReceived = existingIds.Contains(x.Id),
                IsInStock = swExistingIds.Contains(x.Id)
            }).ToList();

            result = [.. result.OrderBy(x => x.LotNo).ThenBy(x => x.OrderNo).ThenBy(x => x.ListNo)];

            return result;
        }

        public async Task<List<ReceivedListModel>> GetSPReceivedByReceiveNoAsync(string receiveNo, string? lotNo)
        {
            var allReceived = await (
                from a in _sPDbContext.SendShowroomDetail
                join b in _sPDbContext.Lot on a.LotNo equals b.LotNo
                join c in _sPDbContext.Order on b.OrderNo equals c.OrderNo
                where a.Doc == receiveNo && a.IsActive
                select new
                {
                    a.SendShowroomId,
                    a.Doc,
                    a.LotNo,
                    a.TtQty,
                    a.TtWg,
                    b.EdesFn,
                    b.Article,
                    b.OrderNo,
                    b.ListNo,
                    b.Barcode,
                    b.ImgPath,
                    CustCode = c.CustCode ?? string.Empty
                }
            ).ToListAsync();

            if (!string.IsNullOrWhiteSpace(lotNo))
                allReceived = [.. allReceived.Where(x => x.LotNo.Contains(lotNo))];

            var existingIds = await _sWDbContext.Stock
                .Where(x => x.ReceiveNo == receiveNo)
                .Select(x => x.ReceiveId)
                .ToHashSetAsync();

            var result = allReceived.Select(x => new ReceivedListModel
            {
                ReceivedID = x.SendShowroomId,
                ReceiveNo = x.Doc,
                CustCode = x.CustCode,
                LotNo = x.LotNo,
                TtQty = x.TtQty,
                TtWg = x.TtWg,
                EdesFn = x.EdesFn ?? string.Empty,
                Article = x.Article ?? string.Empty,
                OrderNo = x.OrderNo,
                ListNo = x.ListNo,
                Barcode = x.Barcode ?? string.Empty,
                IsReceived = existingIds.Contains(x.SendShowroomId)
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
                join f in _jPDbContext.Cprofile on e.Article equals f.Article

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
                    f.LinkArticle,
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

            // Batch-load real article data for registered items (LinkArticle is non-empty and non-Z)
            var registeredArticles = newItems
                .Where(x => !string.IsNullOrEmpty(x.LinkArticle) && !x.LinkArticle.StartsWith('Z'))
                .Select(x => x.LinkArticle)
                .Distinct()
                .ToList();

            var realCprofileEdesArtMap = new Dictionary<string, string>();
            var realCpriceSaleByLinkBar = new Dictionary<string, (string Barcode, string ListGem, string Picture)>();

            if (registeredArticles.Count > 0)
            {
                // หา CProfile ทีละ Article (PK lookup) หลีกเลี่ยง EF Core CTE issue
                foreach (var article in registeredArticles)
                {
                    var prof = await _jPDbContext.Cprofile.FindAsync(article);
                    if (prof != null)
                        realCprofileEdesArtMap[article] = prof.EdesArt;
                }

                // หา CPriceSale ทีละคู่ (LinkArticle + ZBarcode) หลีกเลี่ยง EF Core CTE issue
                var registeredPairs = newItems
                    .Where(x => !string.IsNullOrEmpty(x.LinkArticle) && !x.LinkArticle.StartsWith('Z')
                                && !string.IsNullOrEmpty(x.Barcode))
                    .Select(x => new { LinkArticle = x.LinkArticle, ZBarcode = x.Barcode! })
                    .DistinctBy(x => x.ZBarcode)
                    .ToList();

                foreach (var pair in registeredPairs)
                {
                    var cpItem = await _jPDbContext.CpriceSale
                        .Where(p => p.Article == pair.LinkArticle)
                        .Select(p => new { p.Barcode, p.ListGem, p.Picture })
                        .FirstOrDefaultAsync();

                    if (cpItem != null)
                        realCpriceSaleByLinkBar[pair.ZBarcode] = (cpItem.Barcode, cpItem.ListGem, cpItem.Picture);
                }
            }

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
                    // Registered item: LinkArticle is non-empty and non-Z (real article exists)
                    var isRegistered = !string.IsNullOrEmpty(item.LinkArticle) && !item.LinkArticle.StartsWith('Z');

                    string stockArticle;
                    string stockTempArticle;
                    string stockBarcode;
                    string? stockListGem;
                    string stockEdesArt;
                    string stockImgPath;

                    if (isRegistered && realCprofileEdesArtMap.TryGetValue(item.LinkArticle, out var realEdesArt))
                    {
                        // Use real article data: Article = real, TempArticle = Z code
                        stockArticle = item.LinkArticle;
                        stockTempArticle = item.Article ?? string.Empty;
                        stockEdesArt = realEdesArt;

                        if (realCpriceSaleByLinkBar.TryGetValue(item.Barcode ?? string.Empty, out var realCprice))
                        {
                            stockBarcode = realCprice.Barcode;
                            stockListGem = realCprice.ListGem;
                            stockImgPath = realCprice.Picture;
                        }
                        else
                        {
                            stockBarcode = item.Barcode ?? string.Empty;
                            stockListGem = item.ListGem;
                            stockImgPath = item.Picture;
                        }
                    }
                    else
                    {
                        // Not yet registered or no real CProfile found: keep original data
                        stockArticle = item.Article != null && !item.Article.StartsWith('Z') ? item.Article : string.Empty;
                        stockTempArticle = !string.IsNullOrEmpty(item.LinkArticle)
                            ? item.LinkArticle
                            : (item.Article != null && item.Article.StartsWith('Z') ? item.Article : string.Empty);
                        stockBarcode = item.Barcode ?? string.Empty;
                        stockListGem = item.ListGem;
                        stockEdesArt = item.EdesArt;
                        stockImgPath = item.Picture;
                    }

                    stockEntitiesToAdd.Add(new Stock
                    {
                        ReceiveId = item.Id,
                        ReceiveNo = item.ReceiveNo,
                        CustCode = item.CustCode ?? string.Empty,
                        OrderNo = item.OrderNo ?? string.Empty,
                        LotNo = item.Lotno ?? string.Empty,
                        Article = stockArticle,
                        TempArticle = stockTempArticle,
                        Barcode = stockBarcode,
                        ListGem = stockListGem,
                        Unit = item.Unit ?? string.Empty,
                        EdesFn = item.EdesFn,
                        EdesArt = stockEdesArt,
                        BillNumber = item.Billnumber,
                        ImgPath = stockImgPath,
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

        //SP
        public async Task UpdateSPLotItemsAsync(string receiveNo, List<int> receiveIds, int userId)
        {
            if (receiveIds == null || receiveIds.Count == 0) return;

            var itemsToReceive = await (
                from a in _sPDbContext.SendShowroomDetail
                join b in _sPDbContext.Lot on a.LotNo equals b.LotNo
                join c in _sPDbContext.Order on b.OrderNo equals c.OrderNo
                where a.Doc == receiveNo && a.IsActive
                select new
                {
                    a.SendShowroomId,
                    a.Doc,
                    a.LotNo,
                    a.TtQty,
                    a.TtWg,
                    b.EdesFn,
                    b.EdesArt,
                    b.Article,
                    b.OrderNo,
                    b.ListNo,
                    b.Barcode,
                    b.ImgPath,
                    b.Unit,
                    CustCode = c.CustCode ?? string.Empty
                }
            ).ToListAsync();

            if (itemsToReceive.Count == 0) return;

            itemsToReceive = [.. itemsToReceive.Where(x => receiveIds.Contains(x.SendShowroomId))];

            var existingReceiveIds = await _sWDbContext.Stock
                .Where(x => receiveIds.Contains(x.ReceiveId))
                .Select(x => x.ReceiveId)
                .ToListAsync();

            var newReceiveIds = receiveIds.Except(existingReceiveIds).ToList();
            if (newReceiveIds.Count == 0) return;

            var newItems = itemsToReceive.Where(x => newReceiveIds.Contains(x.SendShowroomId)).ToList();

            // หา BillNumber จาก Received โดยเอา BillNumber ที่มี TtQty มากที่สุดของแต่ละ LotNo
            var lotNos = newItems.Select(x => x.LotNo).Distinct().ToList();
            var receivedRecords = await _sPDbContext.Received
                .Where(r => lotNos.Contains(r.LotNo) && r.IsActive)
                .ToListAsync();

            var bestRecordByLot = receivedRecords
                .GroupBy(r => r.LotNo)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(r => r.TtQty ?? 0).First()
                );

            var billNumberByLot = bestRecordByLot.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.BillNumber);
            var receiveIdByLot = bestRecordByLot.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ReceiveId);

            // หา ListGem จาก CpriceSale โดย lookup ทีละ Barcode (PK) หลีกเลี่ยง EF Core CTE issue
            var barcodes = newItems
                .Select(x => x.Barcode ?? string.Empty)
                .Where(b => b.Length > 0)
                .Distinct()
                .ToList();
            var listGemByBarcode = new Dictionary<string, string>();
            foreach (var barcode in barcodes)
            {
                var cpItem = await _jPDbContext.CpriceSale.FindAsync(barcode);
                listGemByBarcode[barcode] = cpItem?.ListGem ?? string.Empty;
            }

            var incomingBillNumbers = billNumberByLot.Values.Distinct().ToList();
            var incomingOrderNos = newItems.Select(x => x.OrderNo).Distinct().ToList();

            var repairingStocks = await _sWDbContext.Stock
                .Where(s => s.IsRepairing && incomingBillNumbers.Contains(s.BillNumber) && incomingOrderNos.Contains(s.OrderNo))
                .ToListAsync();

            var now = DateTime.Now;
            var stockEntitiesToAdd = new List<Stock>();

            foreach (var item in newItems)
            {
                var billNumber = billNumberByLot.GetValueOrDefault(item.LotNo);
                var repairingStock = repairingStocks.FirstOrDefault(s =>
                    s.BillNumber == billNumber && s.OrderNo == item.OrderNo);

                if (repairingStock != null)
                {
                    repairingStock.TtQty += item.TtQty;
                    repairingStock.TtWg += item.TtWg;
                    repairingStock.IsRepairing = false;
                    repairingStock.UpdateDate = now;
                }
                else
                {
                    stockEntitiesToAdd.Add(new Stock
                    {
                        ReceiveId = item.SendShowroomId,
                        ReceiveNo = item.Doc,
                        CustCode = item.CustCode,
                        OrderNo = item.OrderNo,
                        LotNo = item.LotNo,
                        TempArticle = item.Article != null && item.Article.StartsWith('Z') ? item.Article : string.Empty,
                        Article = item.Article != null && !item.Article.StartsWith('Z') ? item.Article : string.Empty,
                        Unit = item.Unit ?? string.Empty,
                        EdesFn = item.EdesFn ?? string.Empty,
                        EdesArt = item.EdesArt ?? string.Empty,
                        Barcode = item.Barcode ?? string.Empty,
                        ListGem = listGemByBarcode.GetValueOrDefault(item.Barcode ?? string.Empty),
                        BillNumber = billNumber,
                        ImgPath = item.ImgPath ?? string.Empty,
                        TtQty = item.TtQty,
                        TtWg = item.TtWg,
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

        public async Task<List<ConvertedArticleModel>> ConvertZArticlesAsync()
        {
            // หา Stock ที่ยังไม่มี Article (Z article ที่ยังไม่ได้แปลง)
            var zStocks = await _sWDbContext.Stock
                .Where(s => s.IsActive
                    && (s.Article == null || s.Article == string.Empty)
                    && s.TempArticle != null && s.TempArticle != string.Empty)
                .ToListAsync();

            if (zStocks.Count == 0) return [];

            var results = new List<ConvertedArticleModel>();
            var now = DateTime.Now;

            foreach (var stock in zStocks)
            {
                // หา CProfile ของ Z article เพื่อดู LinkArticle
                var zProfile = await _jPDbContext.Cprofile.FindAsync(stock.TempArticle);
                if (zProfile == null) continue;

                // ถ้า LinkArticle ว่างหรือยังเป็น Z แปลว่ายังไม่ได้ลงทะเบียน ข้ามไป
                if (string.IsNullOrEmpty(zProfile.LinkArticle) || zProfile.LinkArticle.StartsWith('Z'))
                    continue;

                var realArticle = zProfile.LinkArticle;

                // หา CProfile ของ Article จริง เพื่อเอา EdesArt
                var realProfile = await _jPDbContext.Cprofile.FindAsync(realArticle);
                if (realProfile == null) continue;

                // หา CPriceSale ของ Article จริง โดย match LinkBar = Z barcode ปัจจุบัน
                var oldBarcode = stock.Barcode;
                var realCprice = await _jPDbContext.CpriceSale
                    .Where(p => p.Article == realArticle && p.LinkBar == stock.Barcode)
                    .Select(p => new { p.Barcode, p.ListGem, p.Picture })
                    .FirstOrDefaultAsync();

                stock.Article = realArticle;
                stock.EdesArt = realProfile.EdesArt;

                if (realCprice != null)
                {
                    stock.Barcode = realCprice.Barcode;
                    stock.ListGem = realCprice.ListGem;
                    stock.ImgPath = realCprice.Picture;
                }

                stock.UpdateDate = now;

                results.Add(new ConvertedArticleModel
                {
                    ZArticle = stock.TempArticle ?? string.Empty,
                    RealArticle = realArticle,
                    OldBarcode = oldBarcode,
                    NewBarcode = realCprice?.Barcode ?? oldBarcode,
                    EdesArt = realProfile.EdesArt,
                    OrderNo = stock.OrderNo,
                    LotNo = stock.LotNo
                });
            }

            if (results.Count > 0)
                await _sWDbContext.SaveChangesAsync();

            return results;
        }

        public async Task CancelLotItemsAsync(string receiveNo, List<string> orderNos, List<int> receiveIds, int userId)
        {
            if (receiveIds == null || receiveIds.Count == 0) return;

            var stocks = await _sWDbContext.Stock
                .Where(s => s.ReceiveNo == receiveNo && receiveIds.Contains(s.ReceiveId))
                .ToListAsync();

            if (stocks.Count == 0) return;

            var stockIds = stocks.Select(s => s.StockId).ToList();

            var blockedByTray = await _sWDbContext.TrayItem
                .Where(t => t.IsActive && stockIds.Contains(t.StockId))
                .Select(t => t.StockId)
                .Distinct()
                .ToListAsync();

            var blockedByBorrow = await _sWDbContext.BorrowDetail
                .Where(b => !b.IsReturned && stockIds.Contains(b.StockId))
                .Select(b => b.StockId)
                .Distinct()
                .ToListAsync();

            var blockedByWithdrawal = await _sWDbContext.WithdrawalDetail
                .Where(w => w.IsActive && stockIds.Contains(w.StockId))
                .Select(w => w.StockId)
                .Distinct()
                .ToListAsync();

            var blockedStockIds = new HashSet<int>(blockedByTray.Concat(blockedByBorrow).Concat(blockedByWithdrawal));

            if (blockedStockIds.Count > 0)
            {
                var blockedLotNos = stocks
                    .Where(s => blockedStockIds.Contains(s.StockId))
                    .Select(s => s.LotNo)
                    .Distinct()
                    .ToList();

                throw new InvalidOperationException(
                    $"ไม่สามารถยกเลิกได้ เนื่องจากรายการต่อไปนี้อยู่ในถาด/มีการเบิก/มีการยืม: {string.Join(", ", blockedLotNos)}");
            }

            _sWDbContext.Stock.RemoveRange(stocks);
            await _sWDbContext.SaveChangesAsync();
        }

        public async Task CancelSPLotItemsAsync(string receiveNo, List<int> receiveIds, int userId) => await CancelLotItemsAsync(receiveNo, [], receiveIds, userId);

        public async Task UpdateSPReceiveHeaderStatusAsync(string receiveNo)
        {
            using var transaction = await _sPDbContext.Database.BeginTransactionAsync();
            try
            {
                var header = await _sPDbContext.SendShowroom
                    .FirstOrDefaultAsync(h => h.Doc == receiveNo)
                    ?? throw new InvalidOperationException($"ไม่พบใบรับ {receiveNo}");

                var detailIds = await _sPDbContext.SendShowroomDetail
                    .Where(d => d.Doc == receiveNo && d.IsActive)
                    .Select(d => d.SendShowroomId)
                    .ToListAsync();

                if (detailIds.Count == 0) return;

                var receivedIdSet = await _sWDbContext.Stock
                    .Where(r => r.ReceiveNo == receiveNo)
                    .Select(r => r.ReceiveId)
                    .ToHashSetAsync();

                bool isComplete = detailIds.All(id => receivedIdSet.Contains(id));

                if (header.IsReceived != isComplete)
                {
                    header.IsReceived = isComplete;
                    await _sPDbContext.SaveChangesAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task UpdateJPReceiveHeaderStatusAsync(string receiveNo)
        {
            using var transaction = await _jPDbContext.Database.BeginTransactionAsync();
            try
            {
                var header = await _jPDbContext.Sphreceive
                    .Include(h => h.Spdreceive)
                    .FirstOrDefaultAsync(h => h.ReceiveNo == receiveNo)
                    ?? throw new InvalidOperationException($"ไม่พบใบรับ {receiveNo}");

                var detailIds = header.Spdreceive
                    .Select(d => d.Id)
                    .ToList();

                if (detailIds.Count == 0)
                    throw new InvalidOperationException($"ใบรับ {receiveNo} ไม่มีรายการ detail");

                var receivedIdSet = await _sPDbContext.Received
                    .Where(r =>
                        r.ReceiveNo == receiveNo &&
                        r.IsReceived &&
                        r.IsActive)
                    .Select(r => r.ReceiveId)
                    .ToHashSetAsync();

                var stockIdSet = await _sWDbContext.Stock
                    .Where(r => r.ReceiveNo == receiveNo && r.IsActive)
                    .Select(r => r.ReceiveId)
                    .ToHashSetAsync();

                receivedIdSet.UnionWith(stockIdSet);

                bool isComplete = detailIds.All(id => receivedIdSet.Contains(id));

                if (header.Mupdate != isComplete)
                {
                    header.Mupdate = isComplete;
                    await _jPDbContext.SaveChangesAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

        }

        public async Task SyncAllReceiveHeaderStatusAsync()
        {
            var headers = await _jPDbContext.Sphreceive
                .Include(h => h.Spdreceive)
                .Where(h => h.Mupdate != true && h.Mdate.Year >= 2025)
                .ToListAsync();

            foreach (var header in headers)
            {
                var detailIds = header.Spdreceive
                    .Select(d => d.Id)
                    .ToList();

                if (detailIds.Count == 0) continue;

                var receivedIdSet = await _sPDbContext.Received
                    .Where(r => r.ReceiveNo == header.ReceiveNo && r.IsReceived && r.IsActive)
                    .Select(r => r.ReceiveId)
                    .ToHashSetAsync();

                var stockIdSet = await _sWDbContext.Stock
                    .Where(r => r.ReceiveNo == header.ReceiveNo && r.IsActive)
                    .Select(r => r.ReceiveId)
                    .ToHashSetAsync();

                receivedIdSet.UnionWith(stockIdSet);

                bool isComplete = detailIds.All(id => receivedIdSet.Contains(id));

                if (isComplete)
                {
                    header.Mupdate = true;
                }
            }

            await _jPDbContext.SaveChangesAsync();
        }
    }
}
