namespace Sugarista.Application.Sms;

/// <summary>Reads/writes the global SMS policy (persisted by Infrastructure in app-setup.json).</summary>
public interface ISmsSettingsStore
{
    SmsGlobalSettings Get();

    void Save(SmsGlobalSettings settings);
}
