using System.Text.Json.Serialization;

namespace Soverance.Auth.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UserRole
{
    Member,
    Moderator,
    Admin,
    User
}
