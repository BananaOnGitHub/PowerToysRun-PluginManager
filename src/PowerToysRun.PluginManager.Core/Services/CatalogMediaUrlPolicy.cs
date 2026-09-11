namespace PowerToysRun.PluginManager.Core.Services;

public static class CatalogMediaUrlPolicy
{
    private const int MaximumUrlLength = 2048;

    private static readonly HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "github.com",
        "github.githubassets.com",
        "media.githubusercontent.com",
        "raw.githubusercontent.com",
        "user-images.githubusercontent.com",
    };

    public static bool IsAllowed(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > MaximumUrlLength ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !uri.IsDefaultPort ||
            !AllowedHosts.Contains(uri.Host))
        {
            return false;
        }

        var extension = Path.GetExtension(uri.AbsolutePath);
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase);
    }
}
