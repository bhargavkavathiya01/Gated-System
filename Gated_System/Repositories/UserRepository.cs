using Gated_System.Models;
using Npgsql;
using System.Text.Json;

namespace Gated_System.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly NpgsqlConnection _connection;

        public UserRepository(NpgsqlConnection connection)
        {
            _connection = connection;
        }


        public async Task<bool> UpdateUserAsync(UserModel user)
        {
            const string query = @"SELECT public.sp_api_usermaster(@p_operation, @p_json)::text;";

            var payload = new
            {
                id = user.Id,
                firstname = user.Firstname,
                middlename = user.Middlename,
                lastname = user.Lastname,
                email = user.Email,
                phone = user.Phone,
                modifiedby = user.CreatedBy
            };

            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(query, _connection);
                cmd.Parameters.AddWithValue("p_operation", 3); // 3 = Update
                cmd.Parameters.AddWithValue("p_json", (object)jsonPayload ?? DBNull.Value);

                var scalarResult = await cmd.ExecuteScalarAsync();
                if (scalarResult is null) throw new Exception("Database returned no result.");

                using var doc = JsonDocument.Parse(scalarResult.ToString()!);
                var root = doc.RootElement;

                int statusCode = root.GetProperty("status_code").GetInt32();
                string message = root.GetProperty("message").GetString() ?? "";

                // Handle specific business logic errors from SP
                if (statusCode == 409) // Conflict: Email or Phone exists
                {
                    throw new ApplicationException(message);
                }

                return statusCode == 200;
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        public async Task<bool> UpdatePasswordAsync(UpdatePasswordModel model)
        {
            const string query = @"SELECT public.sp_api_usermaster(@p_operation, @p_json)::text;";

            var payload = new
            {
                id = model.UserId,
                password = model.NewPassword,
                modifiedby = model.ModifiedBy
            };

            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(query, _connection);
                cmd.Parameters.AddWithValue("p_operation", 3);
                cmd.Parameters.AddWithValue("p_json", (object)jsonPayload ?? DBNull.Value);

                var scalarResult = await cmd.ExecuteScalarAsync();
                if (scalarResult == null) return false;

                using var doc = JsonDocument.Parse(scalarResult.ToString()!);
                // Return true if status_code is 200
                return doc.RootElement.GetProperty("status_code").GetInt32() == 200;
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }
    }
}
