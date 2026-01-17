using Gated_System.Models;

namespace Gated_System.Repositories
{
    public interface IUserRepository
    {
        Task<bool> UpdateUserAsync(UserModel user);
        Task<bool> UpdatePasswordAsync(UpdatePasswordModel model);
        Task<bool> UpdateFcmTokenAsync(UpdateFcmTokenModel model);
    }
}
