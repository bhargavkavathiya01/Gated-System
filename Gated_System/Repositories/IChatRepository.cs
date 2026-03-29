using Gated_System.Models;

namespace Gated_System.Repositories
{
    public interface IChatRepository
    {
        Task<int> GetOrCreateChatAsync(GetOrCreateChatRequest request);
        //Task<IReadOnlyList<ChatMessageModel>> GetMessagesAsync(int chatId, int skip, int take);
        Task<IEnumerable<ChatMessageModel>> GetMessagesAsync(GetChatFeedRequest request);
        Task<ChatMessageModel> AddMessageAsync(int chatId, int userId, string message, string? mediaUrl = null, string? mediaType = null);
        Task<IEnumerable<GroupChatModel>> GetUserChatsAsync(GroupRequestChatModel request);
        Task<int> CreatePollAsync(int createdBy, ChatPollCreateModel model);
        Task<IEnumerable<ChatPollViewModel>> GetPollsForChatAsync(int chatId, int userId);
        Task ClosePollAsync(int pollId);
        Task VoteAsync(int pollId, int userId, IEnumerable<int> optionIds);
        Task<List<object>> GetChatFeedAsync(int chatId,int userId,int skip,int take);
        Task<ChatPollFeedModel?> GetPollByPollIdAsync(int pollId, int userId);
        Task<List<int>> GetChatMemberUserIdsAsync(int chatId);
        Task<List<string>> GetFcmTokensForUserIdsAsync(List<int> userIds);
        Task<IEnumerable<GroupChatModel>> GetChatGroupsByPropertyIdAsync(int propertyId);
    }
}
