using Gated_System.Models;
using Gated_System.Repositories;

namespace Gated_System.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepo;
        private readonly IAuthRepository _authRepo;

        public UserService(IUserRepository repo, IAuthRepository authRepo)
        {
            _userRepo = repo;
            _authRepo = authRepo;
        }

        public async Task<ServiceResult<bool>> UpdateProfileAsync(UserModel dto)
        {
            try
            {
                // Verify user exists before updating
                var existingUser = await _authRepo.GetByIdAsync(dto.Id);
                if (existingUser == null)
                    return ServiceResult<bool>.Fail("User not found");

                var success = await _userRepo.UpdateUserAsync(dto);
                return ServiceResult<bool>.Success(success, "Profile updated successfully");
            }
            catch (ApplicationException ex)
            {
                // Catches the "Email already exists" or "Phone number already exists" from Repo
                return ServiceResult<bool>.Fail(ex.Message);
            }
            catch (Exception)
            {
                return ServiceResult<bool>.Fail("An internal error occurred during update");
            }
        }

        public async Task<ServiceResult<bool>> ResetPasswordAsync(ResetPasswordRequest request)
        {
            try
            {
                // 1. Get user details to get the Email/Phone for authentication check
                var user = await _authRepo.GetByIdAsync(request.UserId);
                if (user == null) return ServiceResult<bool>.Fail("User not found");

                // 2. Verify Old Password using your existing AuthRepository logic
                // We use user.Email (or user.Phone) and the OldPassword provided
                var authCheck = await _authRepo.AuthenticateAsync(user.Email, request.OldPassword);

                if (!authCheck.status)
                    return ServiceResult<bool>.Fail("Invalid old password");

                // 3. Prepare the update model
                var updateModel = new UpdatePasswordModel
                {
                    UserId = request.UserId,
                    NewPassword = request.NewPassword,
                    ModifiedBy = request.UserId
                };

                // 4. Update the password in DB
                var success = await _userRepo.UpdatePasswordAsync(updateModel);

                return success
                    ? ServiceResult<bool>.Success(true, "Password reset successfully")
                    : ServiceResult<bool>.Fail("Failed to update password");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.Fail($"Error: {ex.Message}");
            }
        }
    }
}
