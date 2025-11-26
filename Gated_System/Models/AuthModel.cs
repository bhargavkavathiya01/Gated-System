namespace Gated_System.Models
{
    public class AuthResponseModel
    {
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public DateTime AccessTokenExpiresAt { get; set; }
        public IEnumerable<UserPropertyRole>? Data { get; set; }
    }

}
