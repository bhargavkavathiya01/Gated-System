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

            var id = await _repo.CreateSecretaryRepoAsync(dto);
            return id;
        }

        public async Task<int> CreateFlatOwnerAsync(CreateFlatOwnerModel dto)
        {
            // Basic validation
            if (dto.PropertyId <= 0) throw new ApplicationException("Invalid PropertyId.");
            if (dto.BuildingId <= 0) throw new ApplicationException("Invalid BuildingId.");
            if (dto.FlatNo <= 0) throw new ApplicationException("Invalid FlatNo.");
            if (dto.UserId <= 0) throw new ApplicationException("Invalid UserId.");
            if (dto.RoleId <= 0) throw new ApplicationException("Invalid RoleId.");

            var id = await _repo.CreateFlatOwnerRepoAsync(dto);

            return id;
        }

        public async Task<IEnumerable<PropertyViewModel>> GetPropertiesByBuilderIdAsync(int builderId)
        {
            if (builderId <= 0)
                throw new ApplicationException("Invalid builderId.");

            return await _repo.GetPropertiesByBuilderIdAsync(builderId);
        }

    }
}
