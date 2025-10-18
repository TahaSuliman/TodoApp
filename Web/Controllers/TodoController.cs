using Application.DTOs;
using Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers
{
    public class TodoController : Controller
    {
        private readonly ITodoService _todoService;
        private readonly IUserService _userService;
        private readonly ILogger<TodoController> _logger;

        public TodoController(ITodoService todoService, IUserService userService, ILogger<TodoController> logger)
        {
            _todoService = todoService;
            _userService = userService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var result = await _todoService.GetAllAsync();
            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage;
                return View(new List<TodoDto>());
            }
            return View(result.Data);
        }

        public async Task<IActionResult> Create()
        {
            var usersResult = await _userService.GetAllAsync();
            ViewBag.Users = usersResult.Data ?? new List<UserDto>();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateTodoDto dto)
        {
            if (!ModelState.IsValid)
            {
                var usersResult = await _userService.GetAllAsync();
                ViewBag.Users = usersResult.Data ?? new List<UserDto>();
                return View(dto);
            }

            var result = await _todoService.CreateAsync(dto);
            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage;
                return View(dto);
            }

            TempData["Success"] = "تم إنشاء المهمة بنجاح";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var result = await _todoService.GetByIdAsync(id);
            if (!result.IsSuccess)
            {

                TempData["Error"] = result.ErrorMessage;
                return RedirectToAction(nameof(Index));
            }

            var dto = new UpdateTodoDto
            {
                Id = result.Data!.Id,
                Title = result.Data.Title,
                Description = result.Data.Description,
                UserId = result.Data.UserId,

                IsComplete = result.Data.IsComplete
            };
            var usersResult = await _userService.GetAllAsync();
            ViewBag.Users = usersResult.Data ?? new List<UserDto>();



            return View(dto);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(UpdateTodoDto dto)
        {
            if (!ModelState.IsValid)
                return View(dto);

            var result = await _todoService.UpdateAsync(dto);
            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage;
                return View(dto);
            }
             result = await _todoService.UpdateAsync(dto);

            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage;
                return View(dto);
            }

            TempData["Success"] = "تم تحديث المهمة بنجاح";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Toggle(int id)
        {
            var result = await _todoService.ToggleCompleteAsync(id);
            if (!result.IsSuccess)
                TempData["Error"] = result.ErrorMessage;
            else
                TempData["Success"] = "تم تغيير حالة المهمة";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _todoService.DeleteAsync(id);
            if (!result.IsSuccess)
                TempData["Error"] = result.ErrorMessage;
            else
                TempData["Success"] = "تم حذف المهمة بنجاح";

            return RedirectToAction(nameof(Index));
        }
    }
}
