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
                IsActive = true,
                RegisterTypeId = dto.RegisterTypeId
            };

            var userId = await _repo.CreateAsync(user);

            // assign default role - FlatOwner or none. For now assign 'FlatOwner' as example
            //await _repo.AssignRoleAsync(userId,7);

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
                Phone = dto.Phone,
                RegisterTypeId = dto.RegisterTypeId
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
                //var emailData = new EmailModel
                //{
                //    To = request.Email,
                //    Subject = "Your Temporary Password",
                //    Body = $"<p>Your password has been reset.</p><p>Your new temporary password is: <b>{tempPassword}</b></p>"
                //};

                var emailData = new EmailModel
                {
                    To = request.Email,
                    Subject = "Password Reset – Temporary Access",
                    Body = $@"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <meta charset='UTF-8'>
                        <title>Password Reset</title>
                    </head>
                    <body style='margin:0; padding:0; background-color:#f4f6f8; font-family:Arial, Helvetica, sans-serif;'>

                    <table width='100%' cellpadding='0' cellspacing='0' style='background-color:#f4f6f8; padding:20px;'>
                        <tr>
                            <td align='center'>
                                <table width='600' cellpadding='0' cellspacing='0' style='background:#ffffff; border-radius:8px; overflow:hidden;'>
                
                                    <!-- Header -->
                                    <tr>
                                        <td style='background:#1f2937; padding:20px; text-align:center;'>
                                            <h2 style='color:#ffffff; margin:0;'>Nandi</h2>
                                        </td>
                                    </tr>

                                    <!-- Body -->
                                    <tr>
                                        <td style='padding:30px; color:#333333;'>
                                            <p style='font-size:16px; margin:0 0 12px;'>Hello,</p>

                                            <p style='font-size:15px; line-height:1.6;'>
                                                We received a request to reset your password.  
                                                Please use the temporary password below to log in.
                                            </p>

                                            <div style='margin:25px 0; text-align:center;'>
                                                <span style='display:inline-block; padding:12px 20px;
                                                             background:#e5e7eb;
                                                             font-size:18px;
                                                             font-weight:bold;
                                                             letter-spacing:2px;
                                                             border-radius:6px;'>
                                                    {tempPassword}
                                                </span>
                                            </div>

                                            <p style='font-size:14px; line-height:1.6; color:#555555;'>
                                                For security reasons, please change your password immediately after logging in.
                                            </p>

                                            <p style='font-size:14px; color:#555555;'>
                                                If you did not request this change, please contact our support team immediately.
                                            </p>

                                            <p style='margin-top:30px; font-size:14px;'>
                                                Regards,<br>
                                                <strong>Nandi Support Team</strong>
                                            </p>
                                        </td>
                                    </tr>

                                    <!-- Footer -->
                                    <tr>
                                        <td style='background:#f9fafb; padding:15px; text-align:center; font-size:12px; color:#777777;'>
                                            © {DateTime.Now.Year} Nandi. All rights reserved.
                                        </td>
                                    </tr>

                                </table>
                            </td>
                        </tr>
                    </table>

                    </body>
                    </html>"
                };
                await _emailHelper.SendEmailAsync(emailData);

                return ServiceResult<bool>.Success(true, "A temporary password has been sent to your email.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.Fail($"An error occurred: {ex.Message}");
            }
        }

        public async Task<IEnumerable<RegisterTypeModel>> GetRegisterTypesAsync()
        {
            return await _repo.GetRegisterTypesAsync();
        }
    }
}
