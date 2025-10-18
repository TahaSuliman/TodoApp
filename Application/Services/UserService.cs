using Application.Common;
using Application.Common.Exceptions;
using Application.DTOs;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepo;
        private readonly ILogger<UserService> _logger;

        public UserService(IUserRepository userRepo, ILogger<UserService> logger)
        {
            _userRepo = userRepo;
            _logger = logger;
        }

        public async Task<Result<IEnumerable<UserDto>>> GetAllAsync()
        {
            try
            {
                var users = await _userRepo.GetAllAsync();
                var dtos = users.Select(u => new UserDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    BirthDate = u.BirthDate
                });
                return Result<IEnumerable<UserDto>>.Success(dtos);
            }
            catch (Exception ex)
            {
                // تسجيل التفاصيل الفنية
                _logger.LogError(ex,
                    "Error getting all users. Technical Details: {Details}",
                    ExceptionHelper.GetTechnicalDetails(ex));

                // إرجاع رسالة واضحة للمستخدم
                var userMessage = ExceptionHelper.GetUserFriendlyMessage(ex, "فشل في جلب المستخدمين");
                return Result<IEnumerable<UserDto>>.Failure(userMessage);
            }
        }

        public async Task<Result<UserDto>> GetByIdAsync(int id)
        {
            try
            {
                var user = await _userRepo.GetByIdAsync(id);
                if (user == null)
                    return Result<UserDto>.Failure("المستخدم غير موجود");

                var dto = new UserDto
                {
                    Id = user.Id,
                    Name = user.Name,
                    Email = user.Email,
                    BirthDate = user.BirthDate
                };
                return Result<UserDto>.Success(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error getting user {UserId}. Technical Details: {Details}",
                    id,
                    ExceptionHelper.GetTechnicalDetails(ex));

                var userMessage = ExceptionHelper.GetUserFriendlyMessage(ex, "فشل في جلب المستخدم");
                return Result<UserDto>.Failure(userMessage);
            }
        }

        public async Task<Result<UserDto>> CreateAsync(CreateUserDto dto)
        {
            try
            {
                // فحص البريد الإلكتروني المكرر
                var existing = await _userRepo.GetByEmailAsync(dto.Email);
                if (existing != null)
                    return Result<UserDto>.Failure("📧 البريد الإلكتروني مستخدم بالفعل");

                var user = new User
                {
                    Name = dto.Name,
                    Email = dto.Email,
                    BirthDate = dto.BirthDate
                };

                var created = await _userRepo.AddAsync(user);
                _logger.LogInformation("✅ User created successfully: {UserId}, Email: {Email}",
                    created.Id, created.Email);

                var userDto = new UserDto
                {
                    Id = created.Id,
                    Name = created.Name,
                    Email = created.Email,
                    BirthDate = created.BirthDate
                };
                return Result<UserDto>.Success(userDto);
            }
            catch (Exception ex)
            {
                // تسجيل مفصل للخطأ
                _logger.LogError(ex,
                    "❌ Error creating user with Email: {Email}. Technical Details: {Details}",
                    dto.Email,
                    ExceptionHelper.GetTechnicalDetails(ex));

                // رسالة واضحة حسب نوع الخطأ
                string userMessage;

                if (ExceptionHelper.IsDatabaseConnectionError(ex))
                {
                    userMessage = "⚠️ فشل الاتصال بقاعدة البيانات. لا يمكن إنشاء المستخدم حالياً.";
                }
                else if (ExceptionHelper.IsDuplicateKeyError(ex))
                {
                    userMessage = "📧 البريد الإلكتروني مستخدم بالفعل";
                }
                else
                {
                    userMessage = ExceptionHelper.GetUserFriendlyMessage(ex, "فشل في إنشاء المستخدم");
                }

                return Result<UserDto>.Failure(userMessage);
            }
        }

        public async Task<Result<bool>> UpdateAsync(EditUserDto dto)
        {
            try
            {
                var user = await _userRepo.GetByIdAsync(dto.Id);
                if (user == null)
                    return Result<bool>.Failure("❌ المستخدم غير موجود");

                // فحص البريد الإلكتروني المكرر
                var existingEmail = await _userRepo.GetByEmailAsync(dto.Email);
                if (existingEmail != null && existingEmail.Id != dto.Id)
                    return Result<bool>.Failure("📧 البريد الإلكتروني مستخدم من قبل مستخدم آخر");

                user.Name = dto.Name;
                user.Email = dto.Email;
                user.BirthDate = dto.BirthDate;

                await _userRepo.UpdateAsync(user);
                _logger.LogInformation("✅ User updated successfully: {UserId}", dto.Id);

                return Result<bool>.Success(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "❌ Error updating user {UserId}. Technical Details: {Details}",
                    dto.Id,
                    ExceptionHelper.GetTechnicalDetails(ex));

                var userMessage = ExceptionHelper.GetUserFriendlyMessage(ex, "فشل في تحديث المستخدم");
                return Result<bool>.Failure(userMessage);
            }
        }

        public async Task<Result<bool>> DeleteAsync(int id)
        {
            try
            {
                var user = await _userRepo.GetByIdAsync(id);
                if (user == null)
                    return Result<bool>.Failure("❌ المستخدم غير موجود");

                await _userRepo.DeleteAsync(id);
                _logger.LogInformation("✅ User deleted successfully: {UserId}", id);

                return Result<bool>.Success(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "❌ Error deleting user {UserId}. Technical Details: {Details}",
                    id,
                    ExceptionHelper.GetTechnicalDetails(ex));

                // رسالة واضحة إذا كان المستخدم مرتبط بسجلات أخرى
                string userMessage;

                if (ExceptionHelper.IsForeignKeyError(ex))
                {
                    userMessage = "🔗 لا يمكن حذف المستخدم لأنه مرتبط بمهام موجودة. احذف المهام أولاً.";
                }
                else
                {
                    userMessage = ExceptionHelper.GetUserFriendlyMessage(ex, "فشل في حذف المستخدم");
                }

                return Result<bool>.Failure(userMessage);
            }
        }
    }
}

//===================================


//namespace Application.Services
//{

//    public class UserService : IUserService
//    {
//        private readonly IUserRepository _userRepo;
//        private readonly ILogger<UserService> _logger;

//        public UserService(IUserRepository userRepo, ILogger<UserService> logger)
//        {
//            _userRepo = userRepo;
//            _logger = logger;
//        }

//        public async Task<Result<IEnumerable<UserDto>>> GetAllAsync()
//        {
//            try
//            {
//                var users = await _userRepo.GetAllAsync();
//                var dtos = users.Select(u => new UserDto
//                {
//                    Id = u.Id,
//                    Name = u.Name,
//                    Email = u.Email,
//                    BirthDate = u.BirthDate
//                });
//                return Result<IEnumerable<UserDto>>.Success(dtos);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error getting all users");
//                return Result<IEnumerable<UserDto>>.Failure("فشل في جلب المستخدمين");
//            }
//        }

//        public async Task<Result<UserDto>> GetByIdAsync(int id)
//        {
//            try
//            {
//                var user = await _userRepo.GetByIdAsync(id);
//                if (user == null)
//                    return Result<UserDto>.Failure("المستخدم غير موجود");

//                var dto = new UserDto
//                {
//                    Id = user.Id,
//                    Name = user.Name,
//                    Email = user.Email,
//                    BirthDate = user.BirthDate
//                };
//                return Result<UserDto>.Success(dto);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error getting user {UserId}", id);
//                return Result<UserDto>.Failure("فشل في جلب المستخدم");
//            }
//        }

//        public async Task<Result<UserDto>> CreateAsync(CreateUserDto dto)
//        {
//            try
//            {
//                var existing = await _userRepo.GetByEmailAsync(dto.Email);
//                if (existing != null)
//                    return Result<UserDto>.Failure("البريد الإلكتروني مستخدم بالفعل");

//                var user = new User
//                {
//                    Name = dto.Name,
//                    Email = dto.Email,
//                    BirthDate = dto.BirthDate
//                };

//                var created = await _userRepo.AddAsync(user);
//                _logger.LogInformation("User created: {UserId}", created.Id);

//                var userDto = new UserDto
//                {
//                    Id = created.Id,
//                    Name = created.Name,
//                    Email = created.Email,
//                    BirthDate = created.BirthDate
//                };
//                return Result<UserDto>.Success(userDto);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error creating user");
//                return Result<UserDto>.Failure("فشل في إنشاء المستخدم");
//            }
//        }

//        public async Task<Result<bool>> UpdateAsync(EditUserDto dto)
//        {
//            try
//            {
//                var user = await _userRepo.GetByIdAsync(dto.Id);
//                if (user == null)
//                    return Result<bool>.Failure("المستخدم غير موجود");

//                // Check if email is used by another user
//                var existingEmail = await _userRepo.GetByEmailAsync(dto.Email);
//                if (existingEmail != null && existingEmail.Id != dto.Id)
//                    return Result<bool>.Failure("البريد الإلكتروني مستخدم من قبل مستخدم آخر");

//                user.Name = dto.Name;
//                user.Email = dto.Email;
//                user.BirthDate = dto.BirthDate;

//                await _userRepo.UpdateAsync(user);
//                _logger.LogInformation("User updated: {UserId}", dto.Id);

//                return Result<bool>.Success(true);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error updating user {UserId}", dto.Id);
//                return Result<bool>.Failure("فشل في تحديث المستخدم");
//            }
//        }

//        public async Task<Result<bool>> DeleteAsync(int id)
//        {
//            try
//            {
//                var user = await _userRepo.GetByIdAsync(id);
//                if (user == null)
//                    return Result<bool>.Failure("المستخدم غير موجود");

//                await _userRepo.DeleteAsync(id);
//                _logger.LogInformation("User deleted: {UserId}", id);

//                return Result<bool>.Success(true);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error deleting user {UserId}", id);
//                return Result<bool>.Failure("فشل في حذف المستخدم");
//            }
//        }
//    }
//    //===================================


//}
