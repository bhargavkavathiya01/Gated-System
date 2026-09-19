using dotAPNS;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Gated_System.Helpers
{
    public class ApnsVoipOptions
    {
        public string VoipCertPath { get; set; } = "apns-voip-cert.p12";
        public string VoipCertPassword { get; set; } = "";
        public bool UseSandbox { get; set; }
    }

    /// <summary>
    /// Sends Apple VoIP (PushKit) pushes, which are what trigger the native CallKit
    /// incoming-call screen on iOS. Firebase cannot deliver this push type, so this
    /// talks to APNs directly and is separate from <see cref="PushNotificationHelper"/>.
    /// </summary>
    public class VoipPushHelper
    {
        private readonly ILogger<VoipPushHelper> _logger;
        private readonly IApnsClient? _apns;
        private readonly bool _useSandbox;

        public bool IsConfigured => _apns != null;

        /// <summary>Diagnostics for the /voip-status endpoint. Exposed because the
        /// host's stdout logging is unreliable.</summary>
        public string Status { get; private set; } = "not initialized";
        public bool UseSandbox => _useSandbox;
        public string LastSendResult { get; private set; } = "no push attempted yet";

        public VoipPushHelper(IOptions<ApnsVoipOptions> options, ILogger<VoipPushHelper> logger)
        {
            _logger = logger;
            var opts = options.Value;
            _useSandbox = opts.UseSandbox;

            try
            {
                var cert = LoadCertificate(opts, _logger, out var diagnostics);
                _apns = ApnsClient.CreateUsingCert(cert);
                ApnsClient.GetCertificateInfo(cert, out _, out var isVoipCert, out var bundleId);
                Status = $"OK | os={(OperatingSystem.IsWindows() ? "Windows" : "Linux")} | {diagnostics} " +
                         $"| topic={bundleId}.voip | isVoipCert={isVoipCert} | expires={cert.NotAfter:yyyy-MM-dd}";
                _logger.LogInformation("APNs VoIP client initialized. Sandbox={Sandbox}", _useSandbox);
            }
            catch (Exception ex)
            {
                // Never let a missing/bad certificate take the app down - VoIP pushes
                // simply stay disabled and callers fall back to FCM.
                Status = $"FAILED | {ex.GetType().Name}: {ex.Message}";
                _logger.LogError(ex, "APNs VoIP certificate could not be loaded. VoIP pushes are disabled.");
                _apns = null;
            }
        }

        private static X509Certificate2 LoadCertificate(ApnsVoipOptions opts, ILogger logger, out string diagnostics)
        {
            // Cert bytes come from a base64 env var when set, otherwise from the file
            // shipped alongside the app.
            var base64 = Environment.GetEnvironmentVariable("APNS_VOIP_CERT_BASE64");
            var password = Environment.GetEnvironmentVariable("APNS_VOIP_CERT_PASSWORD")
                           ?? opts.VoipCertPassword;

            byte[] raw;
            string source;
            if (!string.IsNullOrWhiteSpace(base64))
            {
                logger.LogInformation("APNs VoIP certificate source: APNS_VOIP_CERT_BASE64 env var.");
                raw = Convert.FromBase64String(base64);
                source = "source=env-base64";
            }
            else
            {
                // Resolve against the app's own folder. A relative path would otherwise
                // resolve against the process working directory, which under IIS
                // in-process hosting is not necessarily the app directory.
                var path = opts.VoipCertPath;
                if (!Path.IsPathRooted(path))
                    path = Path.Combine(AppContext.BaseDirectory, path);

                logger.LogInformation("APNs VoIP certificate path: {Path} (exists: {Exists})",
                    path, File.Exists(path));

                if (!File.Exists(path))
                    throw new FileNotFoundException($"APNs VoIP certificate not found at '{path}'.", path);

                raw = File.ReadAllBytes(path);
                source = $"path={path}";
            }

            logger.LogInformation("APNs VoIP certificate password supplied: {HasPassword}",
                !string.IsNullOrEmpty(password));

            diagnostics = $"{source} | passwordSupplied={!string.IsNullOrEmpty(password)}";

            // Key storage flags are a Windows concept. On Linux (AWS Elastic Beanstalk)
            // the plain load works and the Windows-only flags are meaningless, so try
            // the simple option first there.
            // On Windows/IIS the default UserKeySet load needs the app pool's user
            // profile, which shared hosting often doesn't provide ("The system cannot
            // find the file specified"), and MachineKeySet can hit "Access denied" -
            // hence the ordered attempts.
            // EphemeralKeySet is deliberately NOT used on Windows: Schannel cannot use
            // ephemeral keys for TLS *client* authentication, which is what APNs needs.
            var attempts = OperatingSystem.IsWindows()
                ? new[]
                {
                    X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable,
                    X509KeyStorageFlags.UserKeySet    | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable,
                    X509KeyStorageFlags.DefaultKeySet
                }
                : new[]
                {
                    X509KeyStorageFlags.DefaultKeySet,
                    X509KeyStorageFlags.Exportable,
                    X509KeyStorageFlags.EphemeralKeySet
                };

            Exception? last = null;
            foreach (var flags in attempts)
            {
                try
                {
                    var cert = new X509Certificate2(raw, password, flags);

                    // Loading can succeed while the private key stays unusable, which
                    // would only surface later as a TLS handshake failure.
                    if (!cert.HasPrivateKey || cert.GetRSAPrivateKey() == null)
                        throw new CryptographicException("Certificate loaded but its private key is not usable.");

                    logger.LogInformation("APNs VoIP certificate loaded using {Flags}.", flags);
                    return cert;
                }
                catch (Exception ex)
                {
                    logger.LogWarning("APNs VoIP certificate load failed with {Flags}: {Message}", flags, ex.Message);
                    last = ex;
                }
            }

            throw last ?? new CryptographicException("Could not load the APNs VoIP certificate.");
        }

        /// <summary>
        /// Sends an incoming-call VoIP push. Returns true only if APNs accepted it.
        /// </summary>
        public async Task<bool> SendCallAsync(
            string voipToken,
            string alert,
            string nameCaller,
            string handle,
            IDictionary<string, string> extra)
        {
            if (_apns == null)
            {
                LastSendResult = "SKIPPED: APNs client is not configured.";
                _logger.LogWarning("VoIP push skipped: APNs client is not configured.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(voipToken))
            {
                LastSendResult = "SKIPPED: empty VoIP token.";
                return false;
            }

            try
            {
                var push = new ApplePush(ApplePushType.Voip)
                    .AddVoipToken(voipToken)
                    .AddAlert(alert)
                    .AddImmediateExpiration()
                    .AddCustomProperty("id", Guid.NewGuid().ToString())
                    .AddCustomProperty("nameCaller", nameCaller)
                    .AddCustomProperty("handle", handle)
                    .AddCustomProperty("isVideo", false)
                    .AddCustomProperty("extra", extra);

                if (_useSandbox)
                    push.SendToDevelopmentServer();

                var response = await _apns.SendAsync(push);

                if (response.IsSuccessful)
                {
                    LastSendResult = $"OK at {DateTime.UtcNow:HH:mm:ss}Z | token={Preview(voipToken)}";
                    _logger.LogInformation("VoIP push delivered to APNs.");
                    return true;
                }

                // Reason matters for debugging: BadDeviceToken usually means the token
                // came from the wrong APNs environment, Unregistered means it's dead.
                LastSendResult = $"REJECTED at {DateTime.UtcNow:HH:mm:ss}Z | Reason={response.Reason} " +
                                 $"| {response.ReasonString} | token={Preview(voipToken)}";
                _logger.LogError("VoIP push rejected by APNs. Reason={Reason}, Detail={Detail}",
                    response.Reason, response.ReasonString);
                return false;
            }
            catch (Exception ex)
            {
                LastSendResult = $"ERROR at {DateTime.UtcNow:HH:mm:ss}Z | {ex.GetType().Name}: {ex.Message}" +
                                 (ex.InnerException != null ? $" | inner: {ex.InnerException.Message}" : "");
                _logger.LogError(ex, "Error sending VoIP push: {Message}", ex.Message);
                return false;
            }
        }

        private static string Preview(string token) =>
            token.Length <= 12 ? token : $"{token[..6]}...{token[^6..]} (len={token.Length})";
    }
}
