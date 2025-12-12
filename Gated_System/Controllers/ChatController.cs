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

        public ChatController(IChatService chatService)
        {
            _chatService = chatService;
        }

        [HttpPost("createorgetchatgroup")]
        public async Task<IActionResult> GetOrCreateChat([FromBody] GetOrCreateChatRequest request)
        {
            // If CreatedBy not provided, set from JWT
            if (request.CreatedBy <= 0)
            {
                var userIdClaim = User.FindFirst("userId")?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                    return Unauthorized("UserId claim missing");

                request.CreatedBy = int.Parse(userIdClaim);
            }

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
        public async Task<IActionResult> GetMessages(GetMessagesRequest request)
        {
            if (request.ChatId <= 0) return BadRequest("Invalid chatId");

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

        [HttpPost("getChats")]
        public async Task<IActionResult> GetMyChats(GroupRequestChatModel request)
        {
            var userId = request.UserId;
            if (userId <= 0)
                return Unauthorized("UserId missing");

            var chats = await _chatService.GetUserChatsAsync(request);
            return Ok(new
            {
                status = true,
                message = "Chats Fetched Successfully",
                data = new { chats = chats }
            });
        }

        [HttpPost("createPoll/{chatId:int}")]
        public async Task<IActionResult> CreatePoll(int chatId, [FromBody] ChatPollCreateModel model)
        {
            var userId = GetCurrentUserId();
            if (model == null) return BadRequest("Invalid payload");
            model.ChatId = chatId;

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
        [HttpGet("getPolls/{chatId:int}")]
        public async Task<IActionResult> GetPolls(int chatId)
        {
            var userId = GetCurrentUserId();
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
        [HttpPost("{pollId:int}/vote")]
        public async Task<IActionResult> Vote(int pollId, [FromBody] ChatVoteRequest request)
        {
            var userId = GetCurrentUserId();
            if (request == null || request.OptionIds == null || !request.OptionIds.Any())
                return BadRequest("optionIds required");

            try
            {
                await _chatService.VoteAsync(userId, pollId, request.OptionIds);
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
            if (string.IsNullOrEmpty(idClaim) || !int.TryParse(idClaim, out var uid)) throw new UnauthorizedAccessException("Invalid userId claim");
            return uid;
        }

        /// <summary>
        /// Close (end) a poll
        /// POST api/chatpolls/{pollId}/close
        /// </summary>
        [HttpPost("{pollId:int}/close")]
        public async Task<IActionResult> ClosePoll(int pollId)
        {
            var userId = GetCurrentUserId();
            try
            {
                // TODO: check permission (creator or admin)
                await _chatService.ClosePollAsync(userId, pollId);
                return Ok(new
                {
                    status = true,
                    message = "Polls Closed Successfully",
                    data = new { }
                });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new
                {
                    status = false,
                    message = ex.Message,
                    data = new { }
                });
            }
        }
    }
}
