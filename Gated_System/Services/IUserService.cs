using Gated_System.Models;

namespace Gated_System.Services
{
    public interface IUserService
    {
        Task<ServiceResult<bool>> UpdateProfileAsync(UserProfileUpdateModel dto);
        Task<ServiceResult<bool>> ResetPasswordAsync(ResetPasswordRequest request);
        Task<ServiceResult<bool>> UpdateFcmTokenAsync(UpdateFcmTokenModel model);
        Task<ServiceResult<IEnumerable<UserListResponseModel>>> GetAllUsersAsync();
        Task<ServiceResult<int>> AddSosContactAsync(int userId, AddSosContactModel dto);
        Task<ServiceResult<IEnumerable<SosContactResponseModel>>> GetSosContactsAsync(int userId);
        Task<ServiceResult<bool>> RemoveSosContactAsync(int id, int userId);
        Task<ServiceResult<bool>> UpdateSosRelationAsync(int id, int userId, UpdateSosRelationModel dto);
        Task<ServiceResult<bool>> TriggerSosAsync(int userId);
    }
}
