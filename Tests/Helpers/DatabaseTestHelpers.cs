using Application.Common;
using Application.DTOs;
using Application.Services;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.Data.SqlClient;
using System.Reflection;
using Moq;


namespace Tests.Helpers
{
    /// <summary>
    /// أدوات مساعدة لإنشاء استثناءات قاعدة البيانات في الاختبارات
    /// </summary>
    public static class DatabaseTestHelpers
    {
        /// <summary>
        /// إنشاء SqlException بكود خطأ محدد
        /// </summary>
        /// <param name="errorNumber">رقم الخطأ (مثل: 547 = FK, 2601/2627 = Unique constraint)</param>
        /// <param name="errorMessage">رسالة الخطأ</param>
        public static SqlException CreateSqlException(int errorNumber, string errorMessage = "Test SQL Error")
        {
            try
            {
                // إنشاء SqlErrorCollection
                var collectionType = typeof(SqlErrorCollection);
                var collection = (SqlErrorCollection?)Activator.CreateInstance(
                    collectionType,
                    BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    null,
                    null
                );

                if (collection == null)
                    throw new InvalidOperationException("Failed to create SqlErrorCollection");

                // إنشاء SqlError
                var errorType = typeof(SqlError);
                var error = Activator.CreateInstance(
                    errorType,
                    BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    new object[]
                    {
                        errorNumber,      // number
                        (byte)1,          // state
                        (byte)1,          // errorClass
                        "TestServer",     // server
                        errorMessage,     // message
                        "TestProcedure",  // procedure
                        1                 // lineNumber
                    },
                    null
                );

                if (error == null)
                    throw new InvalidOperationException("Failed to create SqlError");

                // إضافة الخطأ إلى المجموعة
                var addMethod = collectionType.GetMethod(
                    "Add",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );
                addMethod?.Invoke(collection, new[] { error });

                // إنشاء SqlException
                var exceptionType = typeof(SqlException);
                var exception = (SqlException?)Activator.CreateInstance(
                    exceptionType,
                    BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    new object[]
                    {
                        errorMessage,     // message
                        collection,       // errorCollection
                        null,             // innerException
                        Guid.NewGuid()    // clientConnectionId
                    },
                    null
                );

                return exception ?? throw new InvalidOperationException("Failed to create SqlException");
            }
            catch (Exception ex)
            {
                // في حالة فشل إنشاء SqlException، نعيد استثناء بديل
                throw new InvalidOperationException(
                    $"Failed to create SqlException with error number {errorNumber}: {ex.Message}",
                    ex
                );
            }
        }

        /// <summary>
        /// إنشاء استثناء Foreign Key
        /// </summary>
        public static Exception CreateForeignKeyException(string constraintName = "FK_Constraint")
        {
            return CreateSqlException(547, $"The INSERT statement conflicted with the FOREIGN KEY constraint \"{constraintName}\"");
        }

        /// <summary>
        /// إنشاء استثناء Unique Constraint
        /// </summary>
        public static Exception CreateUniqueConstraintException(string columnName = "Email")
        {
            return CreateSqlException(2601, $"Cannot insert duplicate key in object. The duplicate key value is ({columnName})");
        }

        /// <summary>
        /// إنشاء استثناء اتصال قاعدة البيانات
        /// </summary>
        public static Exception CreateConnectionException()
        {
            return new InvalidOperationException(
                "A network-related or instance-specific error occurred while establishing a connection to SQL Server. " +
                "The server was not found or was not accessible."
            );
        }

        /// <summary>
        /// إنشاء استثناء Timeout
        /// </summary>
        public static TimeoutException CreateTimeoutException()
        {
            return new TimeoutException(
                "Timeout expired. The timeout period elapsed prior to completion of the operation " +
                "or the server is not responding."
            );
        }

        /// <summary>
        /// إنشاء استثناء Connection Pool
        /// </summary>
        public static InvalidOperationException CreateConnectionPoolException()
        {
            return new InvalidOperationException(
                "Timeout expired. The timeout period elapsed prior to obtaining a connection from the pool. " +
                "This may have occurred because all pooled connections were in use and max pool size was reached."
            );
        }

        /// <summary>
        /// إنشاء استثناء Deadlock
        /// </summary>
        public static Exception CreateDeadlockException()
        {
            return CreateSqlException(1205, "Transaction was deadlocked on lock resources with another process and has been chosen as the deadlock victim.");
        }

        /// <summary>
        /// إنشاء استثناء Permission Denied
        /// </summary>
        public static Exception CreatePermissionDeniedException(string objectName = "Table")
        {
            return CreateSqlException(229, $"The SELECT permission was denied on the object '{objectName}'");
        }

        /// <summary>
        /// إنشاء استثناء Database Not Found
        /// </summary>
        public static Exception CreateDatabaseNotFoundException(string dbName = "TodoDb")
        {
            return CreateSqlException(4060, $"Cannot open database \"{dbName}\" requested by the login. The login failed.");
        }
    }

    /// <summary>
    /// استثناء عام لقاعدة البيانات
    /// </summary>
    public class DbException : Exception
    {
        public DbException(string message) : base(message) { }
        public DbException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// بيانات اختبار مشتركة
    /// </summary>
    public static class TestData
    {
        public static User CreateTestUser(int id = 1, string name = "Test User", string email = "test@test.com")
        {
            return new User
            {
                Id = id,
                Name = name,
                Email = email,
                BirthDate = DateTime.Now.AddYears(-25)
            };
        }

        public static Todo CreateTestTodo(int id = 1, string title = "Test Todo", int userId = 1)
        {
            return new Todo
            {
                Id = id,
                Title = title,
                Description = "Test Description",
                UserId = userId,
                IsComplete = false,
                User = CreateTestUser(userId)
            };
        }

        public static CreateUserDto CreateUserDto(string name = "New User", string email = "new@test.com")
        {
            return new CreateUserDto
            {
                Name = name,
                Email = email,
                BirthDate = DateTime.Now.AddYears(-20)
            };
        }

        public static CreateTodoDto CreateTodoDto(string title = "New Todo", int userId = 1)
        {
            return new CreateTodoDto
            {
                Title = title,
                Description = "New Description",
                UserId = userId
            };
        }

        public static List<User> CreateTestUsers(int count = 5)
        {
            return Enumerable.Range(1, count)
                .Select(i => CreateTestUser(i, $"User {i}", $"user{i}@test.com"))
                .ToList();
        }

        public static List<Todo> CreateTestTodos(int count = 5, int userId = 1)
        {
            return Enumerable.Range(1, count)
                .Select(i => CreateTestTodo(i, $"Todo {i}", userId))
                .ToList();
        }
    }

    /// <summary>
    /// Extensions للمساعدة في الاختبارات
    /// </summary>
    public static class TestExtensions
    {
        /// <summary>
        /// التحقق من أن النتيجة فاشلة وتحتوي على رسالة معينة
        /// </summary>
        public static void ShouldBeFailureWithMessage<T>(this Result<T> result, string expectedMessage)
        {
            Assert.False(result.IsSuccess);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains(expectedMessage, result.ErrorMessage);
        }

        /// <summary>
        /// التحقق من أن النتيجة ناجحة
        /// </summary>
        public static void ShouldBeSuccess<T>(this Result<T> result)
        {
            Assert.True(result.IsSuccess, $"Expected success but got: {result.ErrorMessage}");
            Assert.NotNull(result.Data);
        }

        /// <summary>
        /// التحقق من أن النتيجة فاشلة
        /// </summary>
        public static void ShouldBeFailure<T>(this Result<T> result)
        {
            Assert.False(result.IsSuccess);
            Assert.NotNull(result.ErrorMessage);
        }
    }
}