using System;
using System.IO;
using System.Linq;
using Shared.Common.Accounts;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     Tests for the file-backed account store (<see cref="AccountStore"/>).
///     Each test works on its own store instance in a temp file, never on the
///     process-wide <c>AccountStore.Default</c>.
/// </summary>
public class AccountStoreTests : IDisposable
{
    private readonly string storePath;

    public AccountStoreTests()
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

    [Fact]
    public void FreshStore_SeedsAdminAccount()
    {
        var store = FreshStore();

        var admin = store.Get(AccountStore.AdminAccountId);
        Assert.NotNull(admin);
        Assert.Equal(AccountStore.AdminEmail, admin.Email);
        Assert.True(admin.IsAdmin);

        // The seeded credentials must be derivable by the client scheme, so a
        // real client login of admin/admin verifies.
        Assert.Equal(Red5Auth.GenerateUserId(AccountStore.AdminEmail), admin.Uid);
        Assert.Equal(Red5Auth.GenerateSecret(AccountStore.AdminEmail, AccountStore.AdminPassword), admin.Secret);
        Assert.True(AccountStore.VerifyPassword(AccountStore.AdminPassword, admin.PasswordHash));
    }

    [Fact]
    public void VerifyLogin_AcceptsCorrectPasswordAndRejectsWrongOne()
    {
        var store = FreshStore();
        var admin = store.Get(AccountStore.AdminAccountId);

        // Build the header exactly like the client would for admin/admin.
        var secret = Red5Auth.GenerateSecret(AccountStore.AdminEmail, AccountStore.AdminPassword);
        var headerData = $"ver=2&tc=1610833076&nonce=3e691feb538a38a2&uid={Uri.EscapeDataString(admin.Uid)}&host=clientapi&path=%2Fapi%2Fv2%2Faccounts%2Flogin&hbody=da39a3ee5e6b4b0d3255bfef95601890afd80709&cid=0";
        var goodHeader = $"Red5 {Red5Auth.GenerateToken(secret, headerData)} {headerData}";

        // ...and one signed with the wrong password.
        var wrongSecret = Red5Auth.GenerateSecret(AccountStore.AdminEmail, "not-the-password");
        var badHeader = $"Red5 {Red5Auth.GenerateToken(wrongSecret, headerData)} {headerData}";

        Assert.Equal(admin.AccountId, store.VerifyLogin(goodHeader).AccountId);
        Assert.Null(store.VerifyLogin(badHeader));

        // A header signed for an unknown account is rejected too.
        var unknownUid = Uri.EscapeDataString(Red5Auth.GenerateUserId("nobody@example.com"));
        var unknownData = $"ver=2&tc=1610833076&uid={unknownUid}&cid=0";
        Assert.Null(store.VerifyLogin($"Red5 {Red5Auth.GenerateToken(secret, unknownData)} {unknownData}"));

        // And so is garbage.
        Assert.Null(store.VerifyLogin(null));
        Assert.Null(store.VerifyLogin(string.Empty));
    }

    [Fact]
    public void TryVerifyLogin_ReportsWhyALoginWasRejected()
    {
        var store = FreshStore();
        var admin = store.Get(AccountStore.AdminAccountId);

        var secret = Red5Auth.GenerateSecret(AccountStore.AdminEmail, AccountStore.AdminPassword);
        var headerData = $"ver=2&tc=1610833076&nonce=3e691feb538a38a2&uid={Uri.EscapeDataString(admin.Uid)}&host=clientapi&path=%2Fapi%2Fv2%2Faccounts%2Flogin&hbody=da39a3ee5e6b4b0d3255bfef95601890afd80709&cid=0";

        // A correct login names the account and reports no failure.
        Assert.True(store.TryVerifyLogin($"Red5 {Red5Auth.GenerateToken(secret, headerData)} {headerData}", out var account, out var failure));
        Assert.Equal(LoginFailure.None, failure);
        Assert.Equal(admin.AccountId, account.AccountId);

        // No header at all.
        Assert.False(store.TryVerifyLogin(null, out account, out failure));
        Assert.Null(account);
        Assert.Equal(LoginFailure.MissingSignature, failure);

        // A header that is not a signature.
        Assert.False(store.TryVerifyLogin("garbage", out account, out failure));
        Assert.Equal(LoginFailure.MalformedSignature, failure);

        // A signature of an account that was never created — the failure a
        // creation that never landed produces on the login that follows it.
        var unknownData = headerData.Replace(Uri.EscapeDataString(admin.Uid), Uri.EscapeDataString(Red5Auth.GenerateUserId("nobody@example.com")));
        Assert.False(store.TryVerifyLogin($"Red5 {Red5Auth.GenerateToken(secret, unknownData)} {unknownData}", out account, out failure));
        Assert.Null(account);
        Assert.Equal(LoginFailure.UnknownAccount, failure);

        // The account exists, the password does not.
        var wrongSecret = Red5Auth.GenerateSecret(AccountStore.AdminEmail, "not-the-password");
        Assert.False(store.TryVerifyLogin($"Red5 {Red5Auth.GenerateToken(wrongSecret, headerData)} {headerData}", out account, out failure));
        Assert.Null(account);
        Assert.Equal(LoginFailure.SignatureMismatch, failure);
    }

    [Fact]
    public void TryCreate_StoresAccountAndPersistsIt()
    {
        var store = FreshStore();

        var ok = store.TryCreate("Player@Example.com", "hunter2", "UA", "1990-01-01", true, null, out var account, out var errorCode, out _);
        Assert.True(ok);
        Assert.Null(errorCode);
        Assert.NotNull(account);

        // Emails are normalized (ASCII-lowercased, trimmed) and the Red5
        // material is derived from them, so the client can log in immediately.
        Assert.Equal("player@example.com", account.Email);
        Assert.Equal(Red5Auth.GenerateUserId("player@example.com"), account.Uid);
        Assert.Equal(Red5Auth.GenerateSecret("player@example.com", "hunter2"), account.Secret);
        Assert.True(AccountStore.VerifyPassword("hunter2", account.PasswordHash));
        Assert.NotEqual(AccountStore.AdminAccountId, account.AccountId);

        // Reopen the file: the account survived.
        var reloaded = FreshStore();
        var persisted = reloaded.GetByEmail("PLAYER@example.com");
        Assert.NotNull(persisted);
        Assert.Equal(account.AccountId, persisted.AccountId);
        Assert.Equal(account.Secret, persisted.Secret);
    }

    [Fact]
    public void TryCreate_RejectsDuplicateEmail()
    {
        var store = FreshStore();

        Assert.True(store.TryCreate("dupe@example.com", "one", null, null, false, null, out _, out _, out _));

        Assert.False(store.TryCreate("dupe@example.com", "two", null, null, false, null, out _, out var errorCode, out _));
        Assert.Equal(AccountErrors.ErrAccountExists, errorCode);

        // Case and surrounding whitespace do not dodge the check.
        Assert.False(store.TryCreate("  DUPE@example.com ", "two", null, null, false, null, out _, out errorCode, out _));
        Assert.Equal(AccountErrors.ErrAccountExists, errorCode);
    }

    [Fact]
    public void TryCreate_RejectsInvalidInput()
    {
        var store = FreshStore();

        Assert.False(store.TryCreate(null, "pw", null, null, false, null, out _, out var errorCode, out _));
        Assert.Equal(AccountErrors.ErrNoEmail, errorCode);

        Assert.False(store.TryCreate("   ", "pw", null, null, false, null, out _, out errorCode, out _));
        Assert.Equal(AccountErrors.ErrNoEmail, errorCode);

        Assert.False(store.TryCreate("space in@example.com", "pw", null, null, false, null, out _, out errorCode, out _));
        Assert.Equal(AccountErrors.ErrUnknown, errorCode);

        Assert.False(store.TryCreate("no-password@example.com", "", null, null, false, null, out _, out errorCode, out _));
        Assert.Equal(AccountErrors.ErrPasswordMismatch, errorCode);

        Assert.False(store.TryCreate("no-password@example.com", null, null, null, false, null, out _, out errorCode, out _));
        Assert.Equal(AccountErrors.ErrPasswordMismatch, errorCode);
    }

    [Fact]
    public void TryCreate_AssignsSequentialIds()
    {
        var store = FreshStore();

        Assert.True(store.TryCreate("a@example.com", "pw", null, null, false, null, out var first, out _, out _));
        Assert.True(store.TryCreate("b@example.com", "pw", null, null, false, null, out var second, out _, out _));

        Assert.Equal(first.AccountId + 1, second.AccountId);

        // Ids continue above the admin id after a reload.
        var reloaded = FreshStore();
        Assert.True(reloaded.TryCreate("c@example.com", "pw", null, null, false, null, out var third, out _, out _));
        Assert.Equal(second.AccountId + 1, third.AccountId);
    }

    [Fact]
    public void RecordLogin_PersistsLastLoginAt()
    {
        var store = FreshStore();
        var admin = store.Get(AccountStore.AdminAccountId);
        Assert.Null(admin.LastLoginAt);

        store.RecordLogin(admin);

        Assert.NotNull(admin.LastLoginAt);
        Assert.Equal(admin.LastLoginAt, FreshStore().Get(AccountStore.AdminAccountId).LastLoginAt);
    }

    [Fact]
    public void UpdateLanguage_PersistsPreference()
    {
        var store = FreshStore();
        Assert.Equal("en", store.Get(AccountStore.AdminAccountId).Language);

        Assert.True(store.UpdateLanguage(AccountStore.AdminAccountId, "de"));
        Assert.Equal("de", FreshStore().Get(AccountStore.AdminAccountId).Language);

        Assert.False(store.UpdateLanguage(999999, "de"));
    }

    [Fact]
    public void CorruptStoreFile_DoesNotCrashAndReseeds()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(storePath));
        File.WriteAllText(storePath, "{ this is not json");

        var store = FreshStore();
        Assert.NotNull(store.Get(AccountStore.AdminAccountId));

        // The broken file was kept for inspection.
        Assert.True(File.Exists(storePath + ".corrupt"));
    }

    [Fact]
    public void GetByUid_FindsOnlyTheMatchingAccount()
    {
        var store = FreshStore();
        Assert.True(store.TryCreate("someone@example.com", "pw", null, null, false, null, out var account, out _, out _));

        Assert.Equal(account.AccountId, store.GetByUid(account.Uid).AccountId);
        Assert.Null(store.GetByUid("not-a-uid"));
        Assert.Null(store.GetByUid(null));
    }

    [Fact]
    public void NormalizeEmail_FoldsAsciiOnly()
    {
        Assert.Equal("test@mail.com", AccountStore.NormalizeEmail(" TEST@Mail.COM "));

        // Non-ASCII uppercase is NOT folded — the client lowercases bytes, not
        // Unicode characters (see Red5Auth.LowercaseAsciiBytes).
        Assert.Equal("Ünicode@example.com", AccountStore.NormalizeEmail("Ünicode@example.com"));

        Assert.Equal(string.Empty, AccountStore.NormalizeEmail(null));
        Assert.Equal(string.Empty, AccountStore.NormalizeEmail("   "));
    }

    // ---- Opaque client login tickets (the Steam session ticket a
    // ---- Steam-launched client signs the login and account creation with).

    /// <summary>
    ///     A synthetic client ticket, laid out like the ~234-byte blob a
    ///     Steam-launched client was observed putting in the signature's
    ///     <c>uid</c>: a few length-prefixed fields with the Steam account id
    ///     embedded in front of the SteamID64 marker, twice.
    /// </summary>
    private static byte[] TicketBytes(uint steamAccountId, byte entropy)
    {
        var ticket = new byte[234];
        BitConverter.GetBytes(20).CopyTo(ticket, 0);
        ticket[4] = entropy;
        BitConverter.GetBytes(steamAccountId).CopyTo(ticket, 8);
        new byte[] { 0x01, 0x00, 0x10, 0x01 }.CopyTo(ticket, 12);
        ticket[20] = entropy;
        BitConverter.GetBytes(4).CopyTo(ticket, 0x44);
        BitConverter.GetBytes(steamAccountId).CopyTo(ticket, 0x48);
        new byte[] { 0x01, 0x00, 0x10, 0x01 }.CopyTo(ticket, 0x4C);
        ticket[100] = entropy;
        ticket[200] = entropy;
        return ticket;
    }

    private static string TicketUid(uint steamAccountId, byte entropy)
    {
        return Convert.ToBase64String(TicketBytes(steamAccountId, entropy));
    }

    private static string HeaderFor(string uid)
    {
        // The token of a ticket login is not verified (see TryVerifyLogin), it
        // only has to be 40 characters for the header to parse.
        var headerData = $"ver=2&tc=1610833076&nonce=3e691feb538a38a2&uid={Uri.EscapeDataString(uid)}&host=clientapi&path=%2Fapi%2Fv2%2Faccounts%2Flogin&hbody=da39a3ee5e6b4b0d3255bfef95601890afd80709&cid=0";
        return $"Red5 {"0123456789012345678901234567890123456789"} {headerData}";
    }

    [Fact]
    public void OpaqueTicketLogin_ProvisionsAccountForTheSteamAccount()
    {
        var store = FreshStore();

        var uid = TicketUid(0x0aef4a56, 0x2a);
        Assert.True(AccountStore.IsOpaqueTicketUid(uid));
        Assert.True(AccountStore.TryGetSteamAccountId(uid, out var steamAccountId));
        Assert.Equal(0x0110000100000000UL | 0x0aef4a56, steamAccountId);

        Assert.True(store.TryVerifyLogin(HeaderFor(uid), out var account, out var failure));
        Assert.Equal(LoginFailure.None, failure);
        Assert.NotNull(account);
        Assert.NotEqual(AccountStore.AdminAccountId, account.AccountId);
        Assert.True(account.TicketAuth);
        Assert.Equal($"steam-{steamAccountId}@pin.local", account.Email);

        // The account was persisted.
        Assert.Equal(account.AccountId, FreshStore().GetByEmail(account.Email).AccountId);
    }

    [Fact]
    public void OpaqueTicketLogin_IsStableAcrossSessions()
    {
        var store = FreshStore();

        // A new session means a new ticket around the same Steam account.
        var firstUid = TicketUid(0x0aef4a56, 0x01);
        var secondUid = TicketUid(0x0aef4a56, 0x02);
        Assert.NotEqual(firstUid, secondUid);

        Assert.True(store.TryVerifyLogin(HeaderFor(firstUid), out var first, out _));
        Assert.True(store.TryVerifyLogin(HeaderFor(secondUid), out var second, out _));

        Assert.Equal(first.AccountId, second.AccountId);
        Assert.Equal(secondUid, second.Uid);
        Assert.Equal(2, store.GetAll().Count); // admin + one provisioned account
    }

    [Fact]
    public void OpaqueTicketLogin_DoesNotVerifyTheSignature()
    {
        var store = FreshStore();
        var uid = TicketUid(0x0aef4a56, 0x2a);

        // The ticket secret is only computable with a Steam backend, so any
        // well-formed signature for the ticket uid authenticates.
        Assert.True(store.TryVerifyLogin(HeaderFor(uid), out _, out _));
        Assert.True(store.TryVerifyLogin(HeaderFor(uid).Replace("0123456789012345678901234567890123456789", "ffffffffffffffffffffffffffffffffffffffff"), out _, out _));
    }

    [Fact]
    public void OpaqueTicketLogin_DifferentSteamAccounts_GetDifferentAccounts()
    {
        var store = FreshStore();

        Assert.True(store.TryVerifyLogin(HeaderFor(TicketUid(0x0aef4a56, 0x2a)), out var first, out _));
        Assert.True(store.TryVerifyLogin(HeaderFor(TicketUid(0x00424242, 0x2a)), out var second, out _));

        Assert.NotEqual(first.AccountId, second.AccountId);
        Assert.Equal(3, store.GetAll().Count); // admin + two provisioned accounts
    }

    [Fact]
    public void OpaqueTicketWithoutEmbeddedSteamId_IsProvisionedUnderItsOwnHash()
    {
        var store = FreshStore();

        // Same shape, but the Steam account fields zeroed out.
        var ticket = TicketBytes(0, 0x2a);
        Assert.False(AccountStore.TryGetSteamAccountId(Convert.ToBase64String(ticket), out _));

        var uid = Convert.ToBase64String(ticket);
        Assert.True(store.TryVerifyLogin(HeaderFor(uid), out var account, out _));
        Assert.True(account.TicketAuth);
        Assert.StartsWith("ticket-", account.Email);

        // The same ticket again resolves to the same account.
        Assert.True(store.TryVerifyLogin(HeaderFor(uid), out var again, out _));
        Assert.Equal(account.AccountId, again.AccountId);
    }

    [Fact]
    public void DerivedUid_IsNeverTreatedAsATicket()
    {
        Assert.False(AccountStore.IsOpaqueTicketUid(Red5Auth.GenerateUserId("player@example.com")));
        Assert.False(AccountStore.IsOpaqueTicketUid("garbage"));
        Assert.False(AccountStore.IsOpaqueTicketUid(null));
        Assert.False(AccountStore.IsOpaqueTicketUid(string.Empty));
        Assert.False(AccountStore.TryGetSteamAccountId(Red5Auth.GenerateUserId("player@example.com"), out _));

        // So an unknown derived uid keeps its rejection (the "creation never
        // landed" signal), it is not silently provisioned.
        var store = FreshStore();
        Assert.False(store.TryVerifyLogin(HeaderFor(Red5Auth.GenerateUserId("nobody@example.com")), out _, out var failure));
        Assert.Equal(LoginFailure.UnknownAccount, failure);
    }

    [Fact]
    public void DescribeUid_SummarizesTicketsAndKeepsDerivedUids()
    {
        var uid = TicketUid(0x0aef4a56, 0x2a);
        var described = AccountStore.DescribeUid(uid);
        Assert.Contains("234 bytes", described);
        Assert.Contains($"Steam account {0x0110000100000000UL | 0x0aef4a56}", described);

        var derived = Red5Auth.GenerateUserId("player@example.com");
        Assert.Equal(derived, AccountStore.DescribeUid(derived));
        Assert.Equal("(none)", AccountStore.DescribeUid(null));
    }
}
