using ClashUp.Shared.MessagePackObjects;
using MagicOnion;

namespace ClashUp.Shared.Services
{
    /// <summary>
    /// Phase-1 auth: device-id login only. Shape leaves room for
    /// LoginWithGoogleAsync / LoginWithAppleAsync without breaking callers.
    /// </summary>
    public interface IAuthService : IService<IAuthService>
    {
        UnaryResult<LoginResult> LoginWithDeviceIdAsync(LoginRequest request);
    }
}
