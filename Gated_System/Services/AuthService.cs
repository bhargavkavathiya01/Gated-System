using Gated_System.Helpers;
using Gated_System.Models;
using Gated_System.Repositories;
using Microsoft.Extensions.Options;

namespace Gated_System.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _repo;
        private readonly PasswordHasher _hasher;
        private readonly JwtTokenGenerator _jwt;
        private readonly JwtOptions _jwtOptions;

        public AuthService(IUserRepository repo, PasswordHasher hasher, JwtTokenGenerator jwt, IOptions<JwtOptions> jwtOptions)
        {
            _repo = repo;
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

        public async Task<AuthResponseModel?> LoginAsync(LoginModel dto)
        {
            var user = await _repo.AuthenticateAsync(dto.Email,dto.Password);
            if (user == null) return null;
            //if (!_hasher.Verify(dto.Password, user.PasswordHash, user.PasswordSalt)) return null;

            var roles = await _repo.GetRolesAsync(user.Id);
            var accessToken = _jwt.GenerateAccessToken(user.Id, user.Email, roles, out var accessExpiry);
            var refreshToken = _jwt.GenerateRefreshToken();
            var refreshExpiry = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays);

            await _repo.SaveRefreshTokenAsync(user.Id, refreshToken, refreshExpiry);

            return new AuthResponseModel
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                AccessTokenExpiresAt = accessExpiry,
                Data= roles
            };
        }

        public async Task<AuthResponseModel?> RefreshAsync(string refreshToken)
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

            return new AuthResponseModel
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

            var property = new PropertyCreateModel
            {
                PropertyName = dto.PropertyName,
                Address = dto.Address,
                City = dto.City,
                Pincode = dto.Pincode,
                BuilderId = dto.BuilderId
            };

            var id = await _repo.CreatePropertyAsync(property);
            return id;
        }
    }
}
