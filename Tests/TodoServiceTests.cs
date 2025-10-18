using Application.DTOs;
using Application.Services;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests
{
    public class TodoServiceTests
    {
        private readonly Mock<ITodoRepository> _mockRepo;
        private readonly Mock<ILogger<TodoService>> _mockLogger;
        private readonly TodoService _service;

        public TodoServiceTests()
        {
            _mockRepo = new Mock<ITodoRepository>();
            _mockLogger = new Mock<ILogger<TodoService>>();
            _service = new TodoService(_mockRepo.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task GetAllAsync_ReturnsSuccess_WhenTodosExist()
        {
            // Arrange
            var todos = new List<Todo>
        {
            new() { Id = 1, Title = "Test 1", Description = "Desc 1", UserId = 1, User = new User { Name = "User1" } },
            new() { Id = 2, Title = "Test 2", Description = "Desc 2", UserId = 1, User = new User { Name = "User1" } }
        };
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(todos);

            // Act
            var result = await _service.GetAllAsync();

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal(2, result.Data.Count());
        }

        [Fact]
        public async Task GetByIdAsync_ReturnsSuccess_WhenTodoExists()
        {
            // Arrange
            var todo = new Todo
            {
                Id = 1,
                Title = "Test Todo",
                Description = "Test Description",
                UserId = 1,
                User = new User { Name = "Test User" }
            };
            _mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(todo);

            // Act
            var result = await _service.GetByIdAsync(1);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal("Test Todo", result.Data.Title);
        }

        [Fact]
        public async Task GetByIdAsync_ReturnsFailure_WhenTodoNotFound()
        {
            // Arrange
            _mockRepo.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Todo?)null);

            // Act
            var result = await _service.GetByIdAsync(999);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal("المهمة غير موجودة", result.ErrorMessage);
        }

        [Fact]
        public async Task CreateAsync_ReturnsSuccess_WhenValidDto()
        {
            // Arrange
            var dto = new CreateTodoDto
            {
                Title = "New Todo",
                Description = "New Description",
                UserId = 1
            };
            var createdTodo = new Todo
            {
                Id = 1,
                Title = dto.Title,
                Description = dto.Description,
                UserId = dto.UserId,
                User = new User { Name = "Test User" }
            };
            _mockRepo.Setup(r => r.AddAsync(It.IsAny<Todo>())).ReturnsAsync(createdTodo);

            // Act
            var result = await _service.CreateAsync(dto);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal("New Todo", result.Data.Title);
        }

        [Fact]
        public async Task UpdateAsync_ReturnsSuccess_WhenTodoExists()
        {
            // Arrange
            var existingTodo = new Todo
            {
                Id = 1,
                Title = "Old Title",
                Description = "Old Description",
                UserId = 1,
                User = new User { Name = "Test User" }
            };
            var updateDto = new UpdateTodoDto
            {
                Id = 1,
                Title = "Updated Title",
                Description = "Updated Description",
                IsComplete = true
            };
            _mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(existingTodo);
            _mockRepo.Setup(r => r.UpdateAsync(It.IsAny<Todo>())).Returns(Task.CompletedTask);

            // Act
            var result = await _service.UpdateAsync(updateDto);

            // Assert
            Assert.True(result.IsSuccess);
            _mockRepo.Verify(r => r.UpdateAsync(It.IsAny<Todo>()), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_ReturnsSuccess_WhenCalled()
        {
            // Arrange
            _mockRepo.Setup(r => r.DeleteAsync(1)).Returns(Task.CompletedTask);

            // Act
            var result = await _service.DeleteAsync(1);

            // Assert
            Assert.True(result.IsSuccess);
            _mockRepo.Verify(r => r.DeleteAsync(1), Times.Once);
        }

        [Fact]
        public async Task ToggleCompleteAsync_ReturnsSuccess_WhenTodoExists()
        {
            // Arrange
            var todo = new Todo
            {
                Id = 1,
                Title = "Test",
                Description = "Test",
                IsComplete = false,
                UserId = 1,
                User = new User { Name = "Test User" }
            };
            _mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(todo);
            _mockRepo.Setup(r => r.UpdateAsync(It.IsAny<Todo>())).Returns(Task.CompletedTask);

            // Act
            var result = await _service.ToggleCompleteAsync(1);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.True(todo.IsComplete);
            _mockRepo.Verify(r => r.UpdateAsync(It.IsAny<Todo>()), Times.Once);
        }

        [Fact]
        public async Task GetByUserIdAsync_ReturnsSuccess_WhenUserHasTodos()
        {
            // Arrange
            var todos = new List<Todo>
        {
            new() { Id = 1, Title = "Todo 1", UserId = 1, User = new User { Name = "User1" } },
            new() { Id = 2, Title = "Todo 2", UserId = 1, User = new User { Name = "User1" } }
        };
            _mockRepo.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(todos);

            // Act
            var result = await _service.GetByUserIdAsync(1);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal(2, result.Data.Count());
        }
    }
}
