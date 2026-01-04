using Gated_System.Models;

namespace Gated_System.Services
{
    public interface ISuperAdminService
    {
        Task<IEnumerable<AdminPropertyViewModel>> GetAllPropertiesAsync();
        Task<IEnumerable<AdminPropertyViewModel>> GetAllPropertiesByStatusAsync(string status);
        Task<string> UpdatePropertyVerificationAsync(UpdatePropertyVerificationModel dto);
    }
}
