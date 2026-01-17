using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;

namespace Gated_System.Helpers
{
    public class PushNotificationHelper
    {
        private readonly ILogger<PushNotificationHelper> _logger;

        public PushNotificationHelper(ILogger<PushNotificationHelper> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Send a notification to a single device token.
        /// </summary>
        public async Task<string?> SendToDeviceAsync(string deviceToken, string title, string body, IDictionary<string, string>? data = null)
        {
            if (string.IsNullOrWhiteSpace(deviceToken))
                throw new ArgumentException("Device token is required", nameof(deviceToken));

            var message = new Message()
            {
                Token = deviceToken,
                Notification = new Notification
                {
                    Title = title,
                    Body = body
                },
                Data = data != null ? new Dictionary<string, string>(data) : null
            };

            try
            {
                var result = await FirebaseMessaging.DefaultInstance.SendAsync(message);
                _logger.LogInformation("FCM message sent. MessageId={MessageId}", result);
                return result;
            }
            catch (FirebaseMessagingException fex)
            {
                _logger.LogError(fex, "FCM send failed: {Message}", fex.Message);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error sending FCM message: {Message}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Send a notification to multiple device tokens (multicast).
        /// </summary>
        public async Task<BatchResponse?> SendToDevicesAsync(IEnumerable<string> deviceTokens, string title, string body, IDictionary<string, string>? data = null)
        {
            var tokens = deviceTokens?.Where(t => !string.IsNullOrWhiteSpace(t)).ToList() ?? new List<string>();
            if (tokens.Count == 0) return null;

            var message = new MulticastMessage
            {
                Tokens = tokens,
                Notification = new Notification
                {
                    Title = title,
                    Body = body
                },
                Data = data != null ? new Dictionary<string, string>(data) : null
            };

            try
            {
                var response = await FirebaseMessaging.DefaultInstance.SendMulticastAsync(message);
                _logger.LogInformation("FCM multicast sent. SuccessCount={Success}, FailureCount={Fail}", response.SuccessCount, response.FailureCount);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending multicast FCM message: {Message}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Send a notification to a topic. Topic name must match FCM topic rules.
        /// </summary>
        public async Task<string?> SendToTopicAsync(string topic, string title, string body, IDictionary<string, string>? data = null)
        {
            if (string.IsNullOrWhiteSpace(topic))
                throw new ArgumentException("Topic is required", nameof(topic));

            var message = new Message()
            {
                Topic = topic,
                Notification = new Notification
                {
                    Title = title,
                    Body = body
                },
                Data = data != null ? new Dictionary<string, string>(data) : null
            };

            try
            {
                var result = await FirebaseMessaging.DefaultInstance.SendAsync(message);
                _logger.LogInformation("FCM topic message sent. MessageId={MessageId}", result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending topic FCM message: {Message}", ex.Message);
                return null;
            }
        }
    }
}
