using Application.DTOs;
using Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers
{
    public class UserController : Controller
    {
        private readonly IUserService _userService;
        private readonly ILogger<UserController> _logger;

        public UserController(IUserService userService, ILogger<UserController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        // GET: /User
        public async Task<IActionResult> Index()
        {
            var result = await _userService.GetAllAsync();

            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage;
                return View(new List<UserDto>());
            }

            return View(result.Data);
        }

        // GET: /User/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var result = await _userService.GetByIdAsync(id);

            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage;
                return RedirectToAction(nameof(Index));
            }

            return View(result.Data);
        }

        // GET: /User/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: /User/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserDto dto)
        {
            if (!ModelState.IsValid)
            {
                return View(dto);
            }

            var result = await _userService.CreateAsync(dto);

            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage;
                return View(dto);
            }

            TempData["Success"] = "تم إنشاء المستخدم بنجاح";
            _logger.LogInformation("User created successfully: {UserId}", result.Data?.Id);

            return RedirectToAction(nameof(Index));
        }

        // GET: /User/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var result = await _userService.GetByIdAsync(id);

            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage;
                return RedirectToAction(nameof(Index));
            }

            var editDto = new EditUserDto
            {
                Id = result.Data!.Id,
                Name = result.Data.Name,
                Email = result.Data.Email,
                BirthDate = result.Data.BirthDate
            };

            return View(editDto);
        }

        // POST: /User/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditUserDto dto)
        {
            if (id != dto.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(dto);
            }

            var result = await _userService.UpdateAsync(dto);

            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage;
                return View(dto);
            }

            TempData["Success"] = "تم تحديث المستخدم بنجاح";
            return RedirectToAction(nameof(Index));
        }

        // GET: /User/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _userService.GetByIdAsync(id);

            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage;
                return RedirectToAction(nameof(Index));
            }

            return View(result.Data);
        }

        // POST: /User/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var result = await _userService.DeleteAsync(id);

            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage;
            }
            else
            {
                TempData["Success"] = "تم حذف المستخدم بنجاح";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
