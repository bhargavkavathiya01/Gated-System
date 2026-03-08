using Gated_System.Models;

namespace Gated_System.Services
{
    public interface ISecurityService
    {
        Task<object> VerifyQrAsync(VerifyQrRequest req,int SecurityId);
        Task CheckoutAsync(CheckoutRequest req);
        Task<int> CreateManualVisitorAsync(ManualEntryRequest req, int securityId);
        Task<int> CreateEmergencyEntryAsync(EmergencyEntryRequest req, int securityId);
    }
}
