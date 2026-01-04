using Gated_System.Helpers;
using Gated_System.Models;
using Gated_System.Repositories;
using Microsoft.Extensions.Options;
using static Gated_System.Helpers.SendEmailHelper;

namespace Gated_System.Services
{
    public class AuthService : IAuthService
    {
        private readonly IAuthRepository _repo;
        private readonly SendEmailHelper _emailHelper;
        private readonly PasswordHasher _hasher;
        private readonly JwtTokenGenerator _jwt;
        private readonly JwtOptions _jwtOptions;

        public AuthService(IAuthRepository repo, PasswordHasher hasher, JwtTokenGenerator jwt, IOptions<JwtOptions> jwtOptions,SendEmailHelper emailHelper)
        {
            _repo = repo;
            _emailHelper = emailHelper;
            _hasher = hasher;
            _jwt = jwt;
            _jwtOptions = jwtOptions.Value;
        }

        public async Task<RegisterModel> RegisterAsync(RegisterModel dto)
        {
            var existing = await _repo.GetByEmailAsync(dto.Email);
            if (existing != null) throw new ApplicationException("Email already registered.");

            //var (hash, salt) = _hasher.HashPassword(dto.Password);
            var user = new UserModel
            {
                Firstname = dto.Firstname,
                Middlename=dto.Middlename,
                Lastname=dto.Lastname,
                Email = dto.Email,
                Phone = dto.Phone,
                Password = dto.Password,
                //PasswordSalt = salt,
                IsActive = true
            };

            var userId = await _repo.CreateAsync(user);

            // assign default role - FlatOwner or none. For now assign 'FlatOwner' as example
            await _repo.AssignRoleAsync(userId,7);

            // generate tokens
            //var roles = await _repo.GetRolesAsync(userId);
            //var accessToken = _jwt.GenerateAccessToken(userId, user.Email, roles, out var accessExpiry);
            //var refreshToken = _jwt.GenerateRefreshToken();
            //var refreshExpiry = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays);

            //await _repo.SaveRefreshTokenAsync(userId, refreshToken, refreshExpiry);

            //return new AuthResponseModel
            //{
            //    AccessToken = accessToken,
            //    RefreshToken = refreshToken,
            //    AccessTokenExpiresAt = accessExpiry
            //};
            return new RegisterModel
            {
                Firstname = dto.Firstname,
                Middlename=dto.Middlename,
                Lastname=dto.Lastname,
                Email = dto.Email,
                Phone = dto.Phone
            };
        }

        public async Task<ServiceResult<AuthResponseModel>> LoginAsync(LoginModel dto)
        {
            var userResult = await _repo.AuthenticateAsync(dto.User, dto.Password);
            if (!userResult.status)
                return ServiceResult<AuthResponseModel>.Fail(userResult.Message);
            //if (!_hasher.Verify(dto.Password, user.PasswordHash, user.PasswordSalt)) return null;
            var user = userResult.Data!;

            var roles = await _repo.GetRolesAsync(user.Id);
            var accessToken = _jwt.GenerateAccessToken(user.Id, user.Email, roles, out var accessExpiry);
            var refreshToken = _jwt.GenerateRefreshToken();
            var refreshExpiry = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays);

            await _repo.SaveRefreshTokenAsync(user.Id, refreshToken, refreshExpiry);

            return ServiceResult<AuthResponseModel>.Success(new AuthResponseModel
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                AccessTokenExpiresAt = accessExpiry,
                UserData = user,
                RoleData = roles
            }, "Login successful");
        }

        public async Task<UserRoleResponseModel?> GetUserByToken(int UserId)
        {
            var user = await _repo.GetByIdAsync(UserId);
            if (user == null) return null;
            var roles = await _repo.GetRolesAsync(user.Id);

            return new UserRoleResponseModel
            {
                UserData=user,
                RoleData = roles
            };
        }

        public async Task<RefreshTokenResponseModel?> RefreshAsync(string refreshToken)
        {
            var r = await _repo.GetRefreshTokenAsync(refreshToken);
            if (r == null) return null;
            var (Userid, expiry) = r.Value;
            if (expiry < DateTime.UtcNow)
            {
                await _repo.DeleteRefreshTokenAsync(refreshToken);
                return null;
            }

            var user = await _repo.GetByIdAsync(Userid);
            if (user == null) return null;
            var roles = await _repo.GetRolesAsync(Userid);
            var accessToken = _jwt.GenerateAccessToken(Userid, user.Email, roles, out var accessExpiry);
            // optional: issue a new refresh token & delete old one
            var newRefresh = _jwt.GenerateRefreshToken();
            var newExpiry = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays);
            await _repo.DeleteRefreshTokenAsync(refreshToken);
            await _repo.SaveRefreshTokenAsync(Userid, newRefresh, newExpiry);

            return new RefreshTokenResponseModel
            {
                AccessToken = accessToken,
                RefreshToken = newRefresh,
                AccessTokenExpiresAt = accessExpiry
            };
        }

        public async Task<int> CreatePropertyAsync(PropertyCreateModel dto)
        {
            // Basic validations (similar pattern to RegisterAsync)
            if (string.IsNullOrWhiteSpace(dto.PropertyName))
                throw new ApplicationException("Property name is required.");

            if (dto.BuilderId <= 0)
                throw new ApplicationException("BuilderId is required.");

            if (dto.Buildings != null && dto.Buildings.Any(b => string.IsNullOrWhiteSpace(b)))
                throw new ApplicationException("All building names must be non-empty.");

            // Call repository that performs both operations inside a transaction
            var id = await _repo.CreatePropertyAsync(dto);
            return id;
        }

        public async Task<ServiceResult<bool>> ForgotPasswordAsync(ForgotPasswordRequest request)
        {
            try
            {
                var user = await _repo.GetByEmailAsync(request.Email);
                if (user == null) return ServiceResult<bool>.Fail("User with this email does not exist.");

                string tempPassword = Guid.NewGuid().ToString().Substring(0, 8);

                var updateModel = new UpdatePasswordModel
                {
                    UserId = user.Id,
                    NewPassword = tempPassword,
                    ModifiedBy = user.Id
                };

                bool dbUpdated = await _repo.UpdatePasswordAsync(updateModel);

                if (!dbUpdated) return ServiceResult<bool>.Fail("Failed to update password in database.");

                // 4. Send Email
                var emailData = new EmailModel
                {
                    To = request.Email,
                    Subject = "Your Temporary Password",
                    Body = $"<p>Your password has been reset.</p><p>Your new temporary password is: <b>{tempPassword}</b></p>"
                };

                await _emailHelper.SendEmailAsync(emailData);

                return ServiceResult<bool>.Success(true, "A temporary password has been sent to your email.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.Fail($"An error occurred: {ex.Message}");
            }
        }
    }
}
