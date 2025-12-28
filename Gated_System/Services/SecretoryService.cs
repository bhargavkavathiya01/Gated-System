using Gated_System.Models;
using Gated_System.Repositories;

namespace Gated_System.Services
{
    public class SecretoryService : ISecretoryService
    {
        private readonly ISecretoryRepository _repo;

        public SecretoryService(ISecretoryRepository repo)
        {
            _repo = repo;
        }

        public async Task<int> CreateCommitteeAsync(CreateCommitteeModel dto)
        {
            // Basic validation
            if (dto.PropertyId <= 0) throw new ApplicationException("Invalid PropertyId.");
            if (dto.UserId <= 0) throw new ApplicationException("Invalid UserId.");
            if (dto.RoleId <= 0) throw new ApplicationException("Invalid RoleId.");

            var id = await _repo.CreateCommitteeRepoAsync(dto);
            return id;
        }
    }
}
