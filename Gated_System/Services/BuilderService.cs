using Gated_System.Models;
using Gated_System.Repositories;

namespace Gated_System.Services
{
    public class BuilderService : IBuilderService
    {
        private readonly IBuilderRepository _repo;

        public BuilderService(IBuilderRepository repo)
        {
            _repo = repo;
        }

        public async Task<int> CreateSecretaryAsync(CreateSecretaryModel dto)
        {
            // Basic validation
            if (dto.PropertyId <= 0) throw new ApplicationException("Invalid PropertyId.");
            if (dto.UserId <= 0) throw new ApplicationException("Invalid UserId.");
            if (dto.RoleId <= 0) throw new ApplicationException("Invalid RoleId.");

            // If you need additional business rules (e.g., check user belongs to builder), do them here.
            // Then call repository to perform DB insertion.
            var id = await _repo.CreateSecretaryRepoAsync(dto);

            // Optionally assign role if not handled by SP:
            // await _repo.AssignRoleAsync(id, dto.RoleId);

            return id;
        }
    }
}
