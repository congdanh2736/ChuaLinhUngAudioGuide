using System.Security.Cryptography;
using AudioGuide.Api.Common;
using AudioGuide.Api.DTOs.Access;
using AudioGuide.Api.DTOs.Common;
using AudioGuide.Api.Interface.Repositories;
using AudioGuide.Api.Interface.Services;
using AudioGuide.Api.Models;
using AutoMapper;
using Microsoft.Extensions.Options;

namespace AudioGuide.Api.Services
{
    /*
     * Nghiệp vụ QUYỀN TRUY CẬP của du khách.
     * Vòng đời 1 phiên:
     *   Online: Pending --(cổng báo thành công)--> Paid --(khách đổi mã)--> Active --> Expired
     *   Cash  : (nhân viên xác nhận đã thu tiền) Paid --(khách đổi mã)--> Active --> Expired
     *   Bất kỳ: --(thanh toán lỗi / admin thu hồi)--> Cancelled
     */
    public class AccessService : IAccessService
    {
        private readonly IAccessSessionRepository _repository;
        private readonly IJwtService _jwtService;
        private readonly IMapper _mapper;
        private readonly AccessSettings _settings;
        private readonly TimeProvider _clock;

        public AccessService(IAccessSessionRepository repository, IJwtService jwtService, IMapper mapper,
            IOptions<AccessSettings> options, TimeProvider clock)
        {
            _repository = repository;
            _jwtService = jwtService;
            _mapper = mapper;
            _settings = options.Value;
            _clock = clock;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;

        public AccessInfoDto GetInfo() => new()
        {
            PriceVnd = _settings.PriceVnd,
            AccessHours = _settings.AccessHours,
            AccessRequired = _settings.Required
        };

        // Mã ngẫu nhiên 6 ký tự (bộ sinh số ngẫu nhiên an toàn), thử lại nếu trùng
        private async Task<string> UniqueCodeAsync()
        {
            for (var attempt = 0; attempt < 10; attempt++)
            {
                var code = RandomNumberGenerator.GetString(BusinessConstants.AccessCodeAlphabet, BusinessConstants.AccessCodeLength);
                if (!await _repository.CodeExistsAsync(code)) return code;
            }
            throw new BusinessException("Không tạo được mã truy cập, vui lòng thử lại", "CODE_GENERATION_FAILED");
        }

        public async Task<AccessSession> CreateSessionAsync(AccessMethod method, AccessStatus status, string? createdById = null,
            string? note = null, int discountPercent = 0)
        {
            // Kiểm tra lại ở tầng nghiệp vụ (không tin dữ liệu từ frontend), rồi tính số tiền sau giảm
            if (!BusinessConstants.DiscountPercents.Contains(discountPercent))
                throw new BusinessException("Mức giảm giá không hợp lệ", "INVALID_DISCOUNT");
            var amount = _settings.PriceVnd * (100 - discountPercent) / 100;   // 50.000 giảm 10% = 45.000
            var now = Now;
            var session = new AccessSession
            {
                Code = await UniqueCodeAsync(),
                Method = method,
                Status = status,
                Amount = amount,
                Currency = "VND",
                CreatedById = createdById,
                Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                CreatedAt = now,
                CodeExpiresAt = now.AddHours(_settings.CodeTtlHours),
                PaidAt = status == AccessStatus.Paid ? now : null
            };
            await _repository.AddAsync(session);
            return session;
        }

        // Nhân viên đã thu tiền mặt -> sinh mã ngắn đưa cho khách
        public async Task<List<AccessSessionDto>> CreateCashCodesAsync(string staffId, int quantity, string? note)
        {
            if (quantity < 1 || quantity > BusinessConstants.MaxCashCodesPerRequest)
                throw new BusinessException($"Số lượng mã phải từ 1 đến {BusinessConstants.MaxCashCodesPerRequest}", "INVALID_QUANTITY");
            var result = new List<AccessSessionDto>();
            for (var i = 0; i < quantity; i++)
                result.Add(_mapper.Map<AccessSessionDto>(await CreateSessionAsync(AccessMethod.Cash, AccessStatus.Paid, staffId, note)));
            return result;
        }

        // Khách nhập mã -> nhận token du khách
        public async Task<AccessTokenDto> RedeemAsync(string code)
        {
            code = code.Trim().ToUpperInvariant();
            var session = await _repository.GetByCodeAsync(code)
                          ?? throw new NotFoundException("Mã truy cập không đúng", "INVALID_ACCESS_CODE");
            var now = Now;
            switch (session.Status)
            {
                case AccessStatus.Pending:
                    throw new PaymentPendingException("Thanh toán chưa hoàn tất, vui lòng chờ trong giây lát");
                case AccessStatus.Cancelled:
                    throw new BusinessException("Mã truy cập đã bị hủy", "ACCESS_CANCELLED");
                case AccessStatus.Paid:
                    if (session.CodeExpiresAt < now)
                    {
                        session.Status = AccessStatus.Expired;
                        await _repository.UpdateAsync(session);
                        throw new BusinessException("Mã truy cập đã hết hạn", "ACCESS_EXPIRED");
                    }
                    // Lần đầu đổi mã -> kích hoạt, bắt đầu tính thời gian sử dụng
                    session.Status = AccessStatus.Active;
                    session.ActivatedAt = now;
                    session.AccessExpiresAt = now.AddHours(_settings.AccessHours);
                    await _repository.UpdateAsync(session);
                    break;
                case AccessStatus.Active:
                    // Đã kích hoạt: cho đổi lại (vd khách xóa dữ liệu trình duyệt) nếu còn hạn
                    if (session.AccessExpiresAt < now)
                    {
                        session.Status = AccessStatus.Expired;
                        await _repository.UpdateAsync(session);
                        throw new BusinessException("Quyền truy cập đã hết hạn", "ACCESS_EXPIRED");
                    }
                    break;
                default:
                    throw new BusinessException("Quyền truy cập đã hết hạn", "ACCESS_EXPIRED");
            }
            var expiresAt = session.AccessExpiresAt!.Value;
            return new AccessTokenDto { Token = _jwtService.CreateVisitorToken(session.Id, expiresAt), ExpiresAt = expiresAt };
        }

        // Token du khách hợp lệ về chữ ký vẫn phải kiểm tra phiên còn hoạt động (admin có thể thu hồi)
        public async Task<AccessSession> VerifySessionAsync(int sessionId)
        {
            var session = await _repository.GetByIdAsync(sessionId);
            if (session == null || session.Status != AccessStatus.Active)
                throw new UnauthorizedException("Quyền truy cập không còn hiệu lực", "ACCESS_REVOKED");
            if (session.AccessExpiresAt < Now)
            {
                session.Status = AccessStatus.Expired;
                await _repository.UpdateAsync(session);
                throw new UnauthorizedException("Quyền truy cập đã hết hạn", "ACCESS_EXPIRED");
            }
            return session;
        }

        public async Task<PagedResult<AccessSessionDto>> SearchAsync(AccessStatus? status, int page, int size)
        {
            // Trạng thái trong DB chỉ đổi khi có thao tác -> cập nhật các phiên đã quá hạn trước khi đọc
            await _repository.MarkExpiredAsync(Now);
            page = Math.Max(page, 1);
            size = Math.Clamp(size, 1, 100);
            var (items, total) = await _repository.SearchAsync(status, (page - 1) * size, size);
            return new PagedResult<AccessSessionDto>
            {
                Items = _mapper.Map<List<AccessSessionDto>>(items), Total = total, Page = page, Size = size
            };
        }

        public async Task<AccessSessionDto> RevokeAsync(int sessionId)
        {
            var session = await _repository.GetByIdAsync(sessionId)
                          ?? throw new NotFoundException("Không tìm thấy phiên truy cập", "SESSION_NOT_FOUND");
            session.Status = AccessStatus.Cancelled;
            await _repository.UpdateAsync(session);
            return _mapper.Map<AccessSessionDto>(session);
        }
    }
}
