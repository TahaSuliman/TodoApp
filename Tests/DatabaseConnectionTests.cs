using Application.DTOs;
using Application.Services;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.Data.SqlClient;
using System.Data.Common;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests
{
    /// <summary>
    /// اختبارات فشل الاتصال بقاعدة البيانات للتأكد من معالجة الأخطاء بشكل صحيح
    /// </summary>
    public class DatabaseConnectionTests
    {
        private readonly Mock<ITodoRepository> _mockTodoRepo;
        private readonly Mock<IUserRepository> _mockUserRepo;
        private readonly Mock<ILogger<TodoService>> _mockTodoLogger;
        private readonly Mock<ILogger<UserService>> _mockUserLogger;
        private readonly TodoService _todoService;
        private readonly UserService _userService;

        public DatabaseConnectionTests()
        {
            _mockTodoRepo = new Mock<ITodoRepository>();
            _mockUserRepo = new Mock<IUserRepository>();
            _mockTodoLogger = new Mock<ILogger<TodoService>>();
            _mockUserLogger = new Mock<ILogger<UserService>>();
            _todoService = new TodoService(_mockTodoRepo.Object, _mockTodoLogger.Object);
            _userService = new UserService(_mockUserRepo.Object, _mockUserLogger.Object);
        }

        #region Todo Service - Database Connection Tests

        [Fact]
        public async Task TodoService_GetAllAsync_ReturnsFailure_WhenDatabaseConnectionFails()
        {
            // Arrange
            //var dbException = new SqlException();
            var dbException = new InvalidOperationException("Database connection failed");


            _mockTodoRepo.Setup(r => r.GetAllAsync())
                .ThrowsAsync(dbException);

            // Act
            var result = await _todoService.GetAllAsync();

            // Assert
            Assert.False(result.IsSuccess);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains("فشل", result.ErrorMessage);
        }

        [Fact]
        public async Task TodoService_CreateAsync_ReturnsConnectionError_WhenDatabaseIsDown()
        {
            // Arrange
            var dto = new CreateTodoDto
            {
                Title = "Test Todo",
                Description = "Test Description",
                UserId = 1
            };

            // محاكاة خطأ اتصال بقاعدة البيانات
            //var dbException = new SqlException();
            var dbException = new InvalidOperationException("Database connection failed");


            _mockTodoRepo.Setup(r => r.AddAsync(It.IsAny<Todo>()))
                .ThrowsAsync(dbException);

            // Act
            var result = await _todoService.CreateAsync(dto);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.NotNull(result.ErrorMessage);

            // التحقق من أن الرسالة تحتوي على إشارة لمشكلة قاعدة البيانات
            Assert.True(
                result.ErrorMessage.Contains("قاعدة البيانات") ||
                result.ErrorMessage.Contains("الاتصال"),
                $"Expected database error message, but got: {result.ErrorMessage}"
            );
        }

        [Fact]
        public async Task TodoService_UpdateAsync_ReturnsFailure_WhenDatabaseTimeout()
        {
            // Arrange
            var dto = new UpdateTodoDto
            {
                Id = 1,
                Title = "Updated",
                Description = "Updated Description",
                IsComplete = true
            };

            var todo = new Todo
            {
                Id = 1,
                Title = "Old Title",
                UserId = 1
            };

            _mockTodoRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(todo);

            // محاكاة خطأ timeout
            var timeoutException = new TimeoutException("Database timeout");
            _mockTodoRepo.Setup(r => r.UpdateAsync(It.IsAny<Todo>()))
                .ThrowsAsync(timeoutException);

            // Act
            var result = await _todoService.UpdateAsync(dto);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public async Task TodoService_CreateAsync_ReturnsForeignKeyError_WhenUserDoesNotExist()
        {
            // Arrange
            var dto = new CreateTodoDto
            {
                Title = "Test Todo",
                Description = "Test Description",
                UserId = 999 // مستخدم غير موجود
            };

            // محاكاة خطأ Foreign Key
            var sqlException = CreateSqlException(547); // 547 = Foreign Key violation
            _mockTodoRepo.Setup(r => r.AddAsync(It.IsAny<Todo>()))
                .ThrowsAsync(sqlException);

            // Act
            var result = await _todoService.CreateAsync(dto);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains("المستخدم", result.ErrorMessage);
        }

        #endregion

        #region User Service - Database Connection Tests

        [Fact]
        public async Task UserService_GetAllAsync_ReturnsFailure_WhenDatabaseConnectionFails()
        {
            // Arrange
            //var dbException = new SqlException();
            var dbException = new InvalidOperationException("Database connection failed");


            _mockUserRepo.Setup(r => r.GetAllAsync())
                .ThrowsAsync(dbException);

            // Act
            var result = await _userService.GetAllAsync();

            // Assert
            Assert.False(result.IsSuccess);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains("فشل", result.ErrorMessage);
        }

        [Fact]
        public async Task UserService_CreateAsync_ReturnsConnectionError_WhenDatabaseIsDown()
        {
            // Arrange
            var dto = new CreateUserDto
            {
                Name = "Test User",
                Email = "test@test.com",
                BirthDate = DateTime.Now
            };

            _mockUserRepo.Setup(r => r.GetByEmailAsync(dto.Email))
                .ReturnsAsync((User?)null);

            // محاكاة خطأ اتصال
            //var dbException = new SqlException();
            var dbException = new InvalidOperationException("Database connection failed");


            _mockUserRepo.Setup(r => r.AddAsync(It.IsAny<User>()))
                .ThrowsAsync(dbException);

            // Act
            var result = await _userService.CreateAsync(dto);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.NotNull(result.ErrorMessage);
            Assert.True(
                result.ErrorMessage.Contains("قاعدة البيانات") ||
                result.ErrorMessage.Contains("الاتصال"),
                $"Expected database error message, but got: {result.ErrorMessage}"
            );
        }

        [Fact]
        public async Task UserService_CreateAsync_ReturnsDuplicateKeyError_WhenEmailExists()
        {
            // Arrange
            var dto = new CreateUserDto
            {
                Name = "Test User",
                Email = "duplicate@test.com",
                BirthDate = DateTime.Now
            };

            _mockUserRepo.Setup(r => r.GetByEmailAsync(dto.Email))
                .ReturnsAsync((User?)null);

            // محاكاة خطأ Duplicate Key (2601 or 2627)
            var sqlException = CreateSqlException(2601);
            _mockUserRepo.Setup(r => r.AddAsync(It.IsAny<User>()))
                .ThrowsAsync(sqlException);

            // Act
            var result = await _userService.CreateAsync(dto);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains("البريد الإلكتروني", result.ErrorMessage);
        }

        [Fact]
        public async Task UserService_DeleteAsync_ReturnsForeignKeyError_WhenUserHasTodos()
        {
            // Arrange
            var user = new User
            {
                Id = 1,
                Name = "Test User",
                Email = "test@test.com"
            };

            _mockUserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);

            // محاكاة خطأ Foreign Key عند الحذف
            var sqlException = CreateSqlException(547);
            _mockUserRepo.Setup(r => r.DeleteAsync(1))
                .ThrowsAsync(sqlException);

            // Act
            var result = await _userService.DeleteAsync(1);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains("مرتبط", result.ErrorMessage);
        }

        [Fact]
        public async Task UserService_UpdateAsync_ReturnsFailure_WhenDatabaseTimeout()
        {
            // Arrange
            var dto = new EditUserDto
            {
                Id = 1,
                Name = "Updated Name",
                Email = "updated@test.com",
                BirthDate = DateTime.Now
            };

            var user = new User
            {
                Id = 1,
                Name = "Old Name",
                Email = "old@test.com"
            };

            _mockUserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _mockUserRepo.Setup(r => r.GetByEmailAsync(dto.Email))
                .ReturnsAsync((User?)null);

            // محاكاة خطأ timeout
            var timeoutException = new TimeoutException("Database timeout");
            _mockUserRepo.Setup(r => r.UpdateAsync(It.IsAny<User>()))
                .ThrowsAsync(timeoutException);

            // Act
            var result = await _userService.UpdateAsync(dto);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.NotNull(result.ErrorMessage);
        }

        #endregion

        #region Network and Connection Tests

        [Fact]
        public async Task TodoService_GetAllAsync_HandlesNetworkException()
        {
            // Arrange
            var networkException = new System.Net.Sockets.SocketException();
            _mockTodoRepo.Setup(r => r.GetAllAsync())
                .ThrowsAsync(networkException);

            // Act
            var result = await _todoService.GetAllAsync();

            // Assert
            Assert.False(result.IsSuccess);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public async Task UserService_CreateAsync_HandlesDbConnectionException()
        {
            // Arrange
            var dto = new CreateUserDto
            {
                Name = "Test",
                Email = "test@test.com",
                BirthDate = DateTime.Now
            };

            _mockUserRepo.Setup(r => r.GetByEmailAsync(dto.Email))
                .ReturnsAsync((User?)null);

            //var dbException = new DbException();
            var dbException = new InvalidOperationException("Database connection failed");


            _mockUserRepo.Setup(r => r.AddAsync(It.IsAny<User>()))
                .ThrowsAsync(dbException);

            // Act
            var result = await _userService.CreateAsync(dto);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.NotNull(result.ErrorMessage);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// إنشاء SqlException مع رقم خطأ محدد
        /// </summary>
        private SqlException CreateSqlException(int errorNumber)
        {
            // ملاحظة: SqlException لا يمكن إنشاؤه مباشرة، لذا نستخدم reflection
            // في بيئة اختبار حقيقية، يمكنك استخدام مكتبات مثل EntityFrameworkCore.InMemory

            var collection = Activator.CreateInstance(
                typeof(SqlErrorCollection),
                true
            ) as SqlErrorCollection;

            var error = Activator.CreateInstance(
                typeof(SqlError),
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                null,
                new object[] { errorNumber, (byte)0, (byte)0, "TestServer", "TestError", "TestProc", 0 },
                null
            );

            var addMethod = typeof(SqlErrorCollection).GetMethod(
                "Add",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );
            addMethod?.Invoke(collection, new[] { error });

            var exception = Activator.CreateInstance(
                typeof(SqlException),
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                null,
                new object[] { "Test exception", collection, null, Guid.NewGuid() },
                null
            ) as SqlException;

            return exception ?? throw new InvalidOperationException("Failed to create SqlException");
        }

        #endregion
    }
}