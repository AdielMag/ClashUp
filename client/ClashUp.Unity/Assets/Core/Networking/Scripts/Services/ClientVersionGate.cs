using System;

using Grpc.Core;

namespace ClashUp.Client.Networking
{
    /// <summary>
    /// Detects the server's "your client is too old" signal. The version gateway
    /// answers an unsupported <c>x-client-version</c> with gRPC
    /// <see cref="StatusCode.FailedPrecondition"/> and a
    /// <c>required-action: upgrade-client</c> trailer. When this is seen the
    /// client must block entry and prompt the player to update — never retry.
    /// </summary>
    public static class ClientVersionGate
    {
        public const string RequiredActionHeader = "required-action";
        public const string UpgradeClientValue = "upgrade-client";

        public static bool IsUpgradeRequired(Exception exception)
        {
            for (var e = exception; e is not null; e = e.InnerException)
            {
                if (e is RpcException rpc && IsUpgradeRequired(rpc))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsUpgradeRequired(RpcException rpc)
        {
            // The gateway uses FailedPrecondition exclusively for version mismatch.
            if (rpc.StatusCode == StatusCode.FailedPrecondition)
            {
                return true;
            }

            var action = rpc.Trailers?.GetValue(RequiredActionHeader);
            return string.Equals(action, UpgradeClientValue, StringComparison.OrdinalIgnoreCase);
        }
    }
}
