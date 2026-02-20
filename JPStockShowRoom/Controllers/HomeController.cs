using JPStockShowRoom.Data.SPDbContext.Entities;
using JPStockShowRoom.Models;
using JPStockShowRoom.Services.Helper;
using JPStockShowRoom.Services.Implement;
using JPStockShowRoom.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace JPStockShowRoom.Controllers
{
    public class HomeController : Controller
    {
        private readonly IReceiveManagementService _receiveManagementService;
        private readonly IStockManagementService _stockManagementService;
        private readonly IWebHostEnvironment _env;
        private readonly AppSettingModel _appSettings;
        private readonly IPISService _pISService;
        private readonly IConfiguration _configuration;
        private readonly Serilog.ILogger _logger;
        private readonly IPermissionManagement _permissionManagement;

        public HomeController(
            IReceiveManagementService receiveManagementService,
            IStockManagementService stockManagementService,
            IWebHostEnvironment webHostEnvironment,
            IOptions<AppSettingModel> appSettings,
            IPISService pISService,
            IConfiguration configuration,
            Serilog.ILogger logger,
            IPermissionManagement permissionManagement)
        {
            _receiveManagementService = receiveManagementService;
            _stockManagementService = stockManagementService;
            _env = webHostEnvironment;
            _appSettings = appSettings.Value;
            _pISService = pISService;
            _configuration = configuration;
            _logger = logger;
            _permissionManagement = permissionManagement;
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
        public async Task<IActionResult> ImportReceiveNo(string receiveNo, string? lotNo)
        {
            var result = await _receiveManagementService.GetJPReceivedByReceiveNoAsync(receiveNo, null, lotNo);
            return Json(result);
        }

        [Authorize]
        [HttpPatch]
        public async Task<IActionResult> UpdateLotItems([FromForm] string receiveNo, [FromForm] List<string> orderNos, [FromForm] List<int> receiveIds)
        {
            var userId = User.GetUserId() ?? 0;
            await _receiveManagementService.UpdateLotItemsAsync(receiveNo, orderNos, receiveIds, userId);
            return Ok();
        }

        #endregion

        #region Stock Management

        [Authorize]
        public async Task<IActionResult> StockManagement()
        {
            await _stockManagementService.SyncArticlesAsync();
            var articles = await _stockManagementService.GetArticleListAsync();
            ViewBag.Articles = articles;
            return PartialView("~/Views/Partial/_StockManagement.cshtml");
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetStockList(string? article, string? edesArt, string? unit)
        {
            var result = await _stockManagementService.GetStockListAsync(article, edesArt, unit);
            return Json(result);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetTrayList(string? article)
        {
            var result = await _stockManagementService.GetTrayListAsync(article);
            return Json(result);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetTrayItems(int trayId)
        {
            var result = await _stockManagementService.GetTrayItemsAsync(trayId);
            return Json(result);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetReceivedForTray(int trayId, string? article)
        {
            var result = await _stockManagementService.GetReceivedForTrayAsync(trayId, article);
            return Json(result);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateTray(string trayNo, string? description)
        {
            var userId = User.GetUserId() ?? 0;
            var result = await _stockManagementService.CreateTrayAsync(trayNo, description, userId);
            return Json(result);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> AddToTray(int trayId, string itemsJson)
        {
            try
            {
                var userId = User.GetUserId() ?? 0;
                var items = System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, decimal>>(itemsJson);
                
                if (items == null || !items.Any())
                {
                    return BadRequest(new { message = "ไม่พบรายการสินค้า" });
                }

                await _stockManagementService.AddToTrayAsync(trayId, items, userId);
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
            await _stockManagementService.RemoveFromTrayAsync(trayItemIds, userId);
            return Ok(new { message = "นำสินค้าออกจากถาดเรียบร้อย" });
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> BorrowFromTray(int trayItemId, decimal borrowQty)
        {
            var userId = User.GetUserId() ?? 0;
            await _stockManagementService.BorrowFromTrayAsync(trayItemId, borrowQty, userId);
            return Ok(new { message = "ยืมสินค้าจากถาดเรียบร้อย" });
        }

        [Authorize]
        [HttpDelete]
        public async Task<IActionResult> DeleteTray(int trayId)
        {
            var userId = User.GetUserId() ?? 0;
            await _stockManagementService.DeleteTrayAsync(trayId, userId);
            return Ok(new { message = "ลบถาดเรียบร้อย" });
        }

        [Authorize]
        [HttpPatch]
        public async Task<IActionResult> ReturnToTray(int trayBorrowId)
        {
            var userId = User.GetUserId() ?? 0;
            await _stockManagementService.ReturnToTrayAsync(trayBorrowId, userId);
            return Ok(new { message = "คืนสินค้ากลับถาดเรียบร้อย" });
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetBorrowList(int? trayId)
        {
            var result = await _stockManagementService.GetBorrowListAsync(trayId);
            return Json(result);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> WithdrawFromStock(int receivedId, decimal withdrawQty, string? remark)
        {
            var userId = User.GetUserId() ?? 0;
            await _stockManagementService.WithdrawFromStockAsync(receivedId, withdrawQty, remark, userId);
            return Ok(new { message = "เบิกสินค้าออกเรียบร้อย" });
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetWithdrawalList()
        {
            var result = await _stockManagementService.GetWithdrawalListAsync();
            return Json(result);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetArticleList()
        {
            var result = await _stockManagementService.GetArticleListAsync();
            return Json(result);
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
    }
}


