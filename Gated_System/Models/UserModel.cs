namespace Gated_System.Models
{
    public class UserModel
    {
        public int Id { get; set; }
        public string Firstname { get; set; } = "";
        public string Middlename { get; set; } = "";
        public string Lastname { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Password { get; set; } = "";
        public byte[] PasswordSalt { get; set; } = Array.Empty<byte>();
        public bool IsActive { get; set; } = true;
        public string CreatedBy { get; set; }
    }

    public class RegisterModel
    {
        public string Firstname { get; set; } = "";
        public string Middlename { get; set; } = "";
        public string Lastname { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public class LoginModel
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public class UserPropertyRole
    {
        public int RoleId { get; set; }          
        public string RoleName { get; set; } = "";

        public int PropertyId { get; set; }
        public int? BuildingId { get; set; }
        public string FlatNumber { get; set; } = "";

        public string? PropertyName { get; set; }
        public string? BuildingName { get; set; }
    }



}
