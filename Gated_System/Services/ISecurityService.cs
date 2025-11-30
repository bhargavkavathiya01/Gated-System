using Gated_System.Models;

namespace Gated_System.Services
{
    public interface ISecurityService
    {
        Task<object> VerifyQrAsync(VerifyQrRequest req);
        Task CheckoutAsync(CheckoutRequest req);
    }
}
