using System.Net;
using System.Text.Json;
using AudioGuide.Api.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AudioGuide.Api.Middleware
{
    /*
     * Xử lý lỗi TẬP TRUNG cho toàn bộ ứng dụng.
     * Thay vì try/catch ở từng Controller, mọi ngoại lệ đều "bay" lên đây và được đổi thành
     * JSON {message, code, details} với mã HTTP phù hợp. Frontend chỉ cần đọc message / code.
     */
    public class ExceptionMiddleware
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            // Lỗi nghiệp vụ do Service ném ra (không tìm thấy, trùng mã, sai trạng thái...)
            catch (BusinessException ex)
            {
                if (ex.StatusCode >= 500) _logger.LogWarning("Dịch vụ ngoài lỗi: {Message}", ex.Message);
                await WriteErrorAsync(context, ex.StatusCode, ex.Message, ex.Code, ex.Details);
            }
            // Lỗi ràng buộc dữ liệu của SQL Server -> 409 thay vì 500
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sql)
            {
                _logger.LogWarning("Vi phạm ràng buộc dữ liệu (SQL {Number}): {Message}", sql.Number, sql.Message);
                var (message, code) = sql.Number switch
                {
                    // 547: còn bản ghi khác tham chiếu tới (ON DELETE NO ACTION) hoặc tham chiếu tới bản ghi không tồn tại
                    547 => ("Không thể thực hiện vì dữ liệu này đang được dữ liệu khác tham chiếu (ràng buộc khóa ngoại).",
                        "REFERENCED_BY_OTHER_DATA"),
                    // 2601 / 2627: trùng khóa chính hoặc ràng buộc UNIQUE
                    2601 or 2627 => ("Dữ liệu bị trùng (vi phạm ràng buộc duy nhất).", "DUPLICATE"),
                    _ => ("Dữ liệu vi phạm ràng buộc của cơ sở dữ liệu.", "DATABASE_CONSTRAINT")
                };
                await WriteErrorAsync(context, (int)HttpStatusCode.Conflict, message, code);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                // Người dùng đóng trang giữa chừng -> không cần báo lỗi
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi không mong muốn");
                await WriteErrorAsync(context, (int)HttpStatusCode.InternalServerError,
                    "Đã có lỗi xảy ra ở server, vui lòng thử lại sau.", "INTERNAL_ERROR");
            }
        }

        public static async Task WriteErrorAsync(HttpContext context, int statusCode, string message, string code, object? details = null)
        {
            if (context.Response.HasStarted) return;
            context.Response.Clear();
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.StatusCode = statusCode;
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { message, code, details }, Json));
        }
    }
}
