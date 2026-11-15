using Sugarista.Application.Sms;
using Sugarista.Infrastructure.Settings;

namespace Sugarista.Infrastructure.Sms;

/// <summary>AppSetupStore-backed SMS policy persistence (app-setup.json).</summary>
public sealed class AppSetupSmsSettingsStore(AppSetupStore appSetupStore) : ISmsSettingsStore
{
    public SmsGlobalSettings Get() => appSetupStore.GetSmsSettings();

    public void Save(SmsGlobalSettings settings) => appSetupStore.PatchSmsSettings(settings);
}
