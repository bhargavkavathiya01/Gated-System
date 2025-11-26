namespace Gated_System.Models
{
    public class PropertyModel
    {
        public int Id { get; set; }
        public string PropertyName { get; set; } = null!;
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? Pincode { get; set; }
        public int BuilderId { get; set; }
        public string? IsVerified { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }
    }

    public class PropertyCreateModel
    {
        public string PropertyName { get; set; } = null!;
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? Pincode { get; set; }
        public int BuilderId { get; set; }          
    }
}
