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


using Gated_System.Models;
using Gated_System.Services;
using Microsoft.AspNetCore.SignalR;
using System.Text.RegularExpressions;

public class ChatHub : Hub
{
    private readonly IChatService _chatService;

    public ChatHub(IChatService chatService)
    {
        _chatService = chatService;
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

            // Send the message to all clients in the group
            await Clients.Group(GroupName(request.ChatId)).SendAsync("ReceiveMessage", new
            {
                id = saved.Id,
                chatId = saved.ChatId,
                userId = saved.UserId,
                message = saved.Message,
                createdOn = saved.CreatedOn
            });
        }
        catch (Exception ex)
        {
            // Log the error for debugging purposes
            Console.WriteLine("Error in SendMessage: " + ex.Message);
            await Clients.Caller.SendAsync("Error", "An error occurred while processing your message.");
        }
    }
}
