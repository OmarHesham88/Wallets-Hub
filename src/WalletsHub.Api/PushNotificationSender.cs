using System.Text.Json;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;

namespace WalletsHub.Api;

public interface IPushNotificationSender
{
    bool IsConfigured { get; }
    Task<PushDeliveryResult> SendPaymentAsync(IReadOnlyCollection<string> tokens, string title, string body, Guid receiptId, CancellationToken cancellationToken = default);
}

public sealed record PushDeliveryResult(int Delivered, IReadOnlyCollection<string> InvalidTokens)
{
    public static PushDeliveryResult Empty { get; } = new(0, []);
}

public sealed class FirebasePushNotificationSender : IPushNotificationSender, IDisposable
{
    private readonly ILogger<FirebasePushNotificationSender> logger;
    private readonly FirebaseApp? firebaseApp;
    private readonly FirebaseMessaging? messaging;

    public FirebasePushNotificationSender(IConfiguration configuration, ILogger<FirebasePushNotificationSender> logger)
    {
        this.logger = logger;
        var path = configuration["Firebase:ServiceAccountPath"];
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            logger.LogWarning("Firebase push delivery is disabled because no service-account file is configured.");
            return;
        }

        try
        {
            var serviceAccount = JsonSerializer.Deserialize<ServiceAccountFile>(File.ReadAllText(path));
            if (serviceAccount is null || string.IsNullOrWhiteSpace(serviceAccount.ProjectId) || string.IsNullOrWhiteSpace(serviceAccount.ClientEmail) || string.IsNullOrWhiteSpace(serviceAccount.PrivateKey))
            {
                logger.LogWarning("Firebase push delivery is disabled because the service-account file is incomplete.");
                return;
            }

            var credential = new ServiceAccountCredential(new ServiceAccountCredential.Initializer(serviceAccount.ClientEmail)
            {
                Scopes = ["https://www.googleapis.com/auth/firebase.messaging"]
            }.FromPrivateKey(serviceAccount.PrivateKey));
            firebaseApp = FirebaseApp.Create(new AppOptions
            {
                Credential = GoogleCredential.FromCredential(credential),
                ProjectId = serviceAccount.ProjectId
            }, "wallets-hub-push");
            messaging = FirebaseMessaging.GetMessaging(firebaseApp);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Firebase push delivery could not be initialized.");
        }
    }

    public bool IsConfigured => messaging is not null;

    public async Task<PushDeliveryResult> SendPaymentAsync(IReadOnlyCollection<string> tokens, string title, string body, Guid receiptId, CancellationToken cancellationToken = default)
    {
        if (messaging is null || tokens.Count == 0) return PushDeliveryResult.Empty;
        var delivered = 0;
        var invalidTokens = new List<string>();
        foreach (var batch in tokens.Distinct(StringComparer.Ordinal).Chunk(500))
        {
            try
            {
                var response = await messaging.SendEachForMulticastAsync(new MulticastMessage
                {
                    Tokens = batch,
                    Notification = new Notification { Title = title, Body = body },
                    Data = new Dictionary<string, string> { ["link"] = "/receipts", ["receiptId"] = receiptId.ToString() },
                    Android = new AndroidConfig
                    {
                        Priority = Priority.High,
                        Notification = new AndroidNotification { ChannelId = "payments", Sound = "default", Color = "#147A52" }
                    }
                }, cancellationToken);
                delivered += response.SuccessCount;
                for (var index = 0; index < response.Responses.Count; index++)
                {
                    var error = response.Responses[index].Exception?.MessagingErrorCode;
                    if (error is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument) invalidTokens.Add(batch[index]);
                }
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Firebase payment notification delivery failed for {TokenCount} devices.", batch.Length);
            }
        }
        return new PushDeliveryResult(delivered, invalidTokens);
    }

    public void Dispose() => firebaseApp?.Delete();

    private sealed record ServiceAccountFile(
        [property: System.Text.Json.Serialization.JsonPropertyName("project_id")] string ProjectId,
        [property: System.Text.Json.Serialization.JsonPropertyName("client_email")] string ClientEmail,
        [property: System.Text.Json.Serialization.JsonPropertyName("private_key")] string PrivateKey);
}
