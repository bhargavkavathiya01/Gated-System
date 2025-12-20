using Gated_System.Models;

namespace Gated_System.Services
{
    public interface IAuthService
    {
        Task<RegisterModel> RegisterAsync(RegisterModel dto);
        Task<ServiceResult<AuthResponseModel>> LoginAsync(LoginModel dto);
        public Task<UserRoleResponseModel?> GetUserByToken(int UserId);
        Task<RefreshTokenResponseModel?> RefreshAsync(string refreshToken);
        Task<int> CreatePropertyAsync(PropertyCreateModel property);
    }
}
