using Gated_System.Helpers;
using Gated_System.Models;
using Gated_System.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Gated_System.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly IAwsS3Service _awsS3Service;

        public ChatController(IChatService chatService, IAwsS3Service awsS3Service)
        {
            _chatService = chatService;
            _awsS3Service = awsS3Service;
        }

        [HttpPost("createorgetchatgroup")]
        public async Task<IActionResult> GetOrCreateChat([FromBody] GetOrCreateChatRequest request)
        {

            var userId = GetCurrentUserId();
            if (userId == -1)
                return Unauthorized(ApiResponse.Fail("Invalid or expired token."));

            request.CreatedBy = userId;

            var chatId = await _chatService.GetOrCreateChatAsync(request);
            return Ok(new
            {
                status = true,
                message = "Chat Group Created Successfully",
                data = new { chatId = chatId }
            });
        }

        // GET api/chat/{chatId}/messages?skip=0&take=50
        [HttpPost("getmessages")]
        public async Task<IActionResult> GetMessages(GetChatFeedRequest request)
        {
            if (request.ChatId <= 0) return BadRequest(ApiResponse.Fail("Invalid ChatId"));

            var messages = await _chatService.GetMessagesAsync(request);
            //return Ok(new GetMessagesResponse
            //{
            //    ChatId = request.ChatId,
            //    Messages = messages
            //});
            return Ok(new
            {
                status = true,
                message = "Messages Fetched Successfully",
                data = new GetMessagesResponse
                {
                    ChatId = request.ChatId,
                    Messages = messages
                }
            });
        }

        [HttpPost("getmychats")]
        public async Task<IActionResult> GetMyChats(GroupRequestChatModel request)
        {
            var userId = GetCurrentUserId();
            if (userId == -1)
                return Unauthorized(ApiResponse.Fail("Invalid or expired token."));
            request.UserId = userId;

            var chats = await _chatService.GetUserChatsAsync(request);
            return Ok(new
            {
                status = true,
                message = "Chats Fetched Successfully",
                data = new { chats = chats }
            });
        }

        [HttpPost("uploadchatmedia")]
        public async Task<IActionResult> UploadChatMedia(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(ApiResponse.Fail("No file uploaded"));

            var userId = GetCurrentUserId();
            if (userId == -1)
                return Unauthorized(ApiResponse.Fail("Invalid or expired token."));

            var result = await _awsS3Service.UploadFileAsync(file, "chat_media");

            if (!result.status)
                return StatusCode(500, ApiResponse.Fail("Error uploading file: " + result.Message));

            var fileExtension = Path.GetExtension(file.FileName)?.ToLower() ?? "";
            var isImage = fileExtension == ".jpg" || fileExtension == ".jpeg" || fileExtension == ".png" || fileExtension == ".gif" || fileExtension == ".webp";
            var mediaType = isImage ? "image" : "file";

            return Ok(new
            {
                status = true,
                message = "Media uploaded successfully",
                data = new
                {
                    mediaUrl = result.Data,
                    mediaType = mediaType
                }
            });
        }

        [HttpPost("createpoll")]
        public async Task<IActionResult> CreatePoll([FromBody] ChatPollCreateModel model)
        {
            var userId = GetCurrentUserId();
            if (userId == -1)
                return Unauthorized(ApiResponse.Fail("Invalid or expired token."));

            if (model == null) return BadRequest(ApiResponse.Fail("Invalid payload"));

            try
            {
                var pollId = await _chatService.CreatePollAsync(userId, model);
                // return CreatedAtAction(nameof(GetPolls), new { chatId }, new { id = pollId });
                return Ok(new
                {
                    status = true,
                    message = "Poll Created Successfully",
                    data = new { pollId = pollId }
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new
                {
                    status = false,
                    message = ex.Message,
                    data = new { }
                });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        /// <summary>
        /// Get polls for a chat
        /// GET api/chatpolls/{chatId}?includeClosed=false
        /// </summary>
        [HttpGet("getallpollsbychat/{chatId:int}")]
        public async Task<IActionResult> GetPolls(int chatId)
        {
            var userId = GetCurrentUserId();
            if (userId == -1)
                return Unauthorized(ApiResponse.Fail("Invalid or expired token."));

            var polls = await _chatService.GetPollsForChatAsync(chatId, userId);
            return Ok(new
            {
                status = true,
                message = "Polls Fetched Successfully",
                data = new { polls = polls }
            });
        }

        /// <summary>
        /// Vote on a poll
        /// POST api/chatpolls/{pollId}/vote
        /// </summary>
        [HttpPost("pollvote")]
        public async Task<IActionResult> Vote([FromBody] ChatVoteRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == -1)
                return Unauthorized(ApiResponse.Fail("Invalid or expired token."));

            if (request == null || request.OptionIds == null || !request.OptionIds.Any())
                return BadRequest(ApiResponse.Fail("optionIds required"));

            try
            {
                await _chatService.VoteAsync(userId, request.PollId, request.OptionIds);
                return Ok(new
                {
                    status = true,
                    message = "Vote Recorded Successfully",
                    data = new { }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    status = false,
                    message = ex.Message,
                    data = new { }
                });
            }
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

        [HttpGet("getpollbyid/{pollId:int}")]
        public async Task<IActionResult> GetPollById(int pollId)
        {
            var userId = GetCurrentUserId();
            if (userId <= 0)
                return Unauthorized();

            var poll = await _chatService.GetPollByPollIdAsync(pollId, userId);
            if (poll == null)
                return NotFound();

            return Ok(new
            {
                status = true,
                message = "Poll fetched successfully",
                data = poll
            });
        }


        [HttpPost("getchatfeed")]
        public async Task<IActionResult> GetChatFeed([FromBody] GetChatFeedRequest request)
        {
            if (request.ChatId <= 0)
                return BadRequest(ApiResponse.Fail("Invalid ChatId"));

            var userId = GetCurrentUserId();
            if (userId == -1)
                return Unauthorized(ApiResponse.Fail("Invalid or expired token."));

            var result = await _chatService.GetChatFeedAsync(
                request.ChatId,
                userId,
                request.Skip,
                request.Take
            );

            return Ok(ApiResponse.Success("Chat feed fetched successfully", result));
        }


        /// <summary>
        /// Close (end) a poll
        /// POST api/chatpolls/{pollId}/close
        /// </summary>
        //[HttpPost("{pollId:int}/close")]
        //public async Task<IActionResult> ClosePoll(int pollId)
        //{
        //    var userId = GetCurrentUserId();
        //    if (userId == -1)
        //        return Unauthorized(ApiResponse.Fail("Invalid or expired token."));
        //    try
        //    {
        //        // TODO: check permission (creator or admin)
        //        await _chatService.ClosePollAsync(userId, pollId);
        //        return Ok(new
        //        {
        //            status = true,
        //            message = "Polls Closed Successfully",
        //            data = new { }
        //        });
        //    }
        //    catch (UnauthorizedAccessException)
        //    {
        //        return Forbid();
        //    }
        //    catch (ApplicationException ex)
        //    {
        //        return BadRequest(new
        //        {
        //            status = false,
        //            message = ex.Message,
        //            data = new { }
        //        });
        //    }
        //}

        [HttpGet("getchatgroupsbypropertyid/{propertyId:int}")]
        public async Task<IActionResult> GetChatGroupsByPropertyId(int propertyId)
        {
            var userId = GetCurrentUserId();
            if (userId == -1)
                return Unauthorized(ApiResponse.Fail("Invalid or expired token."));

            var groups = await _chatService.GetChatGroupsByPropertyIdAsync(propertyId);
            return Ok(ApiResponse.Success("Chat groups fetched successfully", groups));
        }

    }
}
