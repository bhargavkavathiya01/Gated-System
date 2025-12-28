using Gated_System.Models;

namespace Gated_System.Services
{
    public interface ISecretoryService
    {
        Task<int> CreateCommitteeAsync(CreateCommitteeModel dto);
    }
}
