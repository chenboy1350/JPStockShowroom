using JPStockShowRoom.Models;
using JPStockShowRoom.Services.Helper;
using JPStockShowRoom.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace JPStockShowRoom.Controllers
{
    public class HomeController : Controller
    {
        private readonly IReceiveManagementService _receiveManagementService;
        private readonly IStockQueryService _stockQueryService;
        private readonly ITrayService _trayService;
        private readonly IBorrowService _borrowService;
        private readonly IWithdrawalService _withdrawalService;
        private readonly IBreakService _breakService;
        private readonly IAdminStockService _adminStockService;
        private readonly IWebHostEnvironment _env;
        private readonly AppSettingModel _appSettings;
        private readonly IPISService _pISService;
        private readonly IConfiguration _configuration;
        private readonly Serilog.ILogger _logger;
        private readonly IPermissionManagement _permissionManagement;
        private readonly IReportService _reportService;

        public HomeController(
            IReceiveManagementService receiveManagementService,
            IStockQueryService stockQueryService,
            ITrayService trayService,
            IBorrowService borrowService,
            IWithdrawalService withdrawalService,
            IBreakService breakService,
            IAdminStockService adminStockService,
            IWebHostEnvironment webHostEnvironment,
            IOptions<AppSettingModel> appSettings,
            IPISService pISService,
            IConfiguration configuration,
            Serilog.ILogger logger,
            IPermissionManagement permissionManagement,
            IReportService reportService)
        {
            _receiveManagementService = receiveManagementService;
            _stockQueryService = stockQueryService;
            _trayService = trayService;
            _borrowService = borrowService;
            _withdrawalService = withdrawalService;
            _breakService = breakService;
            _adminStockService = adminStockService;
            _env = webHostEnvironment;
            _appSettings = appSettings.Value;
            _pISService = pISService;
            _configuration = configuration;
            _logger = logger;
            _permissionManagement = permissionManagement;
            _reportService = reportService;
        }

        [Authorize]
        public IActionResult Index()
        {
            ViewBag.AppVersion = _appSettings.AppVersion;
            return View();
        }

        [Authorize]
        public async Task<IActionResult> ReceiveManagement()
        {
            var result = await _receiveManagementService.GetTopJPReceivedAsync(null, null, null);
            return PartialView("~/Views/Partial/_ReceiveManagment.cshtml", result);
        }

        #region Receive Management

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetReceiveList(string? receiveNo, string? lotNo)
        {
            var result = await _receiveManagementService.GetTopJPReceivedAsync(receiveNo, null, lotNo);
            return Json(result);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetReceiveRow(string receiveNo)
        {
            var result = await _receiveManagementService.GetTopJPReceivedAsync(receiveNo, null, null);
            return Json(result.FirstOrDefault());
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetSPReceiveList(string? receiveNo, string? lotNo)
        {
            var result = await _receiveManagementService.GetTopSPReceivedAsync(receiveNo, lotNo);
            return Json(result);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetSPReceiveRow(string receiveNo)
        {
            var result = await _receiveManagementService.GetTopSPReceivedAsync(receiveNo, null);
            return Json(result.FirstOrDefault());
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> ImportReceiveNo(string receiveNo, string? lotNo)
        {
            var result = await _receiveManagementService.GetJPReceivedByReceiveNoAsync(receiveNo, null, lotNo);
            return Json(result);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> ImportSPReceiveNo(string receiveNo, string? lotNo)
        {
            var result = await _receiveManagementService.GetSPReceivedByReceiveNoAsync(receiveNo, lotNo);
            return Json(result);
        }

        [Authorize]
        [HttpPatch]
        public async Task<IActionResult> UpdateLotItems([FromForm] string receiveNo, [FromForm] List<string> orderNos, [FromForm] List<int> receiveIds)
        {
            var userId = User.GetUserId() ?? 0;
            await _receiveManagementService.UpdateLotItemsAsync(receiveNo, orderNos, receiveIds, userId);
            await _receiveManagementService.UpdateJPReceiveHeaderStatusAsync(receiveNo);
            return Ok();
        }

        [Authorize]
        [HttpPatch]
        public async Task<IActionResult> UpdateSPLotItems([FromForm] string receiveNo, [FromForm] List<int> receiveIds)
        {
            var userId = User.GetUserId() ?? 0;
            await _receiveManagementService.UpdateSPLotItemsAsync(receiveNo, receiveIds, userId);
            await _receiveManagementService.UpdateSPReceiveHeaderStatusAsync(receiveNo);
            return Ok();
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> CancelImportReceiveNo(string receiveNo, string? lotNo)
        {
            var result = await _receiveManagementService.GetJPReceivedByReceiveNoAsync(receiveNo, null, lotNo);
            return Json(result);
        }

        [Authorize]
        [HttpPatch]
        public async Task<IActionResult> CancelUpdateLotItems([FromForm] string receiveNo, [FromForm] List<string> orderNos, [FromForm] List<int> receiveIds)
        {
            try
            {
                var userId = User.GetUserId() ?? 0;
                await _receiveManagementService.CancelLotItemsAsync(receiveNo, orderNos, receiveIds, userId);
                await _receiveManagementService.UpdateJPReceiveHeaderStatusAsync(receiveNo);
                return Ok();
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(422, new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> CancelImportSPReceiveNo(string receiveNo, string? lotNo)
        {
            var result = await _receiveManagementService.GetSPReceivedByReceiveNoAsync(receiveNo, lotNo);
            return Json(result);
        }

        [Authorize]
        [HttpPatch]
        public async Task<IActionResult> CancelUpdateSPLotItems([FromForm] string receiveNo, [FromForm] List<int> receiveIds)
        {
            try
            {
                var userId = User.GetUserId() ?? 0;
                await _receiveManagementService.CancelSPLotItemsAsync(receiveNo, receiveIds, userId);
                await _receiveManagementService.UpdateSPReceiveHeaderStatusAsync(receiveNo);
                return Ok();
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(422, new { message = ex.Message });
            }
        }

        #endregion

        #region Print Report

        [Authorize]
        public async Task<IActionResult> PrintReport()
        {
            ViewBag.ProductTypes = await _stockQueryService.GetProductTypesAsync();
            return PartialView("~/Views/Partial/_PrintReport.cshtml");
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> WithdrawalReport([FromBody] WithdrawalReportFilterModel request)
        {
            try
            {
                var all = await _withdrawalService.GetWithdrawalListAsync();
                var filtered = all.AsEnumerable();

                if (!string.IsNullOrWhiteSpace(request.Article))
                    filtered = filtered.Where(w =>
                        (w.Article ?? "").Contains(request.Article, StringComparison.OrdinalIgnoreCase) ||
                        (w.TempArticle ?? "").Contains(request.Article, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(request.EDesArt))
                    filtered = filtered.Where(w => w.EDesArt == request.EDesArt);

                if (!string.IsNullOrWhiteSpace(request.Unit))
                    filtered = filtered.Where(w => w.Unit == request.Unit);

                if (!string.IsNullOrWhiteSpace(request.WithdrawalNo))
                    filtered = filtered.Where(w => w.WithdrawalNo == request.WithdrawalNo);

                var pdfBytes = _reportService.GenerateWithdrawalReport(filtered.ToList());
                return File(pdfBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "WithdrawalReport failed: {Message}", ex.Message);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> BorrowReport([FromBody] BorrowReportFilterModel request)
        {
            try
            {
                List<BorrowModel> filtered;

                if (!string.IsNullOrWhiteSpace(request.BorrowNo))
                {
                    filtered = await _borrowService.GetBorrowDetailsByNoAsync(request.BorrowNo);
                }
                else
                {
                    var all = await _borrowService.GetBorrowListAsync(null);
                    var query = all.AsEnumerable();

                    if (!string.IsNullOrWhiteSpace(request.Article))
                        query = query.Where(w =>
                            (w.Article ?? "").Contains(request.Article, StringComparison.OrdinalIgnoreCase));

                    filtered = query.ToList();
                }

                var pdfBytes = _reportService.GenerateBorrowReport(filtered);
                return File(pdfBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "BorrowReport failed: {Message}", ex.Message);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetBorrowHeaders(string? article, string? borrowNo, string? edesArt, bool? isReturned)
        {
            var result = await _borrowService.GetBorrowHeadersAsync(article, edesArt, borrowNo, isReturned);
            return Json(result);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetBorrowDetailsByNo(string borrowNo)
        {
            var result = await _borrowService.GetBorrowDetailsByNoAsync(borrowNo);
            return Json(result);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetPendingBorrowDetails(string? article, string? edesArt, bool? isReturned)
        {
            var result = await _borrowService.GetPendingBorrowDetailsAsync(article, edesArt, isReturned);
            return Json(result);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateBorrowDocument([FromBody] int[] detailIds)
        {
            var userId = User.GetUserId() ?? 0;
            var borrowNo = await _borrowService.CreateBorrowDocumentAsync(detailIds, userId);
            return Json(new { borrowNo });
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CancelPendingBorrow([FromBody] int id)
        {
            var userId = User.GetUserId() ?? 0;
            try
            {
                await _borrowService.CancelPendingBorrowAsync(id, userId);
                return Json(new { success = true });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> StockReport([FromBody] StockReportFilterModel request)
        {
            try
            {
                var result = await _stockQueryService.GetReportStockListAsync(request.Article, request.EDesArt, request.Unit);
                var filtered = result.AsEnumerable();

                if (request.RegistrationStatus.HasValue)
                {
                    if (request.RegistrationStatus == RegistrationStatus.Pending)
                        filtered = filtered.Where(s => string.IsNullOrEmpty(s.Article));
                    else if (request.RegistrationStatus == RegistrationStatus.Registered)
                        filtered = filtered.Where(s => !string.IsNullOrEmpty(s.Article));
                }

                if (!string.IsNullOrEmpty(request.OrderNoFrom))
                    filtered = filtered.Where(s => string.Compare(s.OrderNo, request.OrderNoFrom, StringComparison.OrdinalIgnoreCase) >= 0);
                if (!string.IsNullOrEmpty(request.OrderNoTo))
                    filtered = filtered.Where(s => string.Compare(s.OrderNo, request.OrderNoTo, StringComparison.OrdinalIgnoreCase) <= 0);

                var pdfBytes = _reportService.GenerateStockReport(filtered.ToList());
                return File(pdfBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "StockReport failed: {Message}", ex.Message);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> StockNoIMGReport([FromBody] StockReportFilterModel request)
        {
            try
            {
                var result = await _stockQueryService.GetReportStockListAsync(request.Article, request.EDesArt, request.Unit);
                var filtered = result.AsEnumerable();

                if (request.RegistrationStatus.HasValue)
                {
                    if (request.RegistrationStatus == RegistrationStatus.Pending)
                        filtered = filtered.Where(s => string.IsNullOrEmpty(s.Article));
                    else if (request.RegistrationStatus == RegistrationStatus.Registered)
                        filtered = filtered.Where(s => !string.IsNullOrEmpty(s.Article));
                }

                if (!string.IsNullOrEmpty(request.OrderNoFrom))
                    filtered = filtered.Where(s => string.Compare(s.OrderNo, request.OrderNoFrom, StringComparison.OrdinalIgnoreCase) >= 0);
                if (!string.IsNullOrEmpty(request.OrderNoTo))
                    filtered = filtered.Where(s => string.Compare(s.OrderNo, request.OrderNoTo, StringComparison.OrdinalIgnoreCase) <= 0);

                var pdfBytes = _reportService.GenerateStockNoIMGReport(filtered.ToList());
                return File(pdfBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "StockNoIMGReport failed: {Message}", ex.Message);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        #endregion

        #region Stock Management

        [Authorize]
        public async Task<IActionResult> StockManagement()
        {
            var convertedItems = await _receiveManagementService.ConvertZArticlesAsync();
            await _stockQueryService.SyncArticlesAsync();
            var articles = await _stockQueryService.GetArticleListAsync();
            ViewBag.Articles = articles;
            ViewBag.BreakDescriptions = await _breakService.GetBreakDescriptionsAsync();
            ViewBag.ProductTypes = await _stockQueryService.GetProductTypesAsync();
            ViewBag.ConvertedItems = convertedItems;
            ViewBag.CurrentUserId = User.GetUserId() ?? 0;
            return PartialView("~/Views/Partial/_StockManagement.cshtml");
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetStockList(string? article, string? edesArt, int? registrationStatus, int page = 1, int pageSize = 20)
        {
            var result = await _stockQueryService.GetStockListAsync(article, edesArt, null, registrationStatus, page, pageSize);
            return Json(result);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetTrayList(string? article)
        {
            var result = await _trayService.GetTrayListAsync(article);
            return Json(result);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetTrayItems(int trayId)
        {
            var result = await _trayService.GetTrayItemsAsync(trayId);
            return Json(result);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetReceivedForTray(int trayId, string? article)
        {
            var result = await _trayService.GetReceivedForTrayAsync(trayId, article);
            return Json(result);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateTray(string trayNo, string? description)
        {
            var userId = User.GetUserId() ?? 0;
            var result = await _trayService.CreateTrayAsync(trayNo, description, userId);
            return Json(result);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> AddToTray(int trayId, string itemsJson)
        {
            try
            {
                var userId = User.GetUserId() ?? 0;
                var items = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, decimal>>(itemsJson);

                if (items == null || !items.Any())
                {
                    return BadRequest(new { message = "ไม่พบรายการสินค้า" });
                }

                await _trayService.AddToTrayAsync(trayId, items, userId);
                return Ok(new { message = "เพิ่มสินค้าลงถาดเรียบร้อย" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "เกิดข้อผิดพลาด: " + ex.Message });
            }
        }

        [Authorize]
        public async Task<IActionResult> RemoveFromTray([FromForm] List<int> trayItemIds)
        {
            var userId = User.GetUserId() ?? 0;
            await _trayService.RemoveFromTrayAsync(trayItemIds, userId);
            return Ok(new { message = "นำสินค้าออกจากถาดเรียบร้อย" });
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> BorrowFromStock(string groupKey, decimal borrowQty)
        {
            var userId = User.GetUserId() ?? 0;
            await _borrowService.BorrowFromStockAsync(groupKey, borrowQty, userId);
            return Ok(new { message = "ยืมสินค้าเรียบร้อย" });
        }

        [Authorize]
        [HttpDelete]
        public async Task<IActionResult> DeleteTray(int trayId)
        {
            var userId = User.GetUserId() ?? 0;
            await _trayService.DeleteTrayAsync(trayId, userId);
            return Ok(new { message = "ลบถาดเรียบร้อย" });
        }

        [Authorize]
        [HttpPatch]
        public async Task<IActionResult> ReturnBorrow(int borrowDetailId)
        {
            var userId = User.GetUserId() ?? 0;
            await _borrowService.ReturnBorrowAsync(borrowDetailId, userId);
            return Ok(new { message = "คืนสินค้าเรียบร้อย" });
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetBorrowList(string? groupKey)
        {
            var result = await _borrowService.GetBorrowListAsync(groupKey);
            return Json(result);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetBorrowsByStockId(string groupKey)
        {
            var result = await _borrowService.GetBorrowsByStockIdAsync(groupKey);
            return Json(result);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> WithdrawFromStock(string groupKey, decimal withdrawQty, string? remark, bool isAdminAdded = false)
        {
            var userId = User.GetUserId() ?? 0;
            await _withdrawalService.WithdrawFromStockAsync(groupKey, withdrawQty, remark, userId, isAdminAdded);
            return Ok(new { message = "เบิกสินค้าออกเรียบร้อย" });
        }

        [Authorize]
        [HttpDelete]
        public async Task<IActionResult> DeleteAdminStock(string groupKey)
        {
            await _adminStockService.DeleteAdminStockAsync(groupKey);
            return Ok(new { message = "ลบสินค้าเรียบร้อย" });
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetWithdrawalList()
        {
            var result = await _withdrawalService.GetWithdrawalListAsync();
            return Json(result);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetWithdrawalHeaders(string? article, string? withdrawalNo, string? edesArt)
        {
            var result = await _withdrawalService.GetWithdrawalHeadersAsync(article, edesArt, withdrawalNo);
            return Json(result);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetWithdrawalDetailsByNo(string withdrawalNo)
        {
            var result = await _withdrawalService.GetWithdrawalDetailsByNoAsync(withdrawalNo);
            return Json(result);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetPendingWithdrawalDetails(string? article, string? edesArt)
        {
            var result = await _withdrawalService.GetPendingWithdrawalDetailsAsync(article, edesArt);
            return Json(result);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateWithdrawalDocument([FromBody] int[] detailIds)
        {
            var userId = User.GetUserId() ?? 0;
            var withdrawalNo = await _withdrawalService.CreateWithdrawalDocumentAsync(detailIds, userId);
            return Json(new { withdrawalNo });
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CancelPendingWithdrawal([FromBody] int id)
        {
            var userId = User.GetUserId() ?? 0;
            try
            {
                await _withdrawalService.CancelPendingWithdrawalAsync(id, userId);
                return Json(new { success = true });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> SearchAddStockItems(string? article, string? barcode)
        {
            var result = await _adminStockService.SearchAddStockItems(article ?? string.Empty, barcode ?? string.Empty);
            return Json(result);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetArticleList()
        {
            var result = await _stockQueryService.GetArticleListAsync();
            return Json(result);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> ImportStockFromExcel(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "ไม่พบไฟล์" });

            try
            {
                var userId = User.GetUserId() ?? 0;
                using var stream = file.OpenReadStream();
                var result = await _adminStockService.ImportStockFromExcelAsync(stream, userId);
                var failedRows = result.Rows.Where(r => !r.IsSuccess).ToList();
                return Ok(new { successCount = result.SuccessCount, failedRows });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "ImportStockFromExcel failed: {Message}", ex.Message);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> AddStock(string barcode, decimal qty)
        {
            try
            {
                var userId = User.GetUserId() ?? 0;
                await _adminStockService.AddStockAsync(barcode, qty, userId);
                return Ok(new { message = "เพิ่มสินค้าเข้า Stock เรียบร้อย" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "AddStock failed: {Message}", ex.Message);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        #endregion

        [HttpGet]
        [Authorize]
        public IActionResult GetImage(string filename)
        {
            var imgPath = Path.Combine(_env.WebRootPath, "img", "blankimg.png");

            if (string.IsNullOrEmpty(filename))
                return BadRequest("Missing filename.");

            filename = Path.GetFileName(filename);
            var fullPath = Path.Combine("\\\\factoryserver\\bmp$", filename);

            if (!System.IO.File.Exists(fullPath))
                fullPath = imgPath;

            var contentType = fullPath.GetContentType();
            if (string.IsNullOrEmpty(contentType))
                contentType = "application/octet-stream";

            var imageBytes = System.IO.File.ReadAllBytes(fullPath);
            return File(imageBytes, contentType);
        }

        #region Repair Management

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> GetBreak([FromBody] BreakAndLostFilterModel breakAndLostFilterModel)
        {
            List<LostAndRepairModel> result = await _breakService.GetBreakAsync(breakAndLostFilterModel);
            return Ok(result);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AddBreak(string groupKey, double breakQty, int breakDes)
        {
            try
            {
                var result = await _breakService.AddBreakAsync(groupKey, breakQty, breakDes);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "AddBreak failed: {Message}", ex.Message);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AddNewBreakDescription(string breakDescription)
        {
            try
            {
                var result = await _breakService.AddNewBreakDescription(breakDescription);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "AddNewBreakDescription failed: {Message}", ex.Message);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> BreakReport([FromBody] BreakAndLostFilterModel breakAndLostFilterModel)
        {
            try
            {
                List<LostAndRepairModel> result;
                if (!string.IsNullOrWhiteSpace(breakAndLostFilterModel.BreakNo))
                {
                    result = await _breakService.GetBreakDetailsByNoAsync(breakAndLostFilterModel.BreakNo);
                }
                else
                {
                    result = await _breakService.GetBreakAsync(breakAndLostFilterModel);
                    await _breakService.PintedBreakReport(breakAndLostFilterModel.BreakIDs);
                }
                byte[] pdfBytes = _reportService.GenerateBreakReport(result);
                string contentDisposition = $"inline; filename=BreakReport_{DateTime.Now:yyyyMMdd}.pdf";
                Response.Headers.Append("Content-Disposition", contentDisposition);
                return File(pdfBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "BreakReport failed: {Message}", ex.Message);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetBreakHeaders(string? article, string? breakNo, string? edesArt)
        {
            var result = await _breakService.GetBreakHeadersAsync(article, edesArt, breakNo);
            return Json(result);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetBreakDetailsByNo(string breakNo)
        {
            var result = await _breakService.GetBreakDetailsByNoAsync(breakNo);
            return Json(result);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetPendingBreakDetails(string? article, string? edesArt)
        {
            var result = await _breakService.GetPendingBreakDetailsAsync(article, edesArt);
            return Json(result);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateBreakDocument([FromBody] int[] detailIds)
        {
            try
            {
                var userId = int.Parse(User.FindFirst("UserID")?.Value ?? "0");
                var breakNo = await _breakService.CreateBreakDocumentAsync(detailIds, userId);
                return Json(new { breakNo });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "CreateBreakDocument failed: {Message}", ex.Message);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        #endregion

        #region Permission Management

        [Authorize]
        public async Task<IActionResult> PermissionManagement()
        {
            var users = await _permissionManagement.GetUserAsync();
            return PartialView("~/Views/Partial/_PermissionManagement.cshtml", users);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetUserPermission(int userId)
        {
            var permissions = await _permissionManagement.GetPermissionAsync();
            var mapping = await _permissionManagement.GetMappingPermissionAsync(userId);
            var enabledIds = mapping.Where(m => m.IsActive).Select(m => m.PermissionId).ToHashSet();

            var result = permissions.Where(p => p.IsActive).Select(p => new
            {
                permissionId = p.PermissionId,
                name = p.Name,
                enabled = enabledIds.Contains(p.PermissionId)
            });

            return Json(result);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> UpdateUserPermission([FromBody] UpdatePermissionModel model)
        {
            var result = await _permissionManagement.UpdatePermissionAsync(model);
            if (!result.IsSuccess)
                return BadRequest(new { message = result.Message });
            return Ok();
        }

        #endregion
    }
}
