using System;
using System.IO;
using Shared.Common.Accounts;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     Tests for the create-account rules (<see cref="AccountCreation"/>) and for
///     the flow they gate: the creation request the shipped client sends has to
///     end up as an account the client can log in to.
///
///     Regression: the client posts no <c>confirm_email</c>/<c>confirm_password</c>
///     (it validates its own confirmation boxes), so requiring those fields made
///     every creation fail with <c>ERR_EMAIL_MISMATCH</c> (HTTP 500) before an
///     account was stored — the client's automatic post-create login then kept
///     failing with <c>ERR_INCORRECT_USERPASS</c> and the game froze on "Create".
/// </summary>
public class AccountCreationTests : IDisposable
{
    private readonly string storePath;

    public AccountCreationTests()
    {
        storePath = Path.Combine(Path.GetTempPath(), $"pin-accounts-{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        try
        {
            File.Delete(storePath);
        }
        catch (IOException)
        {
            // Temp cleanup is best effort.
        }
    }

    private AccountStore FreshStore()
    {
        return new AccountStore(storePath);
    }

    private static AccountCreationRequest ClientRequest(string email = "player@example.com", string password = "hunter2")
    {
        // Exactly the fields the client's creation form posts.
        return new AccountCreationRequest
               {
                   Email = email,
                   Password = password,
                   Country = "UA",
                   Birthday = "1990-01-01",
                   EmailOptIn = false,
                   ReferralKey = string.Empty
               };
    }

    [Fact]
    public void Parse_ReadsThePayloadTheShippedClientSends()
    {
        const string Body = """
                            {
                              "referral_key": "",
                              "email": "player@example.com",
                              "email_optin": false,
                              "password": "hunter2",
                              "country": "US",
                              "birthday": "1990-01-01",
                              "steam_session_ticket": null,
                              "steam_user_id": null,
                              "steam_cdkey": null
                            }
                            """;

        var request = AccountCreation.Parse(Body);

        Assert.NotNull(request);
        Assert.Equal("player@example.com", request.Email);
        Assert.Equal("hunter2", request.Password);
        Assert.Equal("US", request.Country);
        Assert.Equal("1990-01-01", request.Birthday);
        Assert.False(request.EmailOptIn);

        // No confirmation fields in the client's request.
        Assert.Null(request.ConfirmEmail);
        Assert.Null(request.ConfirmPassword);
    }

    [Fact]
    public void Parse_ReturnsNullInsteadOfThrowingOnUnusableBodies()
    {
        Assert.Null(AccountCreation.Parse(null));
        Assert.Null(AccountCreation.Parse(string.Empty));
        Assert.Null(AccountCreation.Parse("   "));
        Assert.Null(AccountCreation.Parse("not json at all"));
    }

    [Fact]
    public void Validate_AcceptsThePayloadTheShippedClientSends()
    {
        Assert.True(AccountCreation.Validate(ClientRequest(), out var errorCode, out var errorMessage));
        Assert.Null(errorCode);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void Validate_AcceptsEmptyConfirmationFieldsLikeMissingOnes()
    {
        var request = ClientRequest();
        request.ConfirmEmail = string.Empty;
        request.ConfirmPassword = "   ";

        Assert.True(AccountCreation.Validate(request, out _, out _));
    }

    [Fact]
    public void Validate_HonoursConfirmationFieldsWhenACallerSendsThem()
    {
        var request = ClientRequest();
        request.ConfirmEmail = "PLAYER@example.com";
        request.ConfirmPassword = "hunter2";
        Assert.True(AccountCreation.Validate(request, out _, out _));

        request.ConfirmEmail = "other@example.com";
        Assert.False(AccountCreation.Validate(request, out var errorCode, out _));
        Assert.Equal(AccountErrors.ErrEmailMismatch, errorCode);

        request.ConfirmEmail = "player@example.com";
        request.ConfirmPassword = "hunter3";
        Assert.False(AccountCreation.Validate(request, out errorCode, out _));
        Assert.Equal(AccountErrors.ErrPasswordMismatch, errorCode);
    }

    [Fact]
    public void Validate_RejectsMissingRequestEmailOrPassword()
    {
        Assert.False(AccountCreation.Validate((AccountCreationRequest)null, out var errorCode, out _));
        Assert.Equal(AccountErrors.ErrUnknown, errorCode);

        var noEmail = ClientRequest(email: "   ");
        Assert.False(AccountCreation.Validate(noEmail, out errorCode, out _));
        Assert.Equal(AccountErrors.ErrNoEmail, errorCode);

        var noPassword = ClientRequest(password: null);
        Assert.False(AccountCreation.Validate(noPassword, out errorCode, out _));
        Assert.Equal(AccountErrors.ErrPasswordMismatch, errorCode);
    }

    [Fact]
    public void Validate_ComparesEmailsCaseInsensitivelyAndPasswordsVerbatim()
    {
        Assert.True(AccountCreation.Validate("Player@Example.com", "player@example.com ", "hunter2", "hunter2", out _, out _));

        Assert.False(AccountCreation.Validate("player@example.com", "player@example.com", "hunter2", "Hunter2", out var errorCode, out _));
        Assert.Equal(AccountErrors.ErrPasswordMismatch, errorCode);
    }

    [Fact]
    public void ClientShapedCreation_ThenClientLogin_Verifies()
    {
        var store = FreshStore();

        // What the endpoint does with the client's request: validate it, then
        // store the account (the confirmation fields stay null).
        var request = ClientRequest("Player@Example.com");
        Assert.True(AccountCreation.Validate(request, out _, out _));
        Assert.True(store.TryCreate(request.Email, request.Password, request.Country, request.Birthday, request.EmailOptIn, request.ReferralKey,
                                    out var account, out var errorCode, out _));
        Assert.Null(errorCode);

        // ...and what the client does next: sign a login request with the uid and
        // secret it derives from the very same email/password.
        var uid = Uri.EscapeDataString(Red5Auth.GenerateUserId("Player@Example.com"));
        var headerData = $"ver=2&tc=1610833076&nonce=3e691feb538a38a2&uid={uid}&host=clientapi&path=%2Fapi%2Fv2%2Faccounts%2Flogin&hbody=da39a3ee5e6b4b0d3255bfef95601890afd80709&cid=0";
        var secret = Red5Auth.GenerateSecret("Player@Example.com", "hunter2");
        var header = $"Red5 {Red5Auth.GenerateToken(secret, headerData)} {headerData}";

        var loggedIn = store.VerifyLogin(header);
        Assert.NotNull(loggedIn);
        Assert.Equal(account.AccountId, loggedIn.AccountId);
        Assert.Equal("player@example.com", loggedIn.Email);
    }
}
