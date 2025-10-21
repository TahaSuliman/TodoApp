using Application.Common;
using Application.Common.Exceptions;
using Application.DTOs;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;


namespace Application.Services
{
    public class TodoService : ITodoService
    {
        private readonly ITodoRepository _todoRepo;
        private readonly ILogger<TodoService> _logger;

        public TodoService(ITodoRepository todoRepo, ILogger<TodoService> logger)
        {
            _todoRepo = todoRepo;
            _logger = logger;
        }

        public async Task<Result<IEnumerable<TodoDto>>> GetAllAsync()
        {
            try
            {
                var todos = await _todoRepo.GetAllAsync();
                var dtos = todos.Select(MapToDto);
                return Result<IEnumerable<TodoDto>>.Success(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "❌ Error getting all todos. Technical Details: {Details}",
                    ExceptionHelper.GetTechnicalDetails(ex));

                var userMessage = ExceptionHelper.GetUserFriendlyMessage(ex, "فشل في جلب المهام");
                return Result<IEnumerable<TodoDto>>.Failure(userMessage);
            }
        }

        public async Task<Result<TodoDto>> GetByIdAsync(int id)
        {
            try
            {
                var todo = await _todoRepo.GetByIdAsync(id);
                if (todo == null)
                    return Result<TodoDto>.Failure("المهمة غير موجودة");

                return Result<TodoDto>.Success(MapToDto(todo));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "❌ Error getting todo {TodoId}. Technical Details: {Details}",
                    id,
                    ExceptionHelper.GetTechnicalDetails(ex));

                var userMessage = ExceptionHelper.GetUserFriendlyMessage(ex, "فشل في جلب المهمة");
                return Result<TodoDto>.Failure(userMessage);
            }
        }

        public async Task<Result<IEnumerable<TodoDto>>> GetByUserIdAsync(int userId)
        {
            try
            {
                var todos = await _todoRepo.GetByUserIdAsync(userId);
                var dtos = todos.Select(MapToDto);
                return Result<IEnumerable<TodoDto>>.Success(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "❌ Error getting todos for user {UserId}. Technical Details: {Details}",
                    userId,
                    ExceptionHelper.GetTechnicalDetails(ex));

                var userMessage = ExceptionHelper.GetUserFriendlyMessage(ex, "فشل في جلب مهام المستخدم");
                return Result<IEnumerable<TodoDto>>.Failure(userMessage);
            }
        }

        public async Task<Result<TodoDto>> CreateAsync(CreateTodoDto dto)
        {
            try
            {
                var todo = new Todo
                {
                    Title = dto.Title,
                    Description = dto.Description,
                    UserId = dto.UserId,
                    IsComplete = false
                };

                var created = await _todoRepo.AddAsync(todo);
                _logger.LogInformation("✅ Todo created successfully: {TodoId}, Title: {Title}",
                    created.Id, created.Title);

                return Result<TodoDto>.Success(MapToDto(created));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "❌ Error creating todo. Title: {Title}, UserId: {UserId}. Technical Details: {Details}",
                    dto.Title, dto.UserId,
                    ExceptionHelper.GetTechnicalDetails(ex));

                string userMessage;

                if (ExceptionHelper.IsDatabaseConnectionError(ex))
                {
                    userMessage = " فشل الاتصال بقاعدة البيانات. لا يمكن إنشاء المهمة حالياً.";
                }
                else if (ExceptionHelper.IsForeignKeyError(ex))
                {
                    userMessage = " المستخدم المحدد غير موجود. تأكد من صحة معرّف المستخدم.";
                }
                else
                {
                    userMessage = ExceptionHelper.GetUserFriendlyMessage(ex, "فشل في إنشاء المهمة");
                }

                return Result<TodoDto>.Failure(userMessage);
            }
        }

        public async Task<Result<bool>> UpdateAsync(UpdateTodoDto dto)
        {
            try
            {
                var todo = await _todoRepo.GetByIdAsync(dto.Id);
                if (todo == null)
                    return Result<bool>.Failure("المهمة غير موجودة");

                todo.Title = dto.Title;
                todo.Description = dto.Description;
                todo.IsComplete = dto.IsComplete;
                todo.UserId = dto.UserId;

                await _todoRepo.UpdateAsync(todo);
                _logger.LogInformation("✅ Todo updated successfully: {TodoId}", dto.Id);

                return Result<bool>.Success(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "❌ Error updating todo {TodoId}. Technical Details: {Details}",
                    dto.Id,
                    ExceptionHelper.GetTechnicalDetails(ex));

                string userMessage;

                if (ExceptionHelper.IsForeignKeyError(ex))
                {
                    userMessage = "المستخدم المحدد غير موجود. تأكد من صحة معرّف المستخدم.";
                }
                else
                {
                    userMessage = ExceptionHelper.GetUserFriendlyMessage(ex, "فشل في تحديث المهمة");
                }

                return Result<bool>.Failure(userMessage);
            }
        }

        public async Task<Result<bool>> DeleteAsync(int id)
        {
            try
            {
                await _todoRepo.DeleteAsync(id);
                _logger.LogInformation("✅ Todo deleted successfully: {TodoId}", id);

                return Result<bool>.Success(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "❌ Error deleting todo {TodoId}. Technical Details: {Details}",
                    id,
                    ExceptionHelper.GetTechnicalDetails(ex));

                var userMessage = ExceptionHelper.GetUserFriendlyMessage(ex, "فشل في حذف المهمة");
                return Result<bool>.Failure(userMessage);
            }
        }

        public async Task<Result<bool>> ToggleCompleteAsync(int id)
        {
            try
            {
                var todo = await _todoRepo.GetByIdAsync(id);
                if (todo == null)
                    return Result<bool>.Failure(" المهمة غير موجودة");

                todo.IsComplete = !todo.IsComplete;
                await _todoRepo.UpdateAsync(todo);

                _logger.LogInformation("✅ Todo completion toggled: {TodoId}, IsComplete: {IsComplete}",
                    id, todo.IsComplete);

                return Result<bool>.Success(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "❌ Error toggling todo {TodoId}. Technical Details: {Details}",
                    id,
                    ExceptionHelper.GetTechnicalDetails(ex));

                var userMessage = ExceptionHelper.GetUserFriendlyMessage(ex, "فشل في تغيير حالة المهمة");
                return Result<bool>.Failure(userMessage);
            }
        }

        private static TodoDto MapToDto(Todo todo) => new()
        {
            Id = todo.Id,
            Title = todo.Title,
            Description = todo.Description,
            IsComplete = todo.IsComplete,
            UserId = todo.UserId,
            AvatarImagePath = todo.AvatarImagePath,
            UserName = todo.User?.Name ?? ""
        };
    }
}
//================================

