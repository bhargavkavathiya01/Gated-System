using Gated_System.Models;
using System.Threading.Tasks;


namespace Gated_System.Repositories
{
    public interface IAuthRepository
    {
        Task<UserModel?> GetByEmailAsync(string email);
        Task<ServiceResult<UserResponseModel>> AuthenticateAsync(string user, string password);
        Task<UserResponseModel?> GetByIdAsync(int id);
        Task<int> CreateAsync(UserModel user);
        Task AssignRoleAsync(int userId, int roleId);
        Task<IEnumerable<UserPropertyRole>> GetRolesAsync(int userId);
        Task SaveRefreshTokenAsync(int userId, string token, DateTime expiry);
        Task<(int id, DateTime expiry)?> GetRefreshTokenAsync(string token);
        Task DeleteRefreshTokenAsync(string token);
        Task<int> CreatePropertyAsync(PropertyCreateModel property);
        Task<bool> UpdatePasswordAsync(UpdatePasswordModel model);
        Task DeleteAccountAsync(int? targetUserId, string? user, int modifiedBy);
        Task DeleteAccountByEmailAndPhoneAsync(string email, string phone);
        Task<IEnumerable<RegisterTypeModel>> GetRegisterTypesAsync();
        Task<string> SaveDeviceTokenRepoAsync(UserDeviceTokenModel model);
        Task<string> GetPropertyVerificationStatusAsync(int propertyId);
    }
}
