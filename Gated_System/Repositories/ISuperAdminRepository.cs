using Gated_System.Models;

namespace Gated_System.Repositories
{
    public interface ISuperAdminRepository
    {
        Task<IEnumerable<AdminPropertyViewModel>> GetAllPropertiesAsync();
        Task<IEnumerable<AdminPropertyViewModel>> GetAllPropertiesByStatusAsync(string status);
        Task UpdatePropertyVerificationRepoAsync(UpdatePropertyVerificationModel dto);
    }
}
