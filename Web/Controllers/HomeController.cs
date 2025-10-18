using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using Web.Models;

namespace Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<HomeController> _logger;

        public HomeController(AppDbContext context, ILogger<HomeController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            // التحقق من حالة قاعدة البيانات
            var databaseStatus = await CheckDatabaseStatus();

            ViewBag.DatabaseStatus = databaseStatus.IsConnected ? "متصل ✅" : "غير متصل ❌";
            ViewBag.DatabaseMessage = databaseStatus.Message;
            ViewBag.IsHealthy = databaseStatus.IsConnected;

            _logger.LogInformation("Home page loaded. Database status: {Status}",
                databaseStatus.IsConnected ? "Connected" : "Disconnected");

            return View();
        }

        private async Task<DatabaseStatus> CheckDatabaseStatus()
        {
            try
            {
                // محاولة الاتصال بقاعدة البيانات
                var canConnect = await _context.Database.CanConnectAsync();

                if (canConnect)
                {
                    // عد السجلات للتأكد من عمل الاتصال
                    var userCount = await _context.Users.CountAsync();
                    var todoCount = await _context.Todos.CountAsync();

                    return new DatabaseStatus
                    {
                        IsConnected = true,
                        Message = $"قاعدة البيانات تعمل بشكل صحيح. المستخدمين: {userCount}، المهام: {todoCount}"
                    };
                }

                return new DatabaseStatus
                {
                    IsConnected = false,
                    Message = "فشل الاتصال بقاعدة البيانات"
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Database connection check failed");

                return new DatabaseStatus
                {
                    IsConnected = false,
                    Message = $"⚠️ قاعدة البيانات غير متاحة حالياً: {ex.Message}"
                };
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }
        [HttpGet]
        public async Task<IActionResult> GetSystemStatus()
        {
            try
            {
                // فحص حالة قاعدة البيانات
                var canConnect = await _context.Database.CanConnectAsync();

                ViewBag.IsHealthy = canConnect;
                ViewBag.DatabaseStatus = canConnect ? "متصل ✅" : "غير متصل ❌";
                ViewBag.DatabaseMessage = canConnect
                    ? $"تم الاتصال بنجاح في {DateTime.Now:hh:mm:ss tt}"
                    : "فشل الاتصال بقاعدة البيانات";

                return PartialView("_SystemStatusPartial");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking system status");

                ViewBag.IsHealthy = false;
                ViewBag.DatabaseStatus = "خطأ ❌";
                ViewBag.DatabaseMessage = "حدث خطأ أثناء فحص الحالة";

                return PartialView("_SystemStatusPartial");
            }
        }
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }

    public class DatabaseStatus
    {
        public bool IsConnected { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}

//===============================

//namespace Web.Controllers
//{
//    public class HomeController : Controller
//    {
//        private readonly ILogger<HomeController> _logger;

//        public HomeController(ILogger<HomeController> logger)
//        {
//            _logger = logger;
//        }

//        public IActionResult Index()
//        {
//            return View();
//        }

//        public IActionResult Privacy()
//        {
//            return View();
//        }

//        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
//        public IActionResult Error()
//        {
//            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
//        }
//    }
//}
