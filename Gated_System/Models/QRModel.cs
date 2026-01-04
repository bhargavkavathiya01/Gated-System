namespace Gated_System.Models
{
    public class QRModel
    {
        public class QRHistoryRequest
        {
            public int UserId { get; set; }
        }

        public class RevokeQRRequest
        {
            public int RequestId { get; set; }
            public int UserId { get; set; }
        }
    }
}
