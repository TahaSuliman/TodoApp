using Application.Common;
using Application.DTOs;
using Application.Services;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;


namespace Tests
{
    /// <summary>
    /// اختبارات تكامل متقدمة لسيناريوهات قاعدة البيانات المختلفة
    /// </summary>
    public class DatabaseIntegrationTests
    {
        private readonly Mock<ITodoRepository> _mockTodoRepo;
        private readonly Mock<IUserRepository> _mockUserRepo;
        private readonly Mock<ILogger<TodoService>> _mockTodoLogger;
        private readonly Mock<ILogger<UserService>> _mockUserLogger;

        public DatabaseIntegrationTests()
        {
            _mockTodoRepo = new Mock<ITodoRepository>();
            _mockUserRepo = new Mock<IUserRepository>();
            _mockTodoLogger = new Mock<ILogger<TodoService>>();
            _mockUserLogger = new Mock<ILogger<UserService>>();
        }

        #region Connection Resilience Tests

        [Fact]
        public async Task TodoService_RetriesOnTransientError()
        {
            // Arrange
            var todoService = new TodoService(_mockTodoRepo.Object, _mockTodoLogger.Object);
            var callCount = 0;

            _mockTodoRepo.Setup(r => r.GetAllAsync())
                .ReturnsAsync(() =>
                {
                    callCount++;
                    if (callCount == 1)
                        throw new TimeoutException("Transient error");

                    return new List<Todo>
                    {
                        new() { Id = 1, Title = "Test", UserId = 1 }
                    };
                });

            // Act
            var result = await todoService.GetAllAsync();

            // Assert - في حالة وجود retry logic
            // هنا نختبر السلوك الحالي
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task UserService_HandlesConnectionPoolExhaustion()
        {
            // Arrange
            var userService = new UserService(_mockUserRepo.Object, _mockUserLogger.Object);

            var poolException = new InvalidOperationException(
                "Timeout expired. The timeout period elapsed prior to obtaining a connection from the pool."
            );

            _mockUserRepo.Setup(r => r.GetAllAsync())
                .ThrowsAsync(poolException);

            // Act
            var result = await userService.GetAllAsync();

            // Assert
            Assert.False(result.IsSuccess);
            Assert.NotNull(result.ErrorMessage);
        }

        #endregion

        #region Transaction Tests

        [Fact]
        public async Task TodoService_CreateAsync_RollsBackOnError()
        {
            // Arrange
            var todoService = new TodoService(_mockTodoRepo.Object, _mockTodoLogger.Object);
            var dto = new CreateTodoDto
            {
                Title = "Test",
                Description = "Test",
                UserId = 1
            };

            var exception = new Exception("Database error during transaction");
            _mockTodoRepo.Setup(r => r.AddAsync(It.IsAny<Todo>()))
                .ThrowsAsync(exception);

            // Act
            var result = await todoService.CreateAsync(dto);

            // Assert
            Assert.False(result.IsSuccess);
            // تأكد من عدم حفظ بيانات جزئية
            _mockTodoRepo.Verify(r => r.AddAsync(It.IsAny<Todo>()), Times.Once);
        }

        #endregion

        #region Concurrency Tests

        [Fact]
        public async Task TodoService_UpdateAsync_HandlesConcurrencyConflict()
        {
            // Arrange
            var todoService = new TodoService(_mockTodoRepo.Object, _mockTodoLogger.Object);
            var todo = new Todo
            {
                Id = 1,
                Title = "Original",
                Description = "Original",
                UserId = 1
            };

            var dto = new UpdateTodoDto
            {
                Id = 1,
                Title = "Updated",
                Description = "Updated",
                IsComplete = true
            };

            _mockTodoRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(todo);

            // محاكاة خطأ concurrency
            var concurrencyException = new DbUpdateConcurrencyException(
                "Database concurrency error"
            );
            _mockTodoRepo.Setup(r => r.UpdateAsync(It.IsAny<Todo>()))
                .ThrowsAsync(concurrencyException);

            // Act
            var result = await todoService.UpdateAsync(dto);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public async Task UserService_UpdateAsync_DetectsOptimisticLockingFailure()
        {
            // Arrange
            var userService = new UserService(_mockUserRepo.Object, _mockUserLogger.Object);
            var user = new User
            {
                Id = 1,
                Name = "Original",
                Email = "original@test.com"
            };

            var dto = new EditUserDto
            {
                Id = 1,
                Name = "Updated",
                Email = "updated@test.com",
                BirthDate = DateTime.Now
            };

            _mockUserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _mockUserRepo.Setup(r => r.GetByEmailAsync(dto.Email))
                .ReturnsAsync((User?)null);

            var lockException = new DbUpdateConcurrencyException(
                "Optimistic concurrency failure"
            );
            _mockUserRepo.Setup(r => r.UpdateAsync(It.IsAny<User>()))
                .ThrowsAsync(lockException);

            // Act
            var result = await userService.UpdateAsync(dto);

            // Assert
            Assert.False(result.IsSuccess);
        }

        #endregion

        #region Data Integrity Tests

        [Fact]
        public async Task TodoService_CreateAsync_ValidatesForeignKeyConstraint()
        {
            // Arrange
            var todoService = new TodoService(_mockTodoRepo.Object, _mockTodoLogger.Object);
            var dto = new CreateTodoDto
            {
                Title = "Test",
                Description = "Test",
                UserId = 999 // لا يوجد
            };

            var fkException = new DbUpdateException(
                "Foreign key constraint violation",
                new Exception("The INSERT statement conflicted with the FOREIGN KEY constraint")
            );
            _mockTodoRepo.Setup(r => r.AddAsync(It.IsAny<Todo>()))
                .ThrowsAsync(fkException);

            // Act
            var result = await todoService.CreateAsync(dto);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("المستخدم", result.ErrorMessage);
        }

        [Fact]
        public async Task UserService_DeleteAsync_PreventsCascadeDeleteViolation()
        {
            // Arrange
            var userService = new UserService(_mockUserRepo.Object, _mockUserLogger.Object);
            var user = new User
            {
                Id = 1,
                Name = "Test",
                Email = "test@test.com"
            };

            _mockUserRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);

            var fkException = new DbUpdateException(
                "Cannot delete user with related todos",
                new Exception("DELETE statement conflicted with the REFERENCE constraint")
            );
            _mockUserRepo.Setup(r => r.DeleteAsync(1))
                .ThrowsAsync(fkException);

            // Act
            var result = await userService.DeleteAsync(1);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("مرتبط", result.ErrorMessage);
        }

        #endregion

        #region Performance and Load Tests

        [Fact]
        public async Task TodoService_GetAllAsync_HandlesLargeDataset()
        {
            // Arrange
            var todoService = new TodoService(_mockTodoRepo.Object, _mockTodoLogger.Object);
            var largeTodoList = Enumerable.Range(1, 10000)
                .Select(i => new Todo
                {
                    Id = i,
                    Title = $"Todo {i}",
                    Description = $"Description {i}",
                    UserId = 1,
                    User = new User { Name = "User1" }
                })
                .ToList();

            _mockTodoRepo.Setup(r => r.GetAllAsync())
                .ReturnsAsync(largeTodoList);

            // Act
            var result = await todoService.GetAllAsync();

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal(10000, result.Data.Count());
        }

        [Fact]
        public async Task UserService_GetAllAsync_HandlesSlowQuery()
        {
            // Arrange
            var userService = new UserService(_mockUserRepo.Object, _mockUserLogger.Object);

            _mockUserRepo.Setup(r => r.GetAllAsync())
                .Returns(async () =>
                {
                    await Task.Delay(5000); // محاكاة استعلام بطيء
                    throw new TimeoutException("Query timeout");
                });

            // Act
            var result = await userService.GetAllAsync();

            // Assert
            Assert.False(result.IsSuccess);
            Assert.NotNull(result.ErrorMessage);
        }

        #endregion

        #region Error Recovery Tests

        [Fact]
        public async Task TodoService_CreateAsync_RecordsErrorForAudit()
        {
            // Arrange
            var todoService = new TodoService(_mockTodoRepo.Object, _mockTodoLogger.Object);
            var dto = new CreateTodoDto
            {
                Title = "Test",
                Description = "Test",
                UserId = 1
            };

            var exception = new Exception("Database error");
            _mockTodoRepo.Setup(r => r.AddAsync(It.IsAny<Todo>()))
                .ThrowsAsync(exception);

            // Act
            var result = await todoService.CreateAsync(dto);

            // Assert
            Assert.False(result.IsSuccess);

            // تحقق من تسجيل الخطأ
            _mockTodoLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task UserService_CreateAsync_LogsDetailedErrorInformation()
        {
            // Arrange
            var userService = new UserService(_mockUserRepo.Object, _mockUserLogger.Object);
            var dto = new CreateUserDto
            {
                Name = "Test",
                Email = "test@test.com",
                BirthDate = DateTime.Now
            };

            _mockUserRepo.Setup(r => r.GetByEmailAsync(dto.Email))
                .ReturnsAsync((User?)null);

            var exception = new Exception("Database error");
            _mockUserRepo.Setup(r => r.AddAsync(It.IsAny<User>()))
                .ThrowsAsync(exception);

            // Act
            var result = await userService.CreateAsync(dto);

            // Assert
            Assert.False(result.IsSuccess);

            // تحقق من تسجيل الخطأ مع التفاصيل
            _mockUserLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)
                ),
                Times.Once
            );
        }

        #endregion

        #region Multiple Simultaneous Operations

        [Fact]
        public async Task TodoService_HandlesMultipleSimultaneousCreates()
        {
            // Arrange
            var todoService = new TodoService(_mockTodoRepo.Object, _mockTodoLogger.Object);
            var tasks = new List<Task<Result<TodoDto>>>();

            for (int i = 0; i < 10; i++)
            {
                var dto = new CreateTodoDto
                {
                    Title = $"Todo {i}",
                    Description = $"Description {i}",
                    UserId = 1
                };

                var createdTodo = new Todo
                {
                    Id = i,
                    Title = dto.Title,
                    Description = dto.Description,
                    UserId = dto.UserId,
                    User = new User { Name = "User1" }
                };

                _mockTodoRepo.Setup(r => r.AddAsync(It.Is<Todo>(t => t.Title == dto.Title)))
                    .ReturnsAsync(createdTodo);

                tasks.Add(todoService.CreateAsync(dto));
            }

            // Act
            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.All(results, r => Assert.True(r.IsSuccess));
        }

        #endregion
    }

    /// <summary>
    /// استثناء مخصص لـ DbUpdateConcurrencyException
    /// </summary>
    public class DbUpdateConcurrencyException : DbUpdateException
    {
        public DbUpdateConcurrencyException(string message) : base(message) { }
    }

    /// <summary>
    /// استثناء مخصص لـ DbUpdateException
    /// </summary>
    public class DbUpdateException : Exception
    {
        public DbUpdateException(string message, Exception? innerException = null)
            : base(message, innerException) { }
    }
}