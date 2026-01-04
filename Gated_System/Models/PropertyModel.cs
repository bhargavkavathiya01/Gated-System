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
        public int buildingCount { get; set; }
        public List<string> Buildings { get; set; } = new List<string>();
    }

    public class CreateSecretaryModel
    {
        public int PropertyId { get; set; }
        public int UserId { get; set; }  
        public int RoleId { get; set; }
        public int CreatedBy { get; set; } //Builder Id
    }

    public class CreateCommitteeModel
    {
        public int PropertyId { get; set; }
        public int UserId { get; set; }
        public int RoleId { get; set; }
        public int CreatedBy { get; set; } //Secretory Id
    }

    public class CreateFlatOwnerModel
    {
        public int PropertyId { get; set; }
        public int BuildingId { get; set; }
        public int FlatNo { get; set; }
        public int UserId { get; set; }
        public int RoleId { get; set; }
        public int CreatedBy { get; set; } //Builder Id
    }

    public class PropertyViewModel
    {
        public int Id { get; set; }
        public string PropertyName { get; set; } = "";
        public string Address { get; set; } = "";
        public string City { get; set; } = "";
        public string Pincode { get; set; } = "";
        public int BuilderId { get; set; }
        public string IsVerified { get; set; } = "";
    }

    public class AdminPropertyViewModel
    {
        public int PropertyId { get; set; }
        public string PropertyName { get; set; } = "";
        public string Address { get; set; } = "";
        public string City { get; set; } = "";
        public string Pincode { get; set; } = "";
        public int BuilderId { get; set; }
        public string BuilderName { get; set; }
        public string IsVerified { get; set; } = "";
        public DateTime CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }
    }

    public class PropertyMemberRequest
    {
        public int PropertyId { get; set; }
    }


    public class PropertyMemberDetailsResponse
    {
        public int TotalResidents { get; set; }
        public int TotalFlatowners { get; set; }
        public List<SecretaryDetailDto> SecretaryDetails { get; set; } = new();
        public List<SecurityDetailDto> SecurityDetails { get; set; } = new();
    }

    public class SecretaryDetailDto
    {
        public int Id { get; set; }
        public string Firstname { get; set; } = string.Empty;
        public string Lastname { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class SecurityDetailDto
    {
        public int Id { get; set; }
        public string Firstname { get; set; } = string.Empty;
        public string Lastname { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
