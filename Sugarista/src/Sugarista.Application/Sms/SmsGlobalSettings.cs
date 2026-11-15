namespace Sugarista.Application.Sms;

/// <summary>
/// Global SMS policies, editable from the admin panel (persisted in
/// app-setup.json via AppSetupStore so nothing depends on appsettings).
/// </summary>
public sealed record SmsGlobalSettings(
    int MaxRecipientsPerSend,
    int BatchSize,
    int MaxRetries,
    int RetryDelaySeconds,
    int DailyMessageLimit,
    bool BulkEnabled,
    bool FailoverEnabled,
    Guid? FailoverProviderId,
    int HistoryRetentionDays)
{
    public static SmsGlobalSettings Defaults { get; } = new(
        MaxRecipientsPerSend: 5000,
        BatchSize: 50,
        MaxRetries: 2,
        RetryDelaySeconds: 15,
        DailyMessageLimit: 2000,
        BulkEnabled: true,
        FailoverEnabled: false,
        FailoverProviderId: null,
        HistoryRetentionDays: 90);

    public SmsGlobalSettings Clamp() => this with
    {
        MaxRecipientsPerSend = Math.Clamp(MaxRecipientsPerSend, 1, 100_000),
        BatchSize = Math.Clamp(BatchSize, 1, 500),
        MaxRetries = Math.Clamp(MaxRetries, 0, 5),
        RetryDelaySeconds = Math.Clamp(RetryDelaySeconds, 1, 600),
        DailyMessageLimit = Math.Clamp(DailyMessageLimit, 1, 100_000),
        HistoryRetentionDays = Math.Clamp(HistoryRetentionDays, 7, 730)
    };
}
