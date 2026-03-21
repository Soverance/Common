namespace Soverance.Auth.Models;

public class SamlConfig
{
    public int SamlConfigId { get; set; }
    public bool IsEnabled { get; set; }
    public string IdpEntityId { get; set; } = null!;
    public string IdpSsoUrl { get; set; } = null!;
    public string? IdpSloUrl { get; set; }
    public string IdpCertificate { get; set; } = null!;
    public string SpEntityId { get; set; } = null!;
    public bool AutoProvision { get; set; }

    public ICollection<SamlRoleMapping> RoleMappings { get; set; } = new List<SamlRoleMapping>();
}
