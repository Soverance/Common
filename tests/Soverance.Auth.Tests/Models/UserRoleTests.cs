using Soverance.Auth.Models;
using Xunit;

namespace Soverance.Auth.Tests.Models;

public class UserRoleTests
{
    [Fact]
    public void UserRole_IntegerValues_AreStable()
    {
        // These integer values are persisted in every consuming app's database.
        // Changing them would corrupt existing data. Append new roles only.
        Assert.Equal(0, (int)UserRole.Member);
        Assert.Equal(1, (int)UserRole.Moderator);
        Assert.Equal(2, (int)UserRole.Admin);
        Assert.Equal(3, (int)UserRole.User);
    }
}
