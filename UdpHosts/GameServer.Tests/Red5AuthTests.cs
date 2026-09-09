using System;
using Shared.Common.Accounts;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     Tests for the Red5 request-signature scheme (<see cref="Red5Auth"/>).
///
///     The test vectors are real: the uid/secret values are the ones the original
///     client produces for test@mail.com/password (they appear as the known
///     account in FauFau's AuthTests, the community's reverse engineering of the
///     client scheme), and the signature header is a byte-exact client capture
///     that verifies against that secret. If any of these tests fail, the login
///     of every real client against PIN would fail.
/// </summary>
public class Red5AuthTests
{
    private const string TestEmail = "test@mail.com";
    private const string TestPassword = "password";

    // The secret of the captured account (FauFau AuthTests' known account).
    private const string TestSecretV2 = "36e3788836c2c0c2335d6e7b96220fe1fac1a898";

    private const string TestSecretV1 = "575c232a4939112747ef5480378be3f39f6ff547";

    // A full, byte-exact X-Red5-Signature header captured from the original
    // client (FauFau AuthTests' verification vector; the uid belongs to the same
    // account as TestSecretV2).
    private const string CapturedHeader =
        "Red5 f835fb24da4592d417a93e43868939d8688a87e2 ver=2&tc=1610833076&nonce=3e691feb538a38a2&uid=Qth4CwFkTyixv3NPM6V8RL4BByY%3D&host=oracleweb-testserver.nyaasync.net&path=%2Fclientapi%2Fapi%2Fv1%2Flogin_alerts&hbody=da39a3ee5e6b4b0d3255bfef95601890afd80709&cid=0";

    // The real login request captured from the beta-1869 client against
    // clientapi-v01-ew1.firefallthegame.com (2015-05-02): header data with a
    // trailing token2, as every in-game request carries it.
    private const string CapturedLoginHeader =
        "Red5 1b43fce7fb75b35c624ea1afaecd28f09575c0e5 ver=2&tc=1430591697&nonce=16de52646fe70a8a&uid=6U6pWcghAmrmwrR%2BRivHP7bObWs%3D&host=clientapi-v01-ew1.firefallthegame.com&path=%2Fapi%2Fv2%2Faccounts%2Flogin&hbody=da39a3ee5e6b4b0d3255bfef95601890afd80709&cid=0 625298301962e76315045df94dc1689d9beb3d6d";

    [Fact]
    public void GenerateUserId_MatchesOriginalClient()
    {
        Assert.Equal("Qth4CwFkTyixv3NPM6V8RL4BByY=", Red5Auth.GenerateUserId(TestEmail));
    }

    [Fact]
    public void GenerateUserId_IsCaseInsensitiveLikeTheClient()
    {
        // The client folds ASCII A-Z before hashing; mixed case must not change the uid.
        Assert.Equal(Red5Auth.GenerateUserId(TestEmail), Red5Auth.GenerateUserId("TEST@MAIL.COM"));
    }

    [Fact]
    public void GenerateUserId_AdminAccount()
    {
        Assert.Equal("DbrG3K3fyUO2EAhdmfSAe4P1ZTM=", Red5Auth.GenerateUserId(AccountStore.AdminEmail));
    }

    [Fact]
    public void GenerateSecret_V1_MatchesOriginalClient()
    {
        Assert.Equal(TestSecretV1, Red5Auth.GenerateSecret(TestEmail, TestPassword, v2: false));
    }

    [Fact]
    public void GenerateSecret_V2_MatchesOriginalClient()
    {
        // The shipped client uses the 200-round derivation.
        Assert.Equal(TestSecretV2, Red5Auth.GenerateSecret(TestEmail, TestPassword));
    }

    [Fact]
    public void GenerateSecret_AdminAccount()
    {
        Assert.Equal("5cf5b41968c00b17daf1a81a709e41a1d9bf0c55", Red5Auth.GenerateSecret(AccountStore.AdminEmail, AccountStore.AdminPassword));
    }

    [Fact]
    public void GenerateSecret_PasswordIsCaseSensitive()
    {
        Assert.NotEqual(Red5Auth.GenerateSecret(TestEmail, "Password"), Red5Auth.GenerateSecret(TestEmail, "password"));
    }

    [Fact]
    public void GenerateToken_MatchesOriginalClient()
    {
        var headerData = CapturedHeader.Substring("Red5 ".Length + Red5Auth.SecretLength + 1);
        Assert.Equal("f835fb24da4592d417a93e43868939d8688a87e2", Red5Auth.GenerateToken(TestSecretV2, headerData));
    }

    [Fact]
    public void Verify_AcceptsOriginalClientSignature()
    {
        Assert.True(Red5Auth.Verify(TestSecretV2, CapturedHeader));
    }

    [Fact]
    public void Verify_RejectsWrongSecret()
    {
        Assert.False(Red5Auth.Verify("0000000000000000000000000000000000000000", CapturedHeader));
    }

    [Fact]
    public void Verify_RejectsTamperedHeader()
    {
        // Flip one character of the signed data (the time code).
        var tampered = CapturedHeader.Replace("tc=1610833076", "tc=1610833077");
        Assert.False(Red5Auth.Verify(TestSecretV2, tampered));
    }

    [Fact]
    public void Verify_RejectsMalformedHeaders()
    {
        Assert.False(Red5Auth.Verify(TestSecretV2, null));
        Assert.False(Red5Auth.Verify(TestSecretV2, string.Empty));
        Assert.False(Red5Auth.Verify(TestSecretV2, "Red5"));
        Assert.False(Red5Auth.Verify(TestSecretV2, "Red5 short ver=2"));
        Assert.False(Red5Auth.Verify(TestSecretV2, "Bearer f835fb24da4592d417a93e43868939d8688a87e2 ver=2&tc=1"));
        Assert.False(Red5Auth.Verify(TestSecretV2, CapturedLoginHeader)); // different (unknown) account/secret
    }

    [Fact]
    public void Verify_AcceptsSignatureWithTrailingToken2()
    {
        // Every in-game request carries a trailing token2; it is part of the
        // signed data. Build one for the known account and verify it.
        var headerData = "ver=2&tc=1610833076&nonce=3e691feb538a38a2&uid=Qth4CwFkTyixv3NPM6V8RL4BByY%3D&host=oracleweb-testserver.nyaasync.net&path=%2Fclientapi%2Fapi%2Fv1%2Flogin_alerts&hbody=da39a3ee5e6b4b0d3255bfef95601890afd80709&cid=0 625298301962e76315045df94dc1689d9beb3d6d";
        var token = Red5Auth.GenerateToken(TestSecretV2, headerData);
        var header = $"Red5 {token} {headerData}";

        Assert.True(Red5Auth.Verify(TestSecretV2, header));
    }

    [Fact]
    public void TryParse_ReadsOriginalClientHeader()
    {
        Assert.True(Red5Signature.TryParse(CapturedLoginHeader, out var signature));

        // The uid is URL-encoded in the header and must come back decoded.
        Assert.Equal("6U6pWcghAmrmwrR+RivHP7bObWs=", signature.Uid);
        Assert.Equal(2, signature.Version);
        Assert.Equal(1430591697u, signature.TimeCode);
        Assert.Equal(0u, signature.ClientId);
        Assert.Equal("16de52646fe70a8a", signature.Nonce);
        Assert.Equal("clientapi-v01-ew1.firefallthegame.com", signature.Host);
        Assert.Equal("/api/v2/accounts/login", signature.Path);
        Assert.Equal("da39a3ee5e6b4b0d3255bfef95601890afd80709", signature.BodyHash);
        Assert.Equal("1b43fce7fb75b35c624ea1afaecd28f09575c0e5", signature.Token);
        Assert.Equal("625298301962e76315045df94dc1689d9beb3d6d", signature.Token2);

        // The signed data is everything after "Red5 <token> ".
        Assert.StartsWith("ver=2&tc=1430591697", signature.HeaderData);
        Assert.EndsWith("625298301962e76315045df94dc1689d9beb3d6d", signature.HeaderData);
    }

    [Fact]
    public void TryParse_RejectsMalformedHeaders()
    {
        Assert.False(Red5Signature.TryParse(null, out _));
        Assert.False(Red5Signature.TryParse(string.Empty, out _));
        Assert.False(Red5Signature.TryParse("garbage", out _));
        Assert.False(Red5Signature.TryParse("Red5 token-without-query", out _));

        // A signature without any uid cannot identify an account.
        Assert.False(Red5Signature.TryParse(
            "Red5 1b43fce7fb75b35c624ea1afaecd28f09575c0e5 ver=2&tc=1430591697&nonce=16de52646fe70a8a&cid=0 625298301962e76315045df94dc1689d9beb3d6d",
            out _));
    }

    [Fact]
    public void LowercaseAsciiBytes_OnlyFoldsAsciiUppercase()
    {
        // Bytes outside A-Z (including the multi-byte UTF-8 sequence for 'Ü')
        // must stay untouched, matching the client's byte-wise folding.
        var lowered = Red5Auth.LowercaseAsciiBytes("ABCxyzÜ1");
        Assert.Equal(new byte[] { 97, 98, 99, 120, 121, 122, 0xC3, 0x9C, 0x31 }, lowered);
    }
}
