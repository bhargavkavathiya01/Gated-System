namespace Gated_System.Models
{
    public class CreateVisitorDto
    {
        public string VisitorName { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Purpose { get; set; } = "";
        public int PropertyId { get; set; }
        public int BuildingId { get; set; }
        public string FlatId { get; set; }
        public int RequestedBy { get; set; }
        public int ExpiryMinutes { get; set; } = 60;
        public string QrType { get; set; } = "time"; // "time" or "one_time"
        public int? MaxUses { get; set; } = null;
    }

    public class CreatedVisitorResult
    {
        public int Id { get; set; }
        public string QrToken { get; set; } = "";
        public string QrImageBase64 { get; set; } = "";
        public DateTime? ExpiryUtc { get; set; }
    }

    public class VerifyQrRequest
    {
        public string QrToken { get; set; } = "";
        public int PropertyId { get; set; }
        //public int SecurityId { get; set; }
    }

    public class CheckoutRequest
    {
        public int VisitorLogId { get; set; }
        public string? Remarks { get; set; }
    }

}
