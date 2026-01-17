using Gated_System.Models;
using Npgsql;
using System.Text.Json;

namespace Gated_System.Repositories
{
    public class ChatRepository : IChatRepository
    {
        private readonly NpgsqlConnection _connection;

        public ChatRepository(NpgsqlConnection connection)
        {
            _connection = connection;
        }

        /// <summary>
        /// Get existing chat for property/building or create if not exists.
        /// Uses sp_api_groupchatmaster with operation = 6.
        /// </summary>
        public async Task<int> GetOrCreateChatAsync(GetOrCreateChatRequest request)
        {
            const string query = @"SELECT public.sp_api_groupchatmaster(@p_operation, @p_json)::text;";

            var payload = new
            {
                propertyid = request.PropertyId,
                buildingid = request.BuildingId,
                groupname = request.GroupName,
                createdby = request.CreatedBy
            };

            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(query, _connection);
                cmd.Parameters.AddWithValue("p_operation", 6); // get-or-create
                cmd.Parameters.AddWithValue("p_json", (object)jsonPayload ?? DBNull.Value);

                var scalarResult = await cmd.ExecuteScalarAsync();

                if (scalarResult is null || scalarResult is DBNull)
                    throw new Exception("sp_api_groupchatmaster returned null/empty result.");

                var resultJson = scalarResult.ToString()!;
                using var doc = JsonDocument.Parse(resultJson);
                var root = doc.RootElement;

                if (!root.TryGetProperty("status_code", out var statusEl))
                    throw new Exception("sp_api_groupchatmaster: missing status_code");

                var statusCode = statusEl.GetInt32();
                if (statusCode != 200)
                {
                    var msg = root.TryGetProperty("message", out var msgEl)
                        ? msgEl.GetString()
                        : "Unknown error from sp_api_groupchatmaster (op=6)";
                    throw new ApplicationException($"GetOrCreateChat failed. Status: {statusCode}, Message: {msg}");
                }

                if (!root.TryGetProperty("data", out var dataEl))
                    throw new Exception("sp_api_groupchatmaster: missing data");

                if (!dataEl.TryGetProperty("id", out var idEl))
                    throw new Exception("sp_api_groupchatmaster: missing data.id");

                var chatId = idEl.GetInt32();
                return chatId;
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        /// <summary>
        /// Get messages for a specific chat with paging.
        /// </summary>
        public async Task<IEnumerable<ChatMessageModel>> GetMessagesAsync(GetChatFeedRequest request)
        {
            const string query = @"SELECT public.sp_api_groupchatmessages(@p_operation, @p_json)::text;";

            if (request.Skip < 0) request.Skip = 0;
            if (request.Take <= 0) request.Take = 50;

            var payload = new
            {
                chatid = request.ChatId,
                skip=request.Skip,
                take=request.Take
            };

            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(query, _connection);
                cmd.Parameters.AddWithValue("p_operation", 1); // get list
                cmd.Parameters.AddWithValue("p_json", (object)jsonPayload ?? DBNull.Value);

                var scalarResult = await cmd.ExecuteScalarAsync();

                if (scalarResult is null || scalarResult is DBNull)
                    return Enumerable.Empty<ChatMessageModel>();

                var resultJson = scalarResult.ToString()!;
                using var doc = JsonDocument.Parse(resultJson);
                var root = doc.RootElement;

                if (!root.TryGetProperty("status_code", out var statusEl))
                    throw new Exception("missing status_code");

                var statusCode = statusEl.GetInt32();
                if (statusCode != 200)
                {
                    var msg = root.TryGetProperty("message", out var msgEl)
                        ? msgEl.GetString()
                        : "Unknown error from group chat messages (op=1)";
                    throw new ApplicationException($"GetMessages failed. Status: {statusCode}, Message: {msg}");
                }

                if (!root.TryGetProperty("data", out var dataEl) || dataEl.ValueKind == JsonValueKind.Null)
                    return Enumerable.Empty<ChatMessageModel>();

                var list = new List<ChatMessageModel>();

                if (dataEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in dataEl.EnumerateArray())
                    {
                        list.Add(ParseChatMessageElement(el));
                    }
                }
                else if (dataEl.ValueKind == JsonValueKind.Object)
                {
                    list.Add(ParseChatMessageElement(dataEl));
                }

                return list;
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        /// <summary>
        /// Add a new message to a chat.
        /// </summary>
        public async Task<ChatMessageModel> AddMessageAsync(int chatId, int userId, string message)
        {
            const string query = @"SELECT public.sp_api_groupchatmessages(@p_operation, @p_json)::text;";

            var payload = new
            {
                chatid = chatId,
                userid = userId,
                message = message
            };

            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(query, _connection);
                cmd.Parameters.AddWithValue("p_operation", 2); // insert
                cmd.Parameters.AddWithValue("p_json", (object)jsonPayload ?? DBNull.Value);

                var scalarResult = await cmd.ExecuteScalarAsync();

                if (scalarResult is null || scalarResult is DBNull)
                    throw new Exception("returned null/empty result for insert.");

                var resultJson = scalarResult.ToString()!;
                using var doc = JsonDocument.Parse(resultJson);
                var root = doc.RootElement;

                if (!root.TryGetProperty("status_code", out var statusEl))
                    throw new Exception("missing status_code");

                var statusCode = statusEl.GetInt32();
                if (statusCode != 201)
                {
                    var msg = root.TryGetProperty("message", out var msgEl)
                        ? msgEl.GetString()
                        : "Unknown error from group chat messages (op=2)";
                    throw new ApplicationException($"AddMessage failed. Status: {statusCode}, Message: {msg}");
                }

                if (!root.TryGetProperty("data", out var dataEl))
                    throw new Exception("missing data for insert");

                // Compose ChatMessageModel from returned data
                var model = new ChatMessageModel
                {
                    Id = dataEl.GetProperty("id").GetInt32(),
                    ChatId = dataEl.GetProperty("chatid").GetInt32(),
                    UserId = dataEl.GetProperty("userid").GetInt32(),
                    Message = dataEl.GetProperty("message").GetString() ?? string.Empty,
                    CreatedOn = DateTime.UtcNow // DB has now() but not returned; this is approximate
                };

                return model;
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        // ---------- private helper ----------

        private static ChatMessageModel ParseChatMessageElement(JsonElement el)
        {
            var id = el.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.Number
                ? idEl.GetInt32()
                : 0;

            var chatId = el.TryGetProperty("chatid", out var cEl) && cEl.ValueKind == JsonValueKind.Number
                ? cEl.GetInt32()
                : 0;

            var userId = el.TryGetProperty("userid", out var uEl) && uEl.ValueKind == JsonValueKind.Number
                ? uEl.GetInt32()
                : 0;

            var message = el.TryGetProperty("message", out var mEl) && mEl.ValueKind == JsonValueKind.String
                ? mEl.GetString() ?? string.Empty
                : string.Empty;

            DateTime createdOn;
            if (el.TryGetProperty("createdon", out var createdEl) && createdEl.ValueKind == JsonValueKind.String)
            {
                // createdon in JSON is typically text from timestamptz; parse safely
                DateTime.TryParse(createdEl.GetString(), out createdOn);
            }
            else
            {
                createdOn = DateTime.UtcNow;
            }

            return new ChatMessageModel
            {
                Id = id,
                ChatId = chatId,
                UserId = userId,
                Message = message,
                CreatedOn = createdOn
            };
        }

        public async Task<IEnumerable<GroupChatModel>> GetUserChatsAsync(GroupRequestChatModel request)
        {
            const string query = @"SELECT public.sp_api_groupchat(@p_operation, @p_json)::text;";

            var payload = new
            {
                userid = request.UserId,
                propertyid = request.PropertyId
            };

            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(query, _connection);
                cmd.Parameters.AddWithValue("p_operation", 1); // fetch chats
                cmd.Parameters.AddWithValue("p_json", (object)jsonPayload ?? DBNull.Value);

                var scalarResult = await cmd.ExecuteScalarAsync();

                if (scalarResult is null || scalarResult is DBNull)
                    return Enumerable.Empty<GroupChatModel>();

                var resultJson = scalarResult.ToString()!;
                using var doc = JsonDocument.Parse(resultJson);
                var root = doc.RootElement;

                if (!root.TryGetProperty("status_code", out var statusEl))
                    throw new Exception("sp_api_groupchat: missing status_code");

                var statusCode = statusEl.GetInt32();
                if (statusCode != 200)
                {
                    var msg = root.TryGetProperty("message", out var msgEl)
                        ? msgEl.GetString()
                        : "Unknown error from sp_api_groupchat (op=1)";
                    throw new ApplicationException($"GetUserChats failed. Status: {statusCode}, Message: {msg}");
                }

                if (!root.TryGetProperty("data", out var dataEl) || dataEl.ValueKind == JsonValueKind.Null)
                    return Enumerable.Empty<GroupChatModel>();

                var list = new List<GroupChatModel>();

                if (dataEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in dataEl.EnumerateArray())
                    {
                        list.Add(ParseGroupChatElement(el));
                    }
                }
                else if (dataEl.ValueKind == JsonValueKind.Object)
                {
                    list.Add(ParseGroupChatElement(dataEl));
                }

                return list;
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        private static GroupChatModel ParseGroupChatElement(JsonElement el)
        {
            var id = el.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.Number
                ? idEl.GetInt32()
                : 0;

            var propertyId = el.TryGetProperty("propertyid", out var pEl) && pEl.ValueKind == JsonValueKind.Number
                ? pEl.GetInt32()
                : 0;

            int? buildingId = null;
            if (el.TryGetProperty("buildingid", out var bEl) && bEl.ValueKind == JsonValueKind.Number)
                buildingId = bEl.GetInt32();

            var groupName = el.TryGetProperty("groupname", out var gnEl) && gnEl.ValueKind == JsonValueKind.String
                ? gnEl.GetString() ?? string.Empty
                : string.Empty;

            var createdBy = el.TryGetProperty("createdby", out var cbEl) && cbEl.ValueKind == JsonValueKind.Number
                ? cbEl.GetInt32()
                : 0;

            DateTime createdOn = DateTime.UtcNow;
            if (el.TryGetProperty("createdon", out var coEl) && coEl.ValueKind == JsonValueKind.String)
            {
                DateTime.TryParse(coEl.GetString(), out createdOn);
            }

            return new GroupChatModel
            {
                Id = id,
                PropertyId = propertyId,
                BuildingId = buildingId,
                GroupName = groupName,
                CreatedBy = createdBy,
                CreatedOn = createdOn
            };
        }

        public async Task<int> CreatePollAsync(int createdBy, ChatPollCreateModel model)
        {
            const string query = @"SELECT public.sp_api_chatpoll(@p_operation, @p_json)::text;";

            var payload = new
            {
                chatid = model.ChatId,
                question = model.Question,
                allowsmultiple = model.AllowsMultiple,
                expiresat = model.ExpiresAt?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                createdby = createdBy,
                options = model.Options
            };

            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(query, _connection);
                cmd.Parameters.AddWithValue("p_operation", 2); // create
                cmd.Parameters.AddWithValue("p_json", (object)jsonPayload ?? DBNull.Value);

                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar is null || scalar is DBNull)
                    throw new ApplicationException("sp_api_chatpoll returned null on create.");

                var json = scalar.ToString()!;
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var status = root.GetProperty("status_code").GetInt32();
                if (status != 201)
                {
                    var msg = root.TryGetProperty("message", out var m) ? m.GetString() : "Create poll failed";
                    throw new ApplicationException(msg);
                }

                var data = root.GetProperty("data");
                var id = data.GetProperty("id").GetInt32();
                return id;
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        public async Task<IEnumerable<ChatPollViewModel>> GetPollsForChatAsync(int chatId, int userId)
        {
            const string query = @"SELECT public.sp_api_chatpoll(@p_operation, @p_json)::text;";

            var payload = new
            {
                chatid = chatId,
                userid = userId
            };

            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(query, _connection);
                cmd.Parameters.AddWithValue("p_operation", 1); // fetch
                cmd.Parameters.AddWithValue("p_json", (object)jsonPayload ?? DBNull.Value);

                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar is null || scalar is DBNull)
                    return Array.Empty<ChatPollViewModel>();

                var json = scalar.ToString()!;
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var status = root.GetProperty("status_code").GetInt32();
                if (status != 200)
                {
                    var msg = root.TryGetProperty("message", out var m) ? m.GetString() : "Fetch polls failed";
                    throw new ApplicationException(msg);
                }

                var data = root.GetProperty("data");
                if (data.ValueKind != JsonValueKind.Array)
                    return Array.Empty<ChatPollViewModel>();

                var list = new List<ChatPollViewModel>();
                foreach (var pollEl in data.EnumerateArray())
                {
                    var poll = new ChatPollViewModel
                    {
                        Id = pollEl.GetProperty("id").GetInt32(),
                        ChatId = pollEl.GetProperty("chatid").GetInt32(),
                        Question = pollEl.GetProperty("question").GetString() ?? "",
                        AllowsMultiple = pollEl.GetProperty("allowsmultiple").GetBoolean(),
                        IsActive = pollEl.GetProperty("isactive").GetBoolean(),
                        CreatedBy = pollEl.GetProperty("createdby").GetInt32(),
                        CreatedOn = DateTime.TryParse(pollEl.GetProperty("createdon").GetString(), out var co) ? co : DateTime.UtcNow
                    };

                    if (pollEl.TryGetProperty("expiresat", out var exEl) && exEl.ValueKind == JsonValueKind.String)
                    {
                        if (DateTime.TryParse(exEl.GetString(), out var exdt)) poll.ExpiresAt = exdt;
                    }

                    // options
                    if (pollEl.TryGetProperty("options", out var optionsEl) && optionsEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var opt in optionsEl.EnumerateArray())
                        {
                            var optionVm = new ChatPollOptionViewModel
                            {
                                Id = opt.GetProperty("id").GetInt32(),
                                Text = opt.GetProperty("text").GetString() ?? "",
                                Sequence = opt.GetProperty("seq").GetInt32(),
                                Votes = opt.GetProperty("votes").GetInt32()
                            };
                            poll.Options.Add(optionVm);
                        }
                    }

                    // uservotes
                    if (pollEl.TryGetProperty("uservotes", out var uvEl) && uvEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var v in uvEl.EnumerateArray())
                        {
                            if (v.ValueKind == JsonValueKind.Number)
                                poll.UserVotedOptionIds.Add(v.GetInt32());
                        }
                    }

                    list.Add(poll);
                }

                return list;
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        public async Task<ChatPollFeedModel?> GetPollByPollIdAsync(int pollId, int userId)
        {
            const string sql = @"SELECT public.sp_api_chatpoll(@p_operation, @p_json)::text;";

            var payload = new
            {
                pollid = pollId,
                userid = userId
            };

            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(sql, _connection);
                cmd.Parameters.AddWithValue("p_operation", 5); // 🔹 get poll by id
                cmd.Parameters.AddWithValue("p_json", jsonPayload);

                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar is null || scalar is DBNull)
                    return null;

                var json = scalar.ToString();
                if (string.IsNullOrWhiteSpace(json))
                    return null;

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.GetProperty("status_code").GetInt32() != 200)
                    return null;

                var data = root.GetProperty("data");

                return JsonSerializer.Deserialize<ChatPollFeedModel>(
                    data.GetRawText(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        public async Task ClosePollAsync(int pollId)
        {
            const string query = @"SELECT public.sp_api_chatpoll(@p_operation, @p_json)::text;";

            var payload = new { pollid = pollId };
            var json = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(query, _connection);
                cmd.Parameters.AddWithValue("p_operation", 3); // close
                cmd.Parameters.AddWithValue("p_json", (object)json ?? DBNull.Value);

                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar is null || scalar is DBNull) throw new ApplicationException("sp_api_chatpoll returned null on close.");

                var res = scalar.ToString()!;
                using var doc = JsonDocument.Parse(res);
                var root = doc.RootElement;
                var status = root.GetProperty("status_code").GetInt32();
                if (status != 200)
                    throw new ApplicationException(root.TryGetProperty("message", out var m) ? m.GetString() : "Close poll failed");
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        public async Task VoteAsync(int pollId, int userId, IEnumerable<int> optionIds)
        {
            const string query = @"SELECT public.sp_api_chatpoll(@p_operation, @p_json)::text;";

            var payload = new
            {
                pollid = pollId,
                userid = userId,
                optionids = optionIds
            };

            var json = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(query, _connection);
                cmd.Parameters.AddWithValue("p_operation", 4); // vote
                cmd.Parameters.AddWithValue("p_json", (object)json ?? DBNull.Value);

                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar is null || scalar is DBNull) throw new ApplicationException("sp_api_chatpoll returned null on vote.");

                var res = scalar.ToString()!;
                using var doc = JsonDocument.Parse(res);
                var root = doc.RootElement;
                var status = root.GetProperty("status_code").GetInt32();
                if (status != 200)
                    throw new ApplicationException(root.TryGetProperty("message", out var m) ? m.GetString() : "Vote failed");
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        public async Task<List<object>> GetChatFeedAsync(int chatId,int userId,int skip,int take)
        {
            const string sql = @"SELECT public.sp_api_getchatfeed(@p_operation, @p_json)::text;";

            var payload = new
            {
                chatid = chatId,
                userid = userId,
                skip = skip,
                take = take
            };

            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(sql, _connection);
                cmd.Parameters.AddWithValue("p_operation", 1);
                cmd.Parameters.AddWithValue("p_json", jsonPayload);

                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar == null || scalar == DBNull.Value)
                    return new List<object>();

                var json = scalar.ToString();
                using var doc = JsonDocument.Parse(json!);
                var root = doc.RootElement;

                if (root.GetProperty("status_code").GetInt32() != 200)
                    throw new ApplicationException(
                        root.GetProperty("message").GetString()
                    );

                var items = root
                    .GetProperty("data")
                    .GetProperty("items");

                var result = new List<object>();

                foreach (var item in items.EnumerateArray())
                {
                    // Keep raw JSON → frontend decides rendering
                    result.Add(JsonSerializer.Deserialize<object>(item.GetRawText())!);
                }

                return result;
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        /// <summary>
        /// Get user IDs of all members in a chat group.
        /// Uses sp_api_groupchatmaster with operation = 7 to get chat members.
        /// </summary>
        public async Task<List<int>> GetChatMemberUserIdsAsync(int chatId)
        {
            const string query = @"SELECT public.sp_api_groupchatmaster(@p_operation, @p_json)::text;";

            var payload = new { chatid = chatId };
            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(query, _connection);
                cmd.Parameters.AddWithValue("p_operation", 7); // get chat members
                cmd.Parameters.AddWithValue("p_json", (object)jsonPayload ?? DBNull.Value);

                var scalarResult = await cmd.ExecuteScalarAsync();
                if (scalarResult is null || scalarResult is DBNull)
                    return new List<int>();

                var resultJson = scalarResult.ToString()!;
                using var doc = JsonDocument.Parse(resultJson);
                var root = doc.RootElement;

                if (!root.TryGetProperty("status_code", out var statusEl))
                    return new List<int>();

                var statusCode = statusEl.GetInt32();
                if (statusCode != 200)
                    return new List<int>();

                if (!root.TryGetProperty("data", out var dataEl) || dataEl.ValueKind != JsonValueKind.Array)
                    return new List<int>();

                var userIds = new List<int>();
                foreach (var item in dataEl.EnumerateArray())
                {
                    if (item.TryGetProperty("userid", out var userIdEl) && userIdEl.ValueKind == JsonValueKind.Number)
                    {
                        userIds.Add(userIdEl.GetInt32());
                    }
                }

                return userIds;
            }
            catch (Exception)
            {
                // Log error if needed
                return new List<int>();
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        /// <summary>
        /// Get FCM tokens for a list of user IDs from usermaster table.
        /// Returns only non-null, non-empty tokens.
        /// Note: Assumes the column name is 'fcmtoken' in usermaster table.
        /// If your column name is different (e.g., 'fcm_token'), update the query accordingly.
        /// </summary>
        public async Task<List<string>> GetFcmTokensForUserIdsAsync(List<int> userIds)
        {
            if (userIds == null || userIds.Count == 0)
                return new List<string>();

            // Note: If your FCM token column has a different name, update 'fcmtoken' here
            const string query = @"
                SELECT DISTINCT fcmtoken 
                FROM usermaster 
                WHERE userid = ANY(:userIds) 
                AND fcmtoken IS NOT NULL 
                AND fcmtoken != '';";

            var tokens = new List<string>();

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(query, _connection);
                cmd.Parameters.AddWithValue("userIds", userIds.ToArray());

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0))
                    {
                        var token = reader.GetString(0);
                        if (!string.IsNullOrWhiteSpace(token))
                        {
                            tokens.Add(token);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Log error if needed
                return new List<string>();
            }
            finally
            {
                await _connection.CloseAsync();
            }

            return tokens;
        }
    }
}
