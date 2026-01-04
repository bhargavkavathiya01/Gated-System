using Gated_System.Models;

namespace Gated_System.Services
{
    public interface IUserService
    {
        Task<ServiceResult<bool>> UpdateProfileAsync(UserModel dto);
        Task<ServiceResult<bool>> ResetPasswordAsync(ResetPasswordRequest request);
    }
}
