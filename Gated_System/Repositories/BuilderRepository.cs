using Gated_System.Models;
using Npgsql;
using System.Text.Json;

namespace Gated_System.Repositories
{
    public class BuilderRepository : IBuilderRepository
    {
        private readonly NpgsqlConnection _connection;

        public BuilderRepository(NpgsqlConnection connection)
        {
            _connection = connection;
        }

        public async Task<int> CreateSecretaryRepoAsync(CreateSecretaryModel model)
        {
            // Adjust stored procedure name and operation code as per your DB design.
            const string query = @"SELECT public.sp_api_userrolemaster(@p_operation, @p_json)::text;";

            var payload = new
            {
                propertyid = model.PropertyId,
                userid = model.UserId,
                roleid = model.RoleId,
                createdby = model.CreatedBy
            };

            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(query, _connection);
                cmd.Parameters.AddWithValue("p_operation", 2);
                cmd.Parameters.AddWithValue("p_json", (object)jsonPayload ?? DBNull.Value);

                var scalarResult = await cmd.ExecuteScalarAsync();

                if (scalarResult is null || scalarResult is DBNull)
                    throw new Exception("sp_api_secretary returned null/empty result.");

                var resultJson = scalarResult.ToString();

                using var doc = JsonDocument.Parse(resultJson);
                var root = doc.RootElement;

                // expected: {"status_code":201,"message":"Inserted","data":{"id":123}}
                if (!root.TryGetProperty("data", out var dataEl) || !dataEl.TryGetProperty("id", out var idEl))
                    throw new Exception("Unexpected response from sp_api_secretary: missing data.id");

                var id = idEl.GetInt32();
                return id;
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }
    }
}
