using Gated_System.Models;
using System.Text.Json;

namespace Gated_System.Repositories
{
    public interface IFlatOwnerRepository
    {
        Task<int> CreateVisitorRequestAsync(object payload);
        Task<JsonDocument> GetVisitorByIdRawAsync(int id);
        Task<int> CreatePGRepoAsync(CreateFlatOwnerModel model);
        Task<IEnumerable<dynamic>> GetPGMembersAsync(PGMemberRequest request);
        Task<bool> DeletePGMemberAsync(DeletePGRequest request);
    }
}
