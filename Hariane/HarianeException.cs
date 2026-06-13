namespace Hariane2Mqtt.Hariane;

public enum HarianeErrorKind
{
    /// <summary>Could not reach hariane.fr (DNS, connection, timeout).</summary>
    Network,

    /// <summary>hariane.fr answered with a server error (5xx) after retries.</summary>
    Server,

    /// <summary>Login rejected — bad username/password.</summary>
    BadCredentials,

    /// <summary>The page/response did not look as expected — the site layout likely changed.</summary>
    SiteChanged,

    /// <summary>An authenticated call was attempted before logging in.</summary>
    NotLoggedIn,
}

/// <summary>A classified Hariane failure, so callers can react (and log) appropriately.</summary>
public class HarianeException(HarianeErrorKind kind, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public HarianeErrorKind Kind { get; } = kind;
}
