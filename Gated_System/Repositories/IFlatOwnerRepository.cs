using Gated_System.Models;
using System.Text.Json;
using static Gated_System.Models.QRModel;

namespace Gated_System.Repositories
{
    public interface IFlatOwnerRepository
    {
        Task<int> CreateVisitorRequestAsync(object payload);
        Task<JsonDocument> GetVisitorByIdRawAsync(int id);
        Task<int> CreatePGRepoAsync(CreateFlatOwnerModel model);
        Task<IEnumerable<dynamic>> GetPGMembersAsync(PGMemberRequest request);
        Task<bool> DeletePGMemberAsync(DeletePGRequest request);
        Task<IEnumerable<dynamic>> GetUserQRHistoryAsync(QRHistoryRequest request);
        Task<bool> RevokeQRAsync(RevokeQRRequest request);
        Task<string> SaveDeviceTokenRepoAsync(UserDeviceTokenModel model);
    }
}
