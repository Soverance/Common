namespace Soverance.Auth.Services;

public interface IAvatarStore
{
    Task<string> SaveAvatarAsync(Guid userId, byte[] imageData, string contentType);
}
