using Gated_System.Models;

namespace Gated_System.Services
{
    public interface IAuthService
    {
        Task<RegisterModel> RegisterAsync(RegisterModel dto);
        Task<AuthResponseModel?> LoginAsync(LoginModel dto);
        Task<AuthResponseModel?> RefreshAsync(string refreshToken);
        Task<int> CreatePropertyAsync(PropertyCreateModel property);
    }
}
