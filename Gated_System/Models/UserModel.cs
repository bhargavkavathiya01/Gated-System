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
        public bool IsActive { get; set; } = true;
        public int CreatedBy { get; set; }
        public int? RegisterTypeId { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public IFormFile? ProfileImage { get; set; }
    }

    public class UserProfileUpdateModel
    {
        public int Id { get; set; }
        public string? Firstname { get; set; }
        public string? Middlename { get; set; }
        public string? Lastname { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Password { get; set; }
        public int? ModifiedBy { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public IFormFile? ProfileImage { get; set; }
    }

    public class RegisterTypeModel
    {
        public int Id { get; set; }
        public string RegisterTypeName { get; set; } = "";
        public bool IsActive { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }
    }
    public class UserListResponseModel
    {
        public int Id { get; set; }
        public string Firstname { get; set; } = "";
        public string Middlename { get; set; } = "";
        public string Lastname { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public bool IsActive { get; set; }
        public int? RoleId { get; set; }
        public string? RoleName { get; set; }
    }

    public class UserResponseModel
    {
        public int Id { get; set; }
        public string Firstname { get; set; } = "";
        public string Middlename { get; set; } = "";
        public string Lastname { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string? PermanentQR { get; set; } = "";
        //public string? ProfileImage { get; set; } = "";
        public int? UserRegistrationTypeId { get; set; }
        public string? ProfilePictureUrl { get; set; } = "";
    }

    public class RegisterModel
    {
        public string Firstname { get; set; } = "";
        public string Middlename { get; set; } = "";
        public string Lastname { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Password { get; set; } = "";
        public int RegisterTypeId { get; set; }
    }

    public class LoginModel
    {
        public string User { get; set; } = "";
        public string Password { get; set; } = "";
        public string? DeviceToken { get; set; }
        public string? Platform { get; set; }
    }

    public class RefreshTokenModel
    {
        public string RefreshToken { get; set; } = "";
    }
    public class EmailOrPhoneModel
    {
        public string user { get; set; } = "";
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

        public List<BuildingData> BuildingData { get; set; } = new List<BuildingData>();
    }

    public class BuildingData
    {
        public int BuildingId { get; set; }
        public string BuildingName { get; set; } = "";
    }

    public class ResetPasswordRequest
    {
        public int UserId { get; set; }
        public string OldPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class UpdatePasswordModel
    {
        public int UserId { get; set; }
        public string NewPassword { get; set; } = string.Empty;
        public int ModifiedBy { get; set; }
    }

    public class UpdateFcmTokenModel
    {
        public int UserId { get; set; }
        public string FcmToken { get; set; } = string.Empty;
        public int ModifiedBy { get; set; }
    }

    public class ForgotPasswordRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public class UserDeviceTokenModel
    {
        public int UserId { get; set; }
        public string DeviceToken { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
    }

    public class PGMemberRequest
    {
        public int PropertyId { get; set; }
        public int BuildingId { get; set; }
        public string FlatNumber { get; set; } = string.Empty;
    }

    public class DeletePGRequest
    {
        public int PropertyId { get; set; }
        public int BuildingId { get; set; }
        public string FlatNumber { get; set; } = string.Empty;
        public int UserId { get; set; }
    }

    public class ServiceResult<T>
    {
        public bool status { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }

        public static ServiceResult<T> Success(T data, string message = "Success")
            => new() { status = true, Data = data, Message = message };

        public static ServiceResult<T> Fail(string message)
            => new() { status = false, Message = message };
    }

}
