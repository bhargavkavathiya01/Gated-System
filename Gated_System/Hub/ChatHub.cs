//using Gated_System.Models;
//using Gated_System.Services;
//using Microsoft.AspNetCore.SignalR;

//namespace Gated_System.Hub
//{
//    public class ChatHub : Microsoft.AspNetCore.SignalR.Hub
//    {
//        private readonly IChatService _chatService;

//        public ChatHub(IChatService chatService)
//        {
//            _chatService = chatService;
//        }

//        private static string GroupName(int chatId) => $"chat-{chatId}";

//        public async Task JoinChat(int chatId)
//        {
//            // Optionally: validate user can access this chat
//            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(chatId));
//        }

//        public async Task LeaveChat(int chatId)
//        {
//            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(chatId));
//        }

//        public async Task SendMessage(SendMessagesRequest request)
//        {
//            if (string.IsNullOrWhiteSpace(request.Message))
//                return;

//            var userIdClaim = Context.User?.FindFirst("userId")?.Value;

//            if (string.IsNullOrEmpty(userIdClaim))
//            {
//                await Clients.Caller.SendAsync("Error", "UserId missing.");
//                return;
//            }

//            int userId = int.Parse(userIdClaim);

//            var saved = await _chatService.AddMessageAsync(request.ChatId, userId, request.Message);

//            await Clients.Group(GroupName(request.ChatId)).SendAsync("ReceiveMessage", new
//            {
//                id = saved.Id,
//                chatId = saved.ChatId,
//                userId = saved.UserId,
//                message = saved.Message,
//                createdOn = saved.CreatedOn
//            });
//        }
//    }
//}


using Gated_System.Helpers;
using Gated_System.Models;
using Gated_System.Services;
using Microsoft.AspNetCore.SignalR;
using System.Text.RegularExpressions;

public class ChatHub : Hub
{
    private readonly IChatService _chatService;
    private readonly PushNotificationHelper _pushNotificationHelper;

    public ChatHub(IChatService chatService, PushNotificationHelper pushNotificationHelper)
    {
        _chatService = chatService;
        _pushNotificationHelper = pushNotificationHelper;
    }

    private static string GroupName(int chatId) => $"chat-{chatId}";

    public async Task JoinChat(int chatId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(chatId));
    }

    public async Task LeaveChat(int chatId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(chatId));
    }

    public async Task SendMessage(SendMessagesRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return;

            int userId = request.UserId;

            if (userId <= 0)
            {
                await Clients.Caller.SendAsync("Error", "Invalid UserId.");
                return;
            }

            var saved = await _chatService.AddMessageAsync(request.ChatId, userId, request.Message);

            // Send response in same format as API
            await Clients.Group(GroupName(request.ChatId)).SendAsync("ReceiveMessage", new
            {
                status = true,
                message = "Message sent successfully",
                data = saved
            });

            // Send push notifications to chat members (excluding sender)
            _ = Task.Run(async () =>
            {
                try
                {
                    var memberUserIds = await _chatService.GetChatMemberUserIdsAsync(request.ChatId);
                    // Exclude the sender
                    var recipientUserIds = memberUserIds.Where(id => id != userId).ToList();
                    
                    if (recipientUserIds.Any())
                    {
                        var fcmTokens = await _chatService.GetFcmTokensForUserIdsAsync(recipientUserIds);
                        
                        if (fcmTokens.Any())
                        {
                            // Truncate message for notification body
                            var messagePreview = request.Message.Length > 100 
                                ? request.Message.Substring(0, 100) + "..." 
                                : request.Message;

                            var data = new Dictionary<string, string>
                            {
                                { "type", "message" },
                                { "chatId", request.ChatId.ToString() },
                                { "messageId", saved.Id.ToString() },
                                { "userId", userId.ToString() }
                            };

                            await _pushNotificationHelper.SendToDevicesAsync(
                                fcmTokens,
                                "New Message",
                                messagePreview,
                                data
                            );
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log error but don't fail the message send
                    Console.WriteLine($"Error sending push notification for message: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            // Log the error for debugging purposes
            Console.WriteLine("Error in SendMessage: " + ex.Message);
            await Clients.Caller.SendAsync("Error", "An error occurred while processing your message.");
        }
    }

    public async Task SendMediaMessage(SendMediaMessageRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.MediaUrl))
            {
                await Clients.Caller.SendAsync("Error", "MediaUrl cannot be empty.");
                return;
            }

            int userId = request.UserId;

            if (userId <= 0)
            {
                await Clients.Caller.SendAsync("Error", "Invalid UserId.");
                return;
            }

            var saved = await _chatService.AddMessageAsync(request.ChatId, userId, request.Message, request.MediaUrl, request.MediaType);

            await Clients.Group(GroupName(request.ChatId)).SendAsync("ReceiveMessage", new
            {
                status = true,
                message = "Media message sent successfully",
                data = saved
            });

            // Send push notifications to chat members (excluding sender)
            _ = Task.Run(async () =>
            {
                try
                {
                    var memberUserIds = await _chatService.GetChatMemberUserIdsAsync(request.ChatId);
                    var recipientUserIds = memberUserIds.Where(id => id != userId).ToList();
                    
                    if (recipientUserIds.Any())
                    {
                        var fcmTokens = await _chatService.GetFcmTokensForUserIdsAsync(recipientUserIds);
                        
                        if (fcmTokens.Any())
                        {
                            var notificationBody = string.IsNullOrWhiteSpace(request.Message) 
                                ? (request.MediaType == "image" ? "📷 Image" : "📄 File")
                                : (request.Message.Length > 100 ? request.Message.Substring(0, 100) + "..." : request.Message);

                            var data = new Dictionary<string, string>
                            {
                                { "type", "media_message" },
                                { "chatId", request.ChatId.ToString() },
                                { "messageId", saved.Id.ToString() },
                                { "userId", userId.ToString() }
                            };

                            await _pushNotificationHelper.SendToDevicesAsync(
                                fcmTokens,
                                "New Media Message",
                                notificationBody,
                                data
                            );
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending push notification for media message: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error in SendMediaMessage: " + ex.Message);
            await Clients.Caller.SendAsync("Error", "An error occurred while processing your media message.");
        }
    }

    private int GetCurrentUserId()
    {
        var idClaim = Context?.User?.FindFirst("userId")?.Value;

        if (string.IsNullOrEmpty(idClaim))
            return -1;

        if (!int.TryParse(idClaim, out var uid))
            return -1;

        return uid;
    }

    public async Task SendPoll(ChatPollCreateModel request)
    {
        var userId = GetCurrentUserId();
        if (userId == -1)
        {
            await Clients.Caller.SendAsync("Error", "Invalid or expired token.");
            return;
        }

        if (request.ChatId <= 0)
        {
            await Clients.Caller.SendAsync("Error", "Invalid data.");
            return;
        }

        var pollId = await _chatService.CreatePollAsync(userId, request);

        //await Clients.Group(GroupName(request.ChatId))
        //   .SendAsync("PollCreated", new
        //   {
        //       type = "poll",
        //       pollId = pollId,
        //       chatId = request.ChatId,
        //       question = request.Question,
        //       allowsMultiple = request.AllowsMultiple,
        //       expiresAt = request.ExpiresAt,
        //       createdBy = userId,
        //       createdOn = DateTime.Now,
        //       options = request.Options
        //   });
        // Fetch the complete poll data (same as API response)
        var pollData = await _chatService.GetPollByPollIdAsync(pollId, userId);

        if (pollData != null)
        {
            // Send response in same format as API
            await Clients.Group(GroupName(request.ChatId))
                .SendAsync("PollCreated", new
                {
                    status = true,
                    message = "Poll Created Successfully",
                    data = pollData
                });
        }
        else
        {
            // Fallback if poll fetch fails
            await Clients.Caller.SendAsync("Error", "Poll created but failed to fetch poll data.");
        }

        // Send push notifications to chat members (excluding creator)
        _ = Task.Run(async () =>
        {
            try
            {
                var memberUserIds = await _chatService.GetChatMemberUserIdsAsync(request.ChatId);
                // Exclude the poll creator
                var recipientUserIds = memberUserIds.Where(id => id != userId).ToList();
                
                if (recipientUserIds.Any())
                {
                    var fcmTokens = await _chatService.GetFcmTokensForUserIdsAsync(recipientUserIds);
                    
                    if (fcmTokens.Any())
                    {
                        // Truncate question for notification body
                        var questionPreview = request.Question.Length > 100 
                            ? request.Question.Substring(0, 100) + "..." 
                            : request.Question;

                        var data = new Dictionary<string, string>
                        {
                            { "type", "poll" },
                            { "chatId", request.ChatId.ToString() },
                            { "pollId", pollId.ToString() },
                            { "createdBy", userId.ToString() }
                        };

                        await _pushNotificationHelper.SendToDevicesAsync(
                            fcmTokens,
                            "New Poll",
                            questionPreview,
                            data
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't fail the poll creation
                Console.WriteLine($"Error sending push notification for poll: {ex.Message}");
            }
        });
    }

    // =======================
    // POLL : VOTE
    // =======================

    public async Task VotePoll(ChatVoteRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == -1)
        {
            await Clients.Caller.SendAsync("Error", "Invalid or expired token.");
            return;
        }

        if (request.PollId <= 0)
        {
            await Clients.Caller.SendAsync("Error", "Invalid vote data.");
            return;
        }

        await _chatService.VoteAsync(
            userId,
            request.PollId,
            request.OptionIds
        );

        // Fetch updated poll result (same as API response)
        var pollResult = await _chatService.GetPollByPollIdAsync(
            request.PollId,
            userId
        );

        //await Clients.Group(GroupName(request.ChatId))
        //    .SendAsync("PollVoted", new
        //    {
        //        type = "pollVote",
        //        pollId = request.PollId,
        //        updatedPoll = pollResult
        //    });

        if (pollResult != null)
        {
            // Send response in same format as API
            await Clients.Group(GroupName(request.ChatId))
                .SendAsync("PollVoted", new
                {
                    status = true,
                    message = "Vote Recorded Successfully",
                    data = pollResult
                });
        }
        else
        {
            await Clients.Caller.SendAsync("Error", "Vote recorded but failed to fetch updated poll data.");
        }
    }

    // =======================
    // POLL : CLOSE
    // =======================

    public async Task ClosePoll(ClosePollRealtimeRequest request)
    {
        await _chatService.ClosePollAsync(request.UserId, request.PollId);

        await Clients.Group(GroupName(request.ChatId))
            .SendAsync("PollClosed", new
            {
                type = "pollClosed",
                pollId = request.PollId,
                closedBy = request.UserId,
                closedAt = DateTime.UtcNow
            });
    }
}
