using Gated_System.Helpers;
using Gated_System.Models;
using Gated_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Gated_System.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _auth;
        private readonly IAwsS3Service _aws;

        public AuthController(IAuthService auth, IAwsS3Service aws)
        {
            _auth = auth;
            _aws = aws;
        }

        private int GetCurrentUserId()
        {
            var idClaim = User.FindFirst("userId")?.Value;

            if (string.IsNullOrEmpty(idClaim))
                return -1;

            if (!int.TryParse(idClaim, out var uid))
                return -1;

            return uid;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromForm] RegisterModel dto)
        {
            try
            {
                if (dto.AadharCard != null)
                {
                    var res = await _aws.UploadFileAsync(dto.AadharCard, "AadharCards");
                    if (res.status) dto.AadharCardUrl = res.Data;
                }
                if (dto.ElectricityBill != null)
                {
                    var res = await _aws.UploadFileAsync(dto.ElectricityBill, "ElectricityBills");
                    if (res.status) dto.ElectricityBillUrl = res.Data;
                }
                if (dto.AppointmentLetter != null)
                {
                    var res = await _aws.UploadFileAsync(dto.AppointmentLetter, "AppointmentLetters");
                    if (res.status) dto.AppointmentLetterUrl = res.Data;
                }

                var res2 = await _auth.RegisterAsync(dto);
                return Ok(new
                {
                    status = true,
                    message = "User registered Successfully",
                    data = res2
                });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
            catch (Exception)
            {
                return StatusCode(500, ApiResponse.Fail("An error occurred during registration."));
            }
        }



        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel dto)
        {
            try
            {
                var res = await _auth.LoginAsync(dto);
                if (!res.status) return Unauthorized(ApiResponse.Fail(res.Message));
                return Ok(res);
            }
            catch (ApplicationException ex)
            {
                // This will catch "Email already exists" or "Phone Number already exists"
                return Conflict(new { status = false, message = ex.Message });
            }
        }

        [HttpGet("getuserbytoken")]
        public async Task<IActionResult> GetUserDataByToken()
        {
            var userId = GetCurrentUserId();
            if (userId == -1)
                return Unauthorized(ApiResponse.Fail("Invalid or expired token."));

            var res = await _auth.GetUserByToken(userId);
            if (res == null) return Unauthorized(ApiResponse.Fail("Invalid or expired token."));
            return Ok(ApiResponse.Success("User Fetched successful", res));
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenModel dto)
        {
            var res = await _auth.RefreshAsync(dto.RefreshToken);
            if (res == null)
                return Unauthorized(ApiResponse.Fail("Invalid refresh token"));
            return Ok(ApiResponse.Success("Token refreshed successfully", res));
        }

        //[Authorize]
        [HttpPost("registerproperty")]
        public async Task<IActionResult> Create([FromForm] PropertyCreateModel dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == -1)
                    return Unauthorized(ApiResponse.Fail("Invalid or expired token."));
                dto.BuilderId = userId;

                if (dto.RegistrationCertificate != null)
                {
                    var res = await _aws.UploadFileAsync(dto.RegistrationCertificate, "RegistrationCertificates");
                    if (res.status) dto.RegistrationCertificateUrl = res.Data;
                }
                if (dto.PanCard != null)
                {
                    var res = await _aws.UploadFileAsync(dto.PanCard, "PanCards");
                    if (res.status) dto.PanCardUrl = res.Data;
                }
                if (dto.TanCard != null)
                {
                    var res = await _aws.UploadFileAsync(dto.TanCard, "TanCards");
                    if (res.status) dto.TanCardUrl = res.Data;
                }

                var id = await _auth.CreatePropertyAsync(dto);
                var returnData = new
                {
                    id,
                    propertyname = dto.PropertyName,
                    address = dto.Address,
                    city = dto.City,
                    pincode = dto.Pincode,
                    builderid = dto.BuilderId,
                    buildingCount = dto.Buildings?.Count ?? 0,
                    buildings = dto.Buildings,
                    registrationCertificate = dto.RegistrationCertificateUrl,
                    panCard = dto.PanCardUrl,
                    tanCard = dto.TanCardUrl
                };
                return Ok(ApiResponse.Success("Property created successfully", returnData));
            }
            catch (ApplicationException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                // log ex if you have logger
                return StatusCode(500, ApiResponse.Fail("Internal server error", new { detail = ex.Message }));
            }
        }

        [HttpPost("forgotpassword")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            if (string.IsNullOrEmpty(request.Email))
                return BadRequest(new { status = false, message = "Email is required" });

            var result = await _auth.ForgotPasswordAsync(request);

            if (!result.status)
            {
                return BadRequest(new
                {
                    status = false,
                    message = result.Message
                });
            }

            return Ok(new
            {
                status = true,
                message = result.Message
            });
        }

        private bool IsAdminOrBuilder()
        {
            return User.Claims.Any(c =>
                (c.Type == System.Security.Claims.ClaimTypes.Role || c.Type == "role") &&
                (c.Value == "Admin" || c.Value == "Builder"));
        }

        private static IActionResult DeleteAccountErrorResult(string message)
        {
            if (message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                return new NotFoundObjectResult(ApiResponse.Fail(message));
            if (message.Contains("already deleted", StringComparison.OrdinalIgnoreCase))
                return new ConflictObjectResult(ApiResponse.Fail(message));
            return new BadRequestObjectResult(ApiResponse.Fail(message));
        }

        // Self-service: no token required. Deletes the account whose email AND phone
        // both match the values supplied.
        [HttpDelete("delete-account")]
        public async Task<IActionResult> DeleteOwnAccount([FromBody] DeleteAccountRequestModel dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Phone))
                return BadRequest(ApiResponse.Fail("Email and phone are required."));

            var result = await _auth.DeleteOwnAccountAsync(dto.Email, dto.Phone);
            if (!result.status)
                return DeleteAccountErrorResult(result.Message);

            return Ok(ApiResponse.Success(result.Message));
        }

        // Admin/Builder: deactivate any account by email or phone.
        [HttpDelete("admin/delete-account")]
        public async Task<IActionResult> DeleteAccountByAdmin([FromBody] EmailOrPhoneModel dto)
        {
            var callerId = GetCurrentUserId();
            if (callerId == -1)
                return Unauthorized(ApiResponse.Fail("Invalid or expired token."));

            if (!IsAdminOrBuilder())
                return Forbid();

            if (string.IsNullOrWhiteSpace(dto?.user))
                return BadRequest(ApiResponse.Fail("Email or phone is required."));

            var result = await _auth.DeleteAccountAsync(null, dto.user, callerId);
            if (!result.status)
                return DeleteAccountErrorResult(result.Message);

            return Ok(ApiResponse.Success(result.Message));
        }

        [HttpGet("getregisteredusertypes")]
        public async Task<IActionResult> GetRegisterTypes()
        {
            try
            {
                var registerTypes = await _auth.GetRegisterTypesAsync();
                return Ok(new
                {
                    status = true,
                    message = "Register types fetched successfully",
                    data = registerTypes
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { status = false, message = "An error occurred", details = ex.Message });
            }
        }
    }
}
