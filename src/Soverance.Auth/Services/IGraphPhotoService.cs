namespace Soverance.Auth.Services;

public interface IGraphPhotoService
{
    Task<(byte[] Data, string ContentType)?> GetUserPhotoAsync(string userEmail);
}
