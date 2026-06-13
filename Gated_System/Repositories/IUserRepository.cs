using Gated_System.Models;

namespace Gated_System.Repositories
{
    public interface IUserRepository
    {
        Task<bool> UpdateUserAsync(UserProfileUpdateModel user);
        Task<bool> UpdatePasswordAsync(UpdatePasswordModel model);
        Task<bool> UpdateFcmTokenAsync(UpdateFcmTokenModel model);
        Task<string?> GetDeviceTokenAsync(int userId);
        Task<IEnumerable<UserListResponseModel>> GetAllUsersAsync();
        Task<int> AddSosContactAsync(int userId, int contactUserId, string? relation);
        Task<IEnumerable<SosContactResponseModel>> GetSosContactsAsync(int userId);
        Task<bool> RemoveSosContactAsync(int id, int userId);
        Task<bool> UpdateSosRelationAsync(int id, int userId, string? relation);
        Task<List<string>> GetSosContactDeviceTokensAsync(int userId);
    }
}
