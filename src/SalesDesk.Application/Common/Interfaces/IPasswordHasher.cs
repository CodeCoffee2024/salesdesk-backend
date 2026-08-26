namespace SalesDesk.Application.Common.Interfaces;

/// <summary>
/// Hashes and verifies user passwords. Kept out of Domain/Application so handlers
/// depend only on this abstraction — Infrastructure supplies the actual algorithm.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}
