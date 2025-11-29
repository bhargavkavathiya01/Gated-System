namespace Gated_System.Models
{
    public class AuthResponseModel
    {
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public DateTime AccessTokenExpiresAt { get; set; }
        public IEnumerable<UserPropertyRole>? Data { get; set; }
    }

    public class UpdatePropertyVerificationModel
    {
        public int PropertyId { get; set; }
        public string IsVerified { get; set; } = ""; // e.g. "Approved" / "Pending" / "Rejected"
        public int? ModifiedBy { get; set; }
    }


}
