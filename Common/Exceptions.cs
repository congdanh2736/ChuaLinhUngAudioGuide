namespace AudioGuide.Api.Common
{
    /*
     * Ngoại lệ nghiệp vụ dùng chung cho cả 3 lớp.
     * Lớp Service chỉ "ném" các ngoại lệ này (không biết gì về HTTP);
     * ExceptionMiddleware bắt lại và đổi thành mã HTTP + JSON {message, code}.
     * Code là mã máy đọc được (vd "POI_NOT_FOUND") để frontend xử lý từng trường hợp.
     */
    public class BusinessException : Exception
    {
        public int StatusCode { get; }
        public string Code { get; }
        public object? Details { get; }

        public BusinessException(string message, string code = "BUSINESS_RULE_VIOLATION",
            int statusCode = StatusCodes.Status400BadRequest, object? details = null) : base(message)
        {
            StatusCode = statusCode;
            Code = code;
            Details = details;
        }
    }

    // Không tìm thấy dữ liệu -> 404
    public class NotFoundException : BusinessException
    {
        public NotFoundException(string message, string code = "NOT_FOUND")
            : base(message, code, StatusCodes.Status404NotFound) { }
    }

    // Trùng dữ liệu / đang bị tham chiếu -> 409
    public class ConflictException : BusinessException
    {
        public ConflictException(string message, string code = "CONFLICT", object? details = null)
            : base(message, code, StatusCodes.Status409Conflict, details) { }
    }

    // Chưa xác thực / token sai -> 401
    public class UnauthorizedException : BusinessException
    {
        public UnauthorizedException(string message, string code = "UNAUTHORIZED")
            : base(message, code, StatusCodes.Status401Unauthorized) { }
    }

    // Không đủ quyền -> 403
    public class ForbiddenException : BusinessException
    {
        public ForbiddenException(string message = "Bạn không có quyền thực hiện thao tác này.", string code = "FORBIDDEN")
            : base(message, code, StatusCodes.Status403Forbidden) { }
    }

    // Thanh toán online chưa xong -> 409 (frontend thử lại sau vài giây)
    public class PaymentPendingException : BusinessException
    {
        public PaymentPendingException(string message)
            : base(message, "PAYMENT_PENDING", StatusCodes.Status409Conflict) { }
    }

    // Vượt giới hạn (vd số câu hỏi chatbot / ngày) -> 429
    public class RateLimitException : BusinessException
    {
        public RateLimitException(string message, string code = "RATE_LIMITED")
            : base(message, code, StatusCodes.Status429TooManyRequests) { }
    }

    // Dịch vụ bên ngoài (dịch máy, TTS, AI) lỗi -> 503
    public class ExternalServiceException : BusinessException
    {
        public ExternalServiceException(string message, string code = "EXTERNAL_SERVICE_ERROR")
            : base(message, code, StatusCodes.Status503ServiceUnavailable) { }
    }
}
