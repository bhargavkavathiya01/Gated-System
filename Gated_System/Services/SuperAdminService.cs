using Gated_System.Models;
using Gated_System.Repositories;

namespace Gated_System.Services
{
    public class SuperAdminService :ISuperAdminService
    {
        private readonly ISuperAdminRepository _repo;
        public SuperAdminService(ISuperAdminRepository repo) => _repo = repo;

        public async Task<IEnumerable<AdminPropertyViewModel>> GetAllPropertiesAsync()
        {
            var result = await _repo.GetAllPropertiesAsync();
            return result ?? Enumerable.Empty<AdminPropertyViewModel>();
        }

        public async Task<IEnumerable<AdminPropertyViewModel>> GetAllPropertiesByStatusAsync(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) throw new ApplicationException("Status required.");
            var result = await _repo.GetAllPropertiesByStatusAsync(status);
            return result ?? Enumerable.Empty<AdminPropertyViewModel>();
        }

        public async Task UpdatePropertyVerificationAsync(UpdatePropertyVerificationModel dto)
        {
            if (dto == null) throw new ApplicationException("Request body is required.");
            if (dto.PropertyId <= 0) throw new ApplicationException("Invalid property id.");
            if (string.IsNullOrWhiteSpace(dto.IsVerified)) throw new ApplicationException("Verification status is required.");

            // normalize status value (optional)
            dto.IsVerified = dto.IsVerified.Trim();

            // call repository to perform update
            await _repo.UpdatePropertyVerificationRepoAsync(dto);
        }
    }
}
