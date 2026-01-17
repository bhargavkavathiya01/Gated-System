using Gated_System.Models;
using Gated_System.Repositories;

namespace Gated_System.Services
{
    public class ChatService : IChatService
    {
        private readonly IChatRepository _repo;

        public ChatService(IChatRepository repo)
        {
            _repo = repo;
        }

        public async Task<int> GetOrCreateChatAsync(GetOrCreateChatRequest request)
        {
            // here you could add extra business validation (role checks, property mapping etc.)
            return await _repo.GetOrCreateChatAsync(request);
        }

        public async Task<IEnumerable<ChatMessageModel>> GetMessagesAsync(GetChatFeedRequest request)
        {
            if (request.Take <= 0) request.Take = 50;
            if (request.Skip < 0) request.Skip = 0;

            return await _repo.GetMessagesAsync(request);
        }

        public async Task<ChatMessageModel> AddMessageAsync(int chatId, int userId, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Message cannot be empty", nameof(message));

            // could check if user is part of this chat group using another SP
            return await _repo.AddMessageAsync(chatId, userId, message.Trim());
        }

        public async Task<IEnumerable<GroupChatModel>> GetUserChatsAsync(GroupRequestChatModel request)
        {
            // add extra business logic if needed later (like role checks)
            return await _repo.GetUserChatsAsync(request);
        }

        public async Task<int> CreatePollAsync(int userId, ChatPollCreateModel model)
        {
            // business checks:
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (string.IsNullOrWhiteSpace(model.Question)) throw new ArgumentException("Question required");
            if (model.Options == null) throw new ArgumentException("Options required");
            if (model.Options is { } opts && System.Linq.Enumerable.Count(opts) < 2)
                throw new ArgumentException("At least two options required");

            // TODO: check user is member of the chat / has permission to create poll (owner/secretary/committee)
            // e.g. if (!await _userRoleRepo.UserHasRoleInChat(userId, model.ChatId, allowedRoles)) throw new UnauthorizedAccessException();

            return await _repo.CreatePollAsync(userId, model);
        }

        public async Task<IEnumerable<ChatPollViewModel>> GetPollsForChatAsync(int chatId, int userId)
        {
            if (chatId <= 0) throw new ArgumentException("Invalid chatId");
            return await _repo.GetPollsForChatAsync(chatId, userId);
        }
        public async Task<ChatPollFeedModel?> GetPollByPollIdAsync(int pollId, int userId)
        {
            if (pollId <= 0)
                throw new ArgumentException("Invalid pollId");

            return await _repo.GetPollByPollIdAsync(pollId, userId);
        }


        public async Task ClosePollAsync(int userId, int pollId)
        {
            // TODO: check that userId is allowed to close this poll (e.g. creator or admin)
            await _repo.ClosePollAsync(pollId);
        }

        public async Task VoteAsync(int userId, int pollId, IEnumerable<int> optionIds)
        {
            if (optionIds == null) throw new ArgumentException("optionIds required");
            // optional: check that poll is active/exists before calling repo (repo does that)
            await _repo.VoteAsync(pollId, userId, optionIds);
        }

        public async Task<ChatFeedResponse> GetChatFeedAsync(int chatId,int userId,int skip,int take)
        {
            var feed = await _repo.GetChatFeedAsync(chatId, userId, skip, take);

            return new ChatFeedResponse
            {
                ChatId = chatId,
                Skip = skip,
                Take = take,
                Items = feed
            };
        }

        public async Task<List<int>> GetChatMemberUserIdsAsync(int chatId)
        {
            if (chatId <= 0) throw new ArgumentException("Invalid chatId");
            return await _repo.GetChatMemberUserIdsAsync(chatId);
        }

        public async Task<List<string>> GetFcmTokensForUserIdsAsync(List<int> userIds)
        {
            if (userIds == null || userIds.Count == 0)
                return new List<string>();
            return await _repo.GetFcmTokensForUserIdsAsync(userIds);
        }
    }
}
