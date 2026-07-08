namespace Sugarista.Infrastructure.Settings;

public class AdminBootstrapOptions
{
    public const string SectionName = "AdminBootstrap";

    public string Username { get; set; } = "admin";

    public string DisplayName { get; set; } = "Sugarista Admin";

    public string Password { get; set; } = "ChangeMe123!";
}
