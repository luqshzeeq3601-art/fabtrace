using FabTrace.Contracts;

namespace FabTrace.Web.Services;

public sealed class UserSession
{
    public string? UserName { get; private set; }
    public IReadOnlyList<string> Roles { get; private set; } = [];
    public string? Token { get; private set; }
    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);

    public event Action? OnChange;

    public void Login(string userName, IReadOnlyList<string> roles, string token)
    {
        UserName = userName;
        Roles = roles;
        Token = token;
        OnChange?.Invoke();
    }

    public void Logout()
    {
        UserName = null;
        Roles = [];
        Token = null;
        OnChange?.Invoke();
    }

    public bool HasRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
}