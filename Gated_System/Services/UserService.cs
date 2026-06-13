using Gated_System.Helpers;
using Gated_System.Models;
using Gated_System.Repositories;

namespace Gated_System.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepo;
        private readonly IAuthRepository _authRepo;
        private readonly PushNotificationHelper _push;

        public UserService(IUserRepository repo, IAuthRepository authRepo, PushNotificationHelper push)
        {
            _userRepo = repo;
            _authRepo = authRepo;
            _push = push;
        }

        public async Task<ServiceResult<bool>> UpdateProfileAsync(UserProfileUpdateModel dto)
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

        public async Task<ServiceResult<bool>> UpdateFcmTokenAsync(UpdateFcmTokenModel model)
        {
            try
            {
                // Validate input
                if (model.UserId <= 0)
                    return ServiceResult<bool>.Fail("Invalid UserId");

                if (string.IsNullOrWhiteSpace(model.FcmToken))
                    return ServiceResult<bool>.Fail("FCM token is required");

                // Verify user exists before updating
                var existingUser = await _authRepo.GetByIdAsync(model.UserId);
                if (existingUser == null)
                    return ServiceResult<bool>.Fail("User not found");

                // Update FCM token
                var success = await _userRepo.UpdateFcmTokenAsync(model);

                return success
                    ? ServiceResult<bool>.Success(true, "FCM token updated successfully")
                    : ServiceResult<bool>.Fail("Failed to update FCM token");
            }
            catch (ApplicationException ex)
            {
                return ServiceResult<bool>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.Fail($"Error updating FCM token: {ex.Message}");
            }
        }

        public async Task<ServiceResult<IEnumerable<UserListResponseModel>>> GetAllUsersAsync()
        {
            try
            {
                var users = await _userRepo.GetAllUsersAsync();
                return ServiceResult<IEnumerable<UserListResponseModel>>.Success(users, "Users fetched successfully");
            }
            catch (Exception ex)
            {
                return ServiceResult<IEnumerable<UserListResponseModel>>.Fail(ex.Message);
            }
        }

        public async Task<ServiceResult<int>> AddSosContactAsync(int userId, AddSosContactModel dto)
        {
            try
            {
                if (dto.ContactUserId <= 0) return ServiceResult<int>.Fail("Invalid ContactUserId.");
                var id = await _userRepo.AddSosContactAsync(userId, dto.ContactUserId, dto.Relation);
                return ServiceResult<int>.Success(id, "SOS contact added successfully");
            }
            catch (ApplicationException ex) { return ServiceResult<int>.Fail(ex.Message); }
            catch (Exception ex)            { return ServiceResult<int>.Fail(ex.Message); }
        }

        public async Task<ServiceResult<IEnumerable<SosContactResponseModel>>> GetSosContactsAsync(int userId)
        {
            try
            {
                var contacts = await _userRepo.GetSosContactsAsync(userId);
                return ServiceResult<IEnumerable<SosContactResponseModel>>.Success(contacts, "SOS contacts fetched successfully");
            }
            catch (Exception ex) { return ServiceResult<IEnumerable<SosContactResponseModel>>.Fail(ex.Message); }
        }

        public async Task<ServiceResult<bool>> RemoveSosContactAsync(int id, int userId)
        {
            try
            {
                var result = await _userRepo.RemoveSosContactAsync(id, userId);
                return ServiceResult<bool>.Success(result, "SOS contact removed successfully");
            }
            catch (ApplicationException ex) { return ServiceResult<bool>.Fail(ex.Message); }
            catch (Exception ex)            { return ServiceResult<bool>.Fail(ex.Message); }
        }

        public async Task<ServiceResult<bool>> UpdateSosRelationAsync(int id, int userId, UpdateSosRelationModel dto)
        {
            try
            {
                var result = await _userRepo.UpdateSosRelationAsync(id, userId, dto.Relation);
                return ServiceResult<bool>.Success(result, "SOS contact updated successfully");
            }
            catch (ApplicationException ex) { return ServiceResult<bool>.Fail(ex.Message); }
            catch (Exception ex)            { return ServiceResult<bool>.Fail(ex.Message); }
        }

        public async Task<ServiceResult<bool>> TriggerSosAsync(int userId)
        {
            try
            {
                var tokens = await _userRepo.GetSosContactDeviceTokensAsync(userId);
                if (tokens.Count == 0)
                    return ServiceResult<bool>.Fail("No SOS contacts found or none have the app installed.");

                await _push.SendToDevicesAsync(
                    tokens,
                    "🚨 SOS Alert",
                    "A contact needs your help urgently!",
                    new Dictionary<string, string>
                    {
                        { "type", "sos_alert" },
                        { "senderId", userId.ToString() },
                        { "notificationId", "99" }
                    }
                );

                return ServiceResult<bool>.Success(true, $"SOS alert sent to {tokens.Count} contact(s).");
            }
            catch (Exception ex) { return ServiceResult<bool>.Fail(ex.Message); }
        }
    }
}
