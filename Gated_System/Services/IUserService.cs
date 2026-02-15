using Gated_System.Models;

namespace Gated_System.Services
{
    public interface IUserService
    {
        Task<ServiceResult<bool>> UpdateProfileAsync(UserProfileUpdateModel dto);
        Task<ServiceResult<bool>> ResetPasswordAsync(ResetPasswordRequest request);
        Task<ServiceResult<bool>> UpdateFcmTokenAsync(UpdateFcmTokenModel model);
    }
}
