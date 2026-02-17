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
        public string FlatNo { get; set; }
        public int? UserId { get; set; }
        public int RoleId { get; set; }
        public string? GuestType { get; set; }
        public int CreatedBy { get; set; } //Builder Id

        // User Registration Fields
        public string? Firstname { get; set; }
        public string? Middlename { get; set; }
        public string? Lastname { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Password { get; set; }
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
        public List<PropertyMembersModel> MemberDetails { get; set; } = new();
        //public int TotalResidents { get; set; }
        //public int TotalFlatowners { get; set; }
        //public List<SecretaryDetailDto> SecretaryDetails { get; set; } = new();
        //public List<SecurityDetailDto> SecurityDetails { get; set; } = new();
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

    public class FlatOwnerRequestModel
    {
        public int Id { get; set; }
        public int PropertyId { get; set; }
        public int BuildingId { get; set; }
        public string FlatNumber { get; set; } = null!;
        public int UserId { get; set; }
        public int RoleId { get; set; }
        public string? GuestType { get; set; }
        public int RequestedBy { get; set; }
        public string Status { get; set; } = "Pending";
        public int? ApprovedBy { get; set; }
        public DateTime? ApprovedOn { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime CreatedOn { get; set; }
    }

    public class CreateFlatOwnerRequestModel
    {
        public int PropertyId { get; set; }
        public int BuildingId { get; set; }
        public string FlatNo { get; set; } = null!;
        public int? UserId { get; set; }
        public int RoleId { get; set; }
        public string? GuestType { get; set; }
        public int RequestedBy { get; set; } // Builder Id
    }

    public class FlatOwnerRequestResponseModel
    {
        public int Id { get; set; }
        public int PropertyId { get; set; }
        public string PropertyName { get; set; } = "";
        public int BuildingId { get; set; }
        public string BuildingName { get; set; } = "";
        public string FlatNumber { get; set; } = "";
        public int UserId { get; set; }
        public string UserFirstName { get; set; } = "";
        public string UserLastName { get; set; } = "";
        public string UserEmail { get; set; } = "";
        public string UserPhone { get; set; } = "";
        public int RoleId { get; set; }
        public string RoleName { get; set; } = "";
        public string? GuestType { get; set; }
        public int RequestedBy { get; set; }
        public string RequestedByName { get; set; } = "";
        public string Status { get; set; } = "";
        public int? ApprovedBy { get; set; }
        public string? ApprovedByName { get; set; }
        public DateTime? ApprovedOn { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime CreatedOn { get; set; }
    }

    public class ApproveFlatOwnerRequestModel
    {
        public int RequestId { get; set; }
        public string Action { get; set; } = "Approved"; // "Approved" or "Rejected"
        public string? RejectionReason { get; set; }
    }

    public class PropertyMembersModel
    {
        public int UserId { get; set; }
        public string Firstname { get; set; } = string.Empty;
        public string Lastname { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        public List<Roles> Roles { get; set; } = new();
    }

    public class Roles
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
