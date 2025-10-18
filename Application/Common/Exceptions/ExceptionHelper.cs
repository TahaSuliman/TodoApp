using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Application.Common.Exceptions
{
    public static class ExceptionHelper
    {
        /// <summary>
        /// تحليل الاستثناء وإرجاع رسالة واضحة للمستخدم
        /// </summary>
        public static string GetUserFriendlyMessage(Exception ex, string defaultMessage)
        {
            // فحص إذا كان خطأ في الاتصال بقاعدة البيانات
            if (IsDatabaseConnectionError(ex))
            {
                return "⚠️ فشل الاتصال بقاعدة البيانات. يرجى المحاولة لاحقاً أو التواصل مع الدعم الفني.";
            }

            // فحص إذا كان خطأ في وقت انتهاء الاستعلام (Timeout)
            if (IsDatabaseTimeoutError(ex))
            {
                return "⏱️ انتهت مهلة الاستعلام. العملية تستغرق وقتاً طويلاً، يرجى المحاولة مرة أخرى.";
            }

            // فحص إذا كان خطأ في قيد البيانات (Constraint)
            if (IsDatabaseConstraintError(ex))
            {
                return "🔒 لا يمكن إتمام العملية بسبب قيود البيانات. تأكد من صحة البيانات المدخلة.";
            }

            // فحص إذا كان خطأ في المفتاح الفريد (Unique Key)
            if (IsDuplicateKeyError(ex))
            {
                return "🔑 البيانات المدخلة موجودة مسبقاً. يرجى استخدام قيم مختلفة.";
            }

            // فحص إذا كان خطأ في المفتاح الأجنبي (Foreign Key)
            if (IsForeignKeyError(ex))
            {
                return "🔗 لا يمكن حذف أو تعديل هذا السجل لأنه مرتبط بسجلات أخرى.";
            }

            // إرجاع الرسالة الافتراضية
            return defaultMessage;
        }

        /// <summary>
        /// فحص إذا كان الخطأ متعلق بالاتصال بقاعدة البيانات
        /// </summary>
        public static bool IsDatabaseConnectionError(Exception ex)
        {
            return ex is SqlException sqlEx && (
                sqlEx.Number == -1 ||      // Connection timeout
                sqlEx.Number == -2 ||      // Timeout expired
                sqlEx.Number == 2 ||       // Network error
                sqlEx.Number == 53 ||      // Connection failed
                sqlEx.Number == 4060 ||    // Cannot open database
                sqlEx.Number == 18456 ||   // Login failed
                sqlEx.Number == 233 ||     // Connection initialization
                sqlEx.Number == 10053 ||   // Connection broken
                sqlEx.Number == 10054 ||   // Connection reset
                sqlEx.Number == 10060 ||   // Connection timeout
                sqlEx.Number == 10061      // Connection refused
            ) || ex is DbException ||
            ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("network", StringComparison.OrdinalIgnoreCase) ||
            ex.InnerException != null && IsDatabaseConnectionError(ex.InnerException);
        }

        /// <summary>
        /// فحص إذا كان الخطأ متعلق بانتهاء مهلة الاستعلام
        /// </summary>
        public static bool IsDatabaseTimeoutError(Exception ex)
        {
            return ex is SqlException sqlEx && sqlEx.Number == -2 ||
                   ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                   ex.InnerException != null && IsDatabaseTimeoutError(ex.InnerException);
        }

        /// <summary>
        /// فحص إذا كان الخطأ متعلق بقيود البيانات
        /// </summary>
        public static bool IsDatabaseConstraintError(Exception ex)
        {
            return ex is SqlException sqlEx && (
                sqlEx.Number == 547 ||     // Constraint violation
                sqlEx.Number == 2627 ||    // Unique constraint
                sqlEx.Number == 2601       // Duplicate key
            ) || ex.Message.Contains("constraint", StringComparison.OrdinalIgnoreCase) ||
            ex.InnerException != null && IsDatabaseConstraintError(ex.InnerException);
        }

        /// <summary>
        /// فحص إذا كان الخطأ متعلق بمفتاح فريد مكرر
        /// </summary>
        public static bool IsDuplicateKeyError(Exception ex)
        {
            return ex is SqlException sqlEx && (
                sqlEx.Number == 2627 ||    // Unique constraint violation
                sqlEx.Number == 2601       // Duplicate key
            ) || ex.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
            ex.InnerException != null && IsDuplicateKeyError(ex.InnerException);
        }

        /// <summary>
        /// فحص إذا كان الخطأ متعلق بمفتاح أجنبي
        /// </summary>
        public static bool IsForeignKeyError(Exception ex)
        {
            return ex is SqlException sqlEx && sqlEx.Number == 547 ||
                   ex.Message.Contains("foreign key", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("reference constraint", StringComparison.OrdinalIgnoreCase) ||
                   ex.InnerException != null && IsForeignKeyError(ex.InnerException);
        }

        /// <summary>
        /// الحصول على تفاصيل فنية للخطأ (للـ Logging فقط)
        /// </summary>
        public static string GetTechnicalDetails(Exception ex)
        {
            var details = new System.Text.StringBuilder();
            details.AppendLine($"Exception Type: {ex.GetType().Name}");
            details.AppendLine($"Message: {ex.Message}");

            if (ex is SqlException sqlEx)
            {
                details.AppendLine($"SQL Error Number: {sqlEx.Number}");
                details.AppendLine($"SQL State: {sqlEx.State}");
                details.AppendLine($"SQL Server: {sqlEx.Server}");
                details.AppendLine($"SQL Procedure: {sqlEx.Procedure}");
                details.AppendLine($"SQL Line Number: {sqlEx.LineNumber}");
            }

            if (ex.InnerException != null)
            {
                details.AppendLine($"Inner Exception: {ex.InnerException.Message}");
            }

            details.AppendLine($"Stack Trace: {ex.StackTrace}");

            return details.ToString();
        }
    }
}
