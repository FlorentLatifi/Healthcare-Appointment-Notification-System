namespace Healthcare.Application.Ports.Authentication;

public interface IBreachedPasswordChecker
{
    Task<bool> IsPasswordBreachedAsync(string password, CancellationToken cancellationToken = default);
}
