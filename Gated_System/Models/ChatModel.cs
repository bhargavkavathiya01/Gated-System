namespace Gated_System.Models
{
    public class ChatMessageModel
    {
        public int Id { get; set; }
        public int ChatId { get; set; }
        public int UserId { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
    }

    public class GetOrCreateChatRequest
    {
        public int PropertyId { get; set; }
        public int? BuildingId { get; set; }
        public int CreatedBy { get; set; }
        public string GroupName { get; set; } 
        public bool IsAnnouncement { get; set; } = false;
    }
    public class GetChatFeedRequest
    {
        public int ChatId { get; set; }
        public int Skip { get; set; } = 0;
        public int Take { get; set; } = 50;
    }

    public class ChatFeedResponse
    {
        public int ChatId { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; }
        public List<object> Items { get; set; } = new();
    }

    public class ClosePollRealtimeRequest
    {
        public int ChatId { get; set; }
        public int PollId { get; set; }
        public int UserId { get; set; }
    }


    public class GetMessagesResponse
    {
        public int ChatId { get; set; }
        public IEnumerable<ChatMessageModel> Messages { get; set; } = Enumerable.Empty<ChatMessageModel>();
    }

    public class SendMessagesRequest
    {
        public int UserId { get; set; }
        public int ChatId { get; set; }
        public string Message { get; set; }
    }

    public class GroupChatModel
    {
        public int Id { get; set; }
        public int PropertyId { get; set; }
        public int? BuildingId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public int CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public bool IsAnnouncement { get; set; } = false;
    }

    public class GroupRequestChatModel
    {
        public int UserId { get; set; }
        public int PropertyId { get; set; }
    }

    public class ChatPollCreateModel
    {
        public int ChatId { get; set; }
        public string Question { get; set; } = string.Empty;
        public bool AllowsMultiple { get; set; } = false;
        public DateTime? ExpiresAt { get; set; } // UTC
        public IEnumerable<string> Options { get; set; } = Array.Empty<string>();
    }

    public class CreatePollRealtimeRequest
    {
        public int ChatId { get; set; }
        public int UserId { get; set; }
        public string Question { get; set; } = "";
        public bool AllowsMultiple { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public List<string> Options { get; set; } = new();
    }


    public class ChatPollOptionCreateModel
    {
        public string OptionText { get; set; } = string.Empty;
    }

    public class ChatPollOptionViewModel
    {
        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public int Sequence { get; set; }
        public int Votes { get; set; }
    }

    public class ChatPollFeedModel
    {
        public int PollId { get; set; }
        public int ChatId { get; set; }
        public string Question { get; set; } = "";
        public bool AllowsMultiple { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedOn { get; set; }

        public PollUserModel CreatedBy { get; set; } = new();
        public List<int> CurrentUserVotes { get; set; } = new();
        public List<PollOptionModel> Options { get; set; } = new();
        public int TotalVotes { get; set; }
    }

    public class PollUserModel
    {
        public int UserId { get; set; }
        public string Name { get; set; } = "";
    }

    public class PollOptionModel
    {
        public int OptionId { get; set; }
        public string Text { get; set; } = "";
        public int VoteCount { get; set; }
        public bool VotedByMe { get; set; }
        public List<PollUserModel> Voters { get; set; } = new();
    }


    public class ChatPollViewModel
    {
        public int Id { get; set; }
        public int ChatId { get; set; }
        public string Question { get; set; } = string.Empty;
        public bool AllowsMultiple { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsActive { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public List<ChatPollOptionViewModel> Options { get; set; } = new();
        public List<int> UserVotedOptionIds { get; set; } = new();
    }

    public class ChatVoteRequest
    {
        public int ChatId { get; set; }
        public int PollId { get; set; }
        public IEnumerable<int> OptionIds { get; set; } = Array.Empty<int>();
    }
}
