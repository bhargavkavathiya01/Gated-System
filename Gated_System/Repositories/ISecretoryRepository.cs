using Gated_System.Models;

namespace Gated_System.Repositories
{
    public interface ISecretoryRepository
    {
        Task<int> CreateCommitteeRepoAsync(CreateCommitteeModel model);
    }
}
