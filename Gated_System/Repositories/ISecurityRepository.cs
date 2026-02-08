using System.Text.Json;

namespace Gated_System.Repositories
{
    public interface ISecurityRepository
    {
        Task<JsonDocument> VerifyQrRawAsync(object payload);
        Task<JsonDocument> CreateVisitorLogRawAsync(object payload);
        Task<JsonDocument> UpdateVisitorRequestStatusRawAsync(object payload);
        Task<JsonDocument> UpdateVisitorLogExitRawAsync(object payload);
        Task<int> CreateManualVisitorRequestAsync(object payload);
    }
}
