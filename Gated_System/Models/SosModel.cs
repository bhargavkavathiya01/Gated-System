namespace Gated_System.Models
{
    public class AddSosContactModel
    {
        public int ContactUserId { get; set; }
        public string? Relation { get; set; }
    }

    public class UpdateSosRelationModel
    {
        public int Id { get; set; }
        public string? Relation { get; set; }
    }

    public class SosContactResponseModel
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int ContactUserId { get; set; }
        public string ContactFullName { get; set; } = "";
        public string ContactEmail { get; set; } = "";
        public string ContactPhone { get; set; } = "";
        public string? ContactProfileImage { get; set; }
        public string? Relation { get; set; }
        public DateTime CreatedOn { get; set; }
    }
}
