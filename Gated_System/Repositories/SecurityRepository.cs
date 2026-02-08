using Npgsql;
using System.Text.Json;

namespace Gated_System.Repositories
{
    public class SecurityRepository : ISecurityRepository
    {
        private readonly NpgsqlConnection _connection;

        public SecurityRepository(NpgsqlConnection connection)
        {
            _connection = connection;
        }

        public async Task<JsonDocument> VerifyQrRawAsync(object payload)
        {
            const string fn = @"SELECT public.sp_api_verify_qrcode(@p_qrcode)::text;";

            // Extract qrcode from the payload object
            string qrCode = "";

            if (payload != null)
            {
                var prop = payload.GetType().GetProperty("qrcode");
                if (prop != null)
                {
                    qrCode = prop.GetValue(payload)?.ToString() ?? "";
                }
            }

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(fn, _connection);
                cmd.Parameters.AddWithValue("p_qrcode", qrCode);

                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar is null || scalar is DBNull)
                    throw new ApplicationException("SP returned empty result.");

                return JsonDocument.Parse(scalar.ToString()!);
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }


        public async Task<JsonDocument> CreateVisitorLogRawAsync(object payload)
        {
            const string fn = @"SELECT public.sp_api_visitorlog(@p_operation, @p_json)::text;";
            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(fn, _connection);
                cmd.Parameters.AddWithValue("p_operation", 2);
                cmd.Parameters.AddWithValue("p_json", (object)jsonPayload ?? DBNull.Value);

                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar is null || scalar is DBNull) throw new ApplicationException("SP returned empty result.");
                return JsonDocument.Parse(scalar.ToString()!);
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        public async Task<JsonDocument> UpdateVisitorRequestStatusRawAsync(object payload)
        {
            const string fn = @"SELECT public.sp_api_visitorrequest(@p_operation, @p_json)::text;";
            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(fn, _connection);
                cmd.Parameters.AddWithValue("p_operation", 3);
                cmd.Parameters.AddWithValue("p_json", (object)jsonPayload ?? DBNull.Value);

                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar is null || scalar is DBNull) throw new ApplicationException("SP returned empty result.");
                return JsonDocument.Parse(scalar.ToString()!);
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        public async Task<JsonDocument> UpdateVisitorLogExitRawAsync(object payload)
        {
            const string fn = @"SELECT public.sp_api_visitorlog(@p_operation, @p_json)::text;";
            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(fn, _connection);
                cmd.Parameters.AddWithValue("p_operation", 3);
                cmd.Parameters.AddWithValue("p_json", (object)jsonPayload ?? DBNull.Value);

                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar is null || scalar is DBNull) throw new ApplicationException("SP returned empty result.");
                return JsonDocument.Parse(scalar.ToString()!);
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        public async Task<int> CreateManualVisitorRequestAsync(object payload)
        {
            const string fn = @"SELECT public.sp_api_visitorrequest(@p_operation, @p_json)::text;";
            var jsonPayload = JsonSerializer.Serialize(payload);

            await _connection.OpenAsync();
            try
            {
                using var cmd = new NpgsqlCommand(fn, _connection);
                cmd.Parameters.AddWithValue("p_operation", 2);
                cmd.Parameters.AddWithValue("p_json", (object)jsonPayload ?? DBNull.Value);

                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar is null || scalar is DBNull)
                    throw new ApplicationException("Stored-proc returned empty result.");

                var result = scalar.ToString()!;
                using var doc = JsonDocument.Parse(result);
                var root = doc.RootElement;

                var status = root.GetProperty("status_code").GetInt32();
                if (status != 201)
                {
                    var msg = root.TryGetProperty("message", out var m) ? m.GetString() : "Failed creating visitor request";
                    throw new ApplicationException(msg);
                }

                var id = root.GetProperty("data").GetProperty("id").GetInt32();
                return id;
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }
    }
}
