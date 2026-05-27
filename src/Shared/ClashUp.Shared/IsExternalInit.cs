// Polyfill so init-only setters compile against netstandard2.1, where the
// runtime type doesn't exist. Internal so it doesn't leak out of the
// assembly. Same trick used in MagicOnion sample sources.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
