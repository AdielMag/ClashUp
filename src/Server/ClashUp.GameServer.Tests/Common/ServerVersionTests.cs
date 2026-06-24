using ClashUp.Server.Common;
using FluentAssertions;
using Xunit;

namespace ClashUp.Server.GameServer.Tests.Common;

public class ServerVersionTests
{
    [Fact]
    public void Current_IsNonEmpty_AndHasNoSourceRevisionSuffix()
    {
        ServerVersion.Current.Should().NotBeNullOrWhiteSpace();
        ServerVersion.Current.Should().NotContain("+", "the SDK source-revision suffix is stripped");
    }
}
