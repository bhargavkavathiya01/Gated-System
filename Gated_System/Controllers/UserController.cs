using FirebaseAdmin.Messaging;
using Gated_System.Helpers;
using Gated_System.Models;
using Gated_System.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Gated_System.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IAwsS3Service _awsS3Service;
        private readonly VoipPushHelper _voipHelper;

        public UserController(IUserService userService, IAwsS3Service awsS3Service, VoipPushHelper voipHelper)
        {
            _userService = userService;
            _awsS3Service = awsS3Service;
            _voipHelper = voipHelper;
        }

        // TEMPORARY diagnostic: reports whether the Apple VoIP certificate loaded.
        // Remove once iOS calling is confirmed working.
        [HttpGet("voip-status")]
        public IActionResult VoipStatus()
        {
            if (GetCurrentUserId() == -1)
                return Unauthorized(new { status = false, message = "Invalid or expired token" });

            return Ok(new
            {
                status = true,
                certificateLoaded = _voipHelper.IsConfigured,
                sandbox = _voipHelper.UseSandbox,
                detail = _voipHelper.Status,
                lastSendResult = _voipHelper.LastSendResult
            });
        }

        // TEMPORARY diagnostic: sends a VoIP push straight to the given user's stored
        // VoIP token and returns Apple's actual answer. Remove once calling works.
        [HttpPost("voip-test/{userId:int}")]
        public async Task<IActionResult> VoipTest(int userId)
        {
            if (GetCurrentUserId() == -1)
                return Unauthorized(new { status = false, message = "Invalid or expired token" });

            var device = await _userService.GetDeviceTokenAsync(userId);
            if (device == null)
                return NotFound(new { status = false, message = $"No device row found for user {userId}" });

            if (!device.HasVoipToken)
                return BadRequest(new
                {
                    status = false,
                    message = $"User {userId} has no VoIP token stored.",
                    platform = device.Platform
                });

            var sent = await _voipHelper.SendCallAsync(
                device.VoipToken!,
                "Visitor Approval Request",
                "Test Visitor",
                "Visitor waiting for approval",
                new Dictionary<string, string>
                {
                    { "visitorRequestId", "0" },
                    { "action", "approve_reject" },
                    { "type", "visitor_request" },
                    { "notificationId", "1" }
                });

            return Ok(new
            {
                status = true,
                accepted = sent,
                platform = device.Platform,
                isIos = device.IsIos,
                appleResponse = _voipHelper.LastSendResult
            });
        }

        private int GetCurrentUserId()
        {
            var idClaim = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(idClaim) || !int.TryParse(idClaim, out var uid))
                return -1;
            return uid;
        }

        [HttpPut("update-profile")]
        public async Task<IActionResult> UpdateProfile([FromForm] UserProfileUpdateModel dto)
        {
            int currentUserId = GetCurrentUserId();
            if (currentUserId == -1) return Unauthorized();

            // Safety: Ensure users can only update their own profile 
            // OR the ID is set from the token for security
            dto.Id = currentUserId;
            dto.ModifiedBy = currentUserId;

            
            if (dto.ProfileImage != null)
            {
                var uploadResult = await _awsS3Service.UploadFileAsync(dto.ProfileImage, "user-profiles");
                if (uploadResult.status)
                {
                    dto.ProfilePictureUrl = uploadResult.Data;
                }
            }

            var result = await _userService.UpdateProfileAsync(dto);

            if (!result.status)
            {
                // This handles 409 Conflict (Email/Phone exists) or 404
                if (result.Message.Contains("exists"))
                    return Conflict(new { status = false, message = result.Message , data = new { } });

                return BadRequest(new { status = false, message = result.Message , data = new { } });
            }

            return Ok(new
            {
                status = true,
                message = result.Message,
                data =new { }
            });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest requestModel)
        {
            int currentUserId = GetCurrentUserId();
            if (currentUserId == -1) return Unauthorized();

            requestModel.UserId = currentUserId;

            // Call service passing the ID and the Request Model
            var result = await _userService.ResetPasswordAsync(requestModel);

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

        [HttpPost("update-fcm-token")]
        public async Task<IActionResult> UpdateFcmToken([FromBody] UpdateFcmTokenModel model)
        {
            int currentUserId = GetCurrentUserId();
            if (currentUserId == -1)
                return Unauthorized(new { status = false, message = "Invalid or expired token", data = new { } });

            // Set user ID from token for security
            model.UserId = currentUserId;
            model.ModifiedBy = currentUserId;

            var result = await _userService.UpdateFcmTokenAsync(model);

            if (!result.status)
            {
                return BadRequest(new
                {
                    status = false,
                    message = result.Message,
                    data = new { }
                });
            }

            return Ok(new
            {
                status = true,
                message = result.Message,
                data = new { }
            });
        }

        [HttpGet("getallusers")]
        public async Task<IActionResult> GetAllUsers()
        {
            var userId = GetCurrentUserId();
            if (userId == -1) return Unauthorized(new { status = false, message = "Invalid or expired token" });

            var result = await _userService.GetAllUsersAsync();
            if (!result.status) return BadRequest(new { status = false, message = result.Message });
            return Ok(new { status = true, message = result.Message, data = result.Data });
        }

        [HttpPost("addSOScontact")]
        public async Task<IActionResult> AddSosContact([FromBody] AddSosContactModel dto)
        {
            var userId = GetCurrentUserId();
            if (userId == -1) return Unauthorized(new { status = false, message = "Invalid or expired token" });

            var result = await _userService.AddSosContactAsync(userId, dto);
            if (!result.status) return BadRequest(new { status = false, message = result.Message });
            return Ok(new { status = true, message = result.Message, data = new { id = result.Data } });
        }

        [HttpGet("getsoscontacts")]
        public async Task<IActionResult> GetSosContacts()
        {
            var userId = GetCurrentUserId();
            if (userId == -1) return Unauthorized(new { status = false, message = "Invalid or expired token" });

            var result = await _userService.GetSosContactsAsync(userId);
            if (!result.status) return BadRequest(new { status = false, message = result.Message });
            return Ok(new { status = true, message = result.Message, data = result.Data });
        }

        [HttpDelete("removeSOScontact/{id}")]
        public async Task<IActionResult> RemoveSosContact(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == -1) return Unauthorized(new { status = false, message = "Invalid or expired token" });

            var result = await _userService.RemoveSosContactAsync(id, userId);
            if (!result.status) return BadRequest(new { status = false, message = result.Message });
            return Ok(new { status = true, message = result.Message });
        }

        [HttpPut("updateSOScontact/{id}")]
        public async Task<IActionResult> UpdateSosRelation(int id, [FromBody] UpdateSosRelationModel dto)
        {
            var userId = GetCurrentUserId();
            if (userId == -1) return Unauthorized(new { status = false, message = "Invalid or expired token" });

            var result = await _userService.UpdateSosRelationAsync(id, userId, dto);
            if (!result.status) return BadRequest(new { status = false, message = result.Message });
            return Ok(new { status = true, message = result.Message });
        }

        [HttpPost("triggersos")]
        public async Task<IActionResult> TriggerSos()
        {
            var userId = GetCurrentUserId();
            if (userId == -1) return Unauthorized(new { status = false, message = "Invalid or expired token" });

            var result = await _userService.TriggerSosAsync(userId);
            if (!result.status) return BadRequest(new { status = false, message = result.Message });
            return Ok(new { status = true, message = result.Message });
        }

        [HttpPost("test-push-notification")]
        public async Task<IActionResult> TestPushNotification([FromBody] TestPushNotificationModel dto)
        {
            try
            {
                var message = new Message
                {
                    Token = dto.DeviceToken,
                    Notification = new Notification
                    {
                        Title = dto.Title ?? "Test Notification",
                        Body  = dto.Body  ?? "Dummy push notification from Gated System"
                    },
                    Data = new Dictionary<string, string> { ["type"] = "test" }
                };

                var result = await FirebaseMessaging.DefaultInstance.SendAsync(message);
                return Ok(new { status = true, message = "Notification sent successfully", messageId = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { status = false, message = ex.Message });
            }
        }
    }
}
