using Gated_System.Models;

namespace Gated_System.Services
{
    public interface IChatService
    {
        Task<int> GetOrCreateChatAsync(GetOrCreateChatRequest request);
        Task<IEnumerable<ChatMessageModel>> GetMessagesAsync(GetChatFeedRequest request);
        Task<ChatMessageModel> AddMessageAsync(int chatId, int userId, string message, string? mediaUrl = null, string? mediaType = null);
        Task<IEnumerable<GroupChatModel>> GetUserChatsAsync(GroupRequestChatModel request);
        Task<int> CreatePollAsync(int userId, ChatPollCreateModel model);
        Task<IEnumerable<ChatPollViewModel>> GetPollsForChatAsync(int chatId, int userId);
        Task ClosePollAsync(int userId, int pollId);
        Task VoteAsync(int userId, int pollId, IEnumerable<int> optionIds);
        Task<ChatFeedResponse> GetChatFeedAsync(int chatId,int userId,int skip,int take);
        Task<ChatPollFeedModel?> GetPollByPollIdAsync(int pollId, int userId);
        Task<List<int>> GetChatMemberUserIdsAsync(int chatId);
        Task<List<string>> GetFcmTokensForUserIdsAsync(List<int> userIds);
        Task<IEnumerable<GroupChatModel>> GetChatGroupsByPropertyIdAsync(int propertyId);
    }
}
