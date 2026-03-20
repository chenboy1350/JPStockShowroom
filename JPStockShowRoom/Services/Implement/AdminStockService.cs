using ClosedXML.Excel;
using JPStockShowRoom.Data.JPDbContext;
using JPStockShowRoom.Data.SWDbContext;
using JPStockShowRoom.Data.SWDbContext.Entities;
using JPStockShowRoom.Models;
using JPStockShowRoom.Services.Helper;
using JPStockShowRoom.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace JPStockShowRoom.Services.Implement
{
    public class AdminStockService(SWDbContext sWDbContext, JPDbContext jPDbContext, IPISService pISService, StockGroupKeyHelper groupKeyHelper) : IAdminStockService
    {
        private readonly SWDbContext _sWDbContext = sWDbContext;
        private readonly JPDbContext _jPDbContext = jPDbContext;
        private readonly IPISService _pISService = pISService;
        private readonly StockGroupKeyHelper _groupKeyHelper = groupKeyHelper;

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
                res = res.Where(r => r.Article != null && r.Article.Contains(article));

            if (!string.IsNullOrWhiteSpace(barcode))
                res = res.Where(r => r.Barcode != null && r.Barcode.Contains(barcode));

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

            var user = await _pISService.GetUser(new ReqUserModel { UserID = userId })
                ?? throw new InvalidOperationException($"ไม่พบผู้ใช้ ID: {userId}");

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

        public async Task DeleteAdminStockAsync(string groupKey)
        {
            var sources = await _groupKeyHelper.ResolveGroupKeyAsync(groupKey, isAdminAdded: true);
            if (sources.Count == 0)
                throw new KeyNotFoundException("ไม่พบสินค้า ADMIN สำหรับ groupKey นี้");

            _sWDbContext.Stock.RemoveRange(sources);
            await _sWDbContext.SaveChangesAsync();
        }
    }
}
