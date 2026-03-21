namespace Soverance.Auth.Models;

public class SamlRoleMapping
{
    public int SamlRoleMappingId { get; set; }
    public int SamlConfigId { get; set; }
    public string IdpGroupId { get; set; } = null!;
    public string RoleName { get; set; } = null!;

    public SamlConfig SamlConfig { get; set; } = null!;
}
