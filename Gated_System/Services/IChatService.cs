using Gated_System.Models;

namespace Gated_System.Services
{
    public interface IChatService
    {
        Task<int> GetOrCreateChatAsync(GetOrCreateChatRequest request);
        Task<IEnumerable<ChatMessageModel>> GetMessagesAsync(GetMessagesRequest request);
        Task<ChatMessageModel> AddMessageAsync(int chatId, int userId, string message);
        Task<IEnumerable<GroupChatModel>> GetUserChatsAsync(GroupRequestChatModel request);
        Task<int> CreatePollAsync(int userId, ChatPollCreateModel model);
        Task<IEnumerable<ChatPollViewModel>> GetPollsForChatAsync(int chatId, int userId);
        Task ClosePollAsync(int userId, int pollId);
        Task VoteAsync(int userId, int pollId, IEnumerable<int> optionIds);
    }
}
