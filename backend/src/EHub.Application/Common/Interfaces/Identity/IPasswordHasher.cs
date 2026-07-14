namespace EHub.Application.Common.Interfaces.Identity;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}
