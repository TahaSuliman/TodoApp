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
    public class UserServiceTests
    {
        private readonly Mock<IUserRepository> _mockRepo;
        private readonly Mock<ILogger<UserService>> _mockLogger;
        private readonly UserService _service;

        public UserServiceTests()
        {
            _mockRepo = new Mock<IUserRepository>();
            _mockLogger = new Mock<ILogger<UserService>>();
            _service = new UserService(_mockRepo.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task GetAllAsync_ReturnsSuccess_WhenUsersExist()
        {
            // Arrange
            var users = new List<User>
        {
            new() { Id = 1, Name = "User 1", Email = "user1@test.com", BirthDate = DateTime.Now },
            new() { Id = 2, Name = "User 2", Email = "user2@test.com", BirthDate = DateTime.Now }
        };
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(users);

            // Act
            var result = await _service.GetAllAsync();

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal(2, result.Data.Count());
        }

        [Fact]
        public async Task GetByIdAsync_ReturnsSuccess_WhenUserExists()
        {
            // Arrange
            var user = new User
            {
                Id = 1,
                Name = "Test User",
                Email = "test@test.com",
                BirthDate = DateTime.Now
            };
            _mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);

            // Act
            var result = await _service.GetByIdAsync(1);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal("Test User", result.Data.Name);
        }

        [Fact]
        public async Task CreateAsync_ReturnsSuccess_WhenEmailIsUnique()
        {
            // Arrange
            var dto = new CreateUserDto
            {
                Name = "New User",
                Email = "new@test.com",
                BirthDate = DateTime.Now
            };
            _mockRepo.Setup(r => r.GetByEmailAsync(dto.Email)).ReturnsAsync((User?)null);
            _mockRepo.Setup(r => r.AddAsync(It.IsAny<User>())).ReturnsAsync(new User
            {
                Id = 1,
                Name = dto.Name,
                Email = dto.Email,
                BirthDate = dto.BirthDate
            });

            // Act
            var result = await _service.CreateAsync(dto);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal("New User", result.Data.Name);
        }

        [Fact]
        public async Task CreateAsync_ReturnsFailure_WhenEmailExists()
        {
            // Arrange
            var existingUser = new User
            {
                Id = 1,
                Name = "Existing User",
                Email = "existing@test.com",
                BirthDate = DateTime.Now
            };
            var dto = new CreateUserDto
            {
                Name = "New User",
                Email = "existing@test.com",
                BirthDate = DateTime.Now
            };
            _mockRepo.Setup(r => r.GetByEmailAsync(dto.Email)).ReturnsAsync(existingUser);

            // Act
            var result = await _service.CreateAsync(dto);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal("البريد الإلكتروني مستخدم بالفعل", result.ErrorMessage);
        }
    }
}
