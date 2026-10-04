using FluentAssertions;
using Stockma.Domain.ValueObjects;

namespace Stockma.Domain.Tests;

public class RefreshTokenMaterialTests
{
    [Fact]
    public void GenerateToken_Decodes_To32Bytes()
    {
        var token = RefreshTokenMaterial.GenerateToken();

        var padded = token.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - (padded.Length % 4)) % 4);

        Convert.FromBase64String(padded).Should().HaveCount(32, "ADR-018: 32 bytes de un CSPRNG");
    }

    [Fact]
    public void GenerateToken_IsBase64UrlWithoutPadding()
    {
        var token = RefreshTokenMaterial.GenerateToken();

        token.Should().NotContain("=").And.NotContain("+").And.NotContain("/");
    }

    [Fact]
    public void GenerateToken_TwoCalls_ProduceDifferentTokens()
    {
        var first = RefreshTokenMaterial.GenerateToken();
        var second = RefreshTokenMaterial.GenerateToken();

        first.Should().NotBe(second);
    }

    [Fact]
    public void Hash_IsDeterministic()
    {
        const string token = "same-token-value";

        RefreshTokenMaterial.Hash(token).Should().Be(RefreshTokenMaterial.Hash(token));
    }

    [Fact]
    public void Hash_MatchesTheKnownSha256Vector()
    {
        RefreshTokenMaterial.Hash("stockma-refresh-test-vector")
            .Should()
            .Be(
                "d10fc168b8e882e4b42c21c1fef1cbccbb0cb3497425be402987449377bfc4dd",
                "SHA-256 hex de 'stockma-refresh-test-vector'");
    }

    [Fact]
    public void Hash_IsLowercaseHex()
    {
        var hash = RefreshTokenMaterial.Hash("cualquier-token");

        hash.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Hash_RejectsBlankToken(string token)
    {
        var act = () => RefreshTokenMaterial.Hash(token);

        act.Should().Throw<ArgumentException>().WithParameterName("token");
    }
}
