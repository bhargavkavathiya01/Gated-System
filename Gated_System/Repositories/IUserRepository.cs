using Gated_System.Models;
using System.Threading.Tasks;


namespace Gated_System.Repositories
{
    public interface IUserRepository
    {
        Task<UserModel?> GetByEmailAsync(string email);
        Task<UserModel?> AuthenticateAsync(string email, string password);
        Task<UserModel?> GetByIdAsync(int id);
        Task<int> CreateAsync(UserModel user);
        Task AssignRoleAsync(int userId, int roleId);
        Task<IEnumerable<UserPropertyRole>> GetRolesAsync(int userId);
        Task SaveRefreshTokenAsync(int userId, string token, DateTime expiry);
        Task<(int id, DateTime expiry)?> GetRefreshTokenAsync(string token);
        Task DeleteRefreshTokenAsync(string token);
        Task<int> CreatePropertyAsync(PropertyCreateModel property);
    }
}
