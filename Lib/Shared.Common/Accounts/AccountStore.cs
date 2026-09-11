using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace Shared.Common.Accounts;

/// <summary>
/// File-backed store of player accounts (<c>accounts.json</c>), following the
/// same deliberate design as <see cref="Shared.Common.Characters.CharacterStore"/>:
/// a plain JSON file next to the WebHostManager binary, atomic writes, no
/// external database.
///
/// On first run the store seeds the built-in <c>admin</c>/<c>admin</c> account
/// that the login screen previously let in unconditionally. New accounts are
/// created through <c>POST api/v2/accounts</c> (the client's account creation
/// form) and are stored with the Red5 uid/secret derived from their email and
/// password, so the client can log in with them immediately.
///
/// The process-wide instance is <see cref="Default"/>; tests construct their own
/// instances against temp files.
/// </summary>
public sealed class AccountStore
{
    /// <summary>Account id of the seeded admin account (matches the style of the original service's ids).</summary>
    public const ulong AdminAccountId = 26294422UL;

    /// <summary>Email of the seeded admin account.</summary>
    public const string AdminEmail = "admin";

    /// <summary>Password of the seeded admin account.</summary>
    public const string AdminPassword = "admin";

    /// <summary>Character slots per account; the seeded zone-picker list needs all of them.</summary>
    public const int DefaultCharacterLimit = 40;

    private const int PasswordSaltLength = 16;
    private const int PasswordHashLength = 32;
    private const int PasswordIterations = 10000;
    private const int MaxEmailLength = 254;

    /// <summary>created_at reported for the seeded admin (unix 1358612495, from the original service's example).</summary>
    private static readonly DateTime AdminCreatedAt = new(2013, 1, 17, 18, 21, 35, DateTimeKind.Utc);

    private static readonly object DefaultLock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static AccountStore defaultStore;

    private readonly object writeLock = new();

    private readonly ConcurrentDictionary<ulong, AccountRecord> accounts = new();

    private readonly string storePath;

    /// <summary>Create (or open) a store at <paramref name="storePath"/>. A missing or empty file seeds the admin account.</summary>
    /// <param name="storePath">Path of the JSON file; null/empty for the default location next to the binary.</param>
    public AccountStore(string storePath = null)
        : this(ResolvePath(storePath), load: true)
    {
    }

    private AccountStore(string path, bool load)
    {
        storePath = path;
        if (load)
        {
            Load();
        }
    }

    /// <summary>
    /// The process-wide store. Lazily falls back to the default file location;
    /// call <see cref="Init"/> early to pin a configured path (idempotent).
    /// </summary>
    public static AccountStore Default => defaultStore ?? CreateDefault(null);

    /// <summary>Path of the backing JSON file.</summary>
    public string StorePath => storePath;

    /// <summary>
    /// Initialize the process-wide store from a configured path. Safe to call
    /// repeatedly; only the first call has an effect, mirroring
    /// <see cref="Shared.Common.Characters.CharacterStore.Init"/>.
    /// </summary>
    public static void Init(string storePath = null)
    {
        _ = CreateDefault(storePath);
    }

    /// <summary>
    /// Hash a password for storage: base64(16 random salt bytes + 32 PBKDF2-HMACSHA256 bytes).
    /// </summary>
    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(PasswordSaltLength);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, PasswordIterations, HashAlgorithmName.SHA256, PasswordHashLength);

        var combined = new byte[PasswordSaltLength + PasswordHashLength];
        salt.AsSpan().CopyTo(combined);
        hash.AsSpan().CopyTo(combined.AsSpan(PasswordSaltLength));

        return Convert.ToBase64String(combined);
    }

    /// <summary>Verify a plaintext password against a stored <see cref="AccountRecord.PasswordHash"/>.</summary>
    public static bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
        {
            return false;
        }

        try
        {
            var combined = Convert.FromBase64String(storedHash);
            if (combined.Length != PasswordSaltLength + PasswordHashLength)
            {
                return false;
            }

            var salt = combined.AsSpan(0, PasswordSaltLength);
            var expected = combined.AsSpan(PasswordSaltLength);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, PasswordIterations, HashAlgorithmName.SHA256, PasswordHashLength);

            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Normalize an email the way uid derivation does: trim, then fold ASCII
    /// A-Z to lowercase (see <see cref="Red5Auth.LowercaseAsciiBytes"/> for why
    /// not <see cref="string.ToLowerInvariant"/>).
    /// </summary>
    public static string NormalizeEmail(string email)
    {
        if (string.IsNullOrEmpty(email))
        {
            return string.Empty;
        }

        var trimmed = email.Trim().ToCharArray();
        for (var i = 0; i < trimmed.Length; i++)
        {
            if (trimmed[i] is >= 'A' and <= 'Z')
            {
                trimmed[i] = (char)(trimmed[i] + 32);
            }
        }

        return new string(trimmed);
    }

    private static AccountStore CreateDefault(string storePath)
    {
        lock (DefaultLock)
        {
            defaultStore ??= new AccountStore(ResolvePath(storePath));
            return defaultStore;
        }
    }

    private static string ResolvePath(string storePath)
    {
        return string.IsNullOrWhiteSpace(storePath)
                   ? Path.Combine(AppContext.BaseDirectory, "accounts.json")
                   : storePath;
    }

    /// <summary>Every account, ordered by id.</summary>
    public IReadOnlyList<AccountRecord> GetAll()
    {
        return accounts.Values.OrderBy(a => a.AccountId).ToList();
    }

    /// <summary>Look up an account by id, or null.</summary>
    public AccountRecord Get(ulong accountId)
    {
        return accounts.TryGetValue(accountId, out var account) ? account : null;
    }

    /// <summary>Look up an account by its Red5 uid (the value from the request signature), or null.</summary>
    public AccountRecord GetByUid(string uid)
    {
        if (string.IsNullOrEmpty(uid))
        {
            return null;
        }

        return accounts.Values.FirstOrDefault(a => string.Equals(a.Uid, uid, StringComparison.Ordinal));
    }

    /// <summary>Look up an account by email (case-insensitive), or null.</summary>
    public AccountRecord GetByEmail(string email)
    {
        var normalized = NormalizeEmail(email);
        if (normalized.Length == 0)
        {
            return null;
        }

        return accounts.Values.FirstOrDefault(a => string.Equals(a.Email, normalized, StringComparison.Ordinal));
    }

    /// <summary>
    /// Verify a raw <c>X-Red5-Signature</c> header against the stored credentials:
    /// parse it, look the account up by uid and check the signature with the
    /// account's secret. Returns the account on success, null otherwise.
    /// </summary>
    public AccountRecord VerifyLogin(string signatureHeader)
    {
        return TryVerifyLogin(signatureHeader, out var account, out _) ? account : null;
    }

    /// <summary>
    /// <see cref="VerifyLogin"/>, but reporting <em>why</em> a header was
    /// rejected — see <see cref="LoginFailure"/>. The client shows the same
    /// error for every failure, so this is what makes a rejected login
    /// diagnosable from the server log.
    /// </summary>
    public bool TryVerifyLogin(string signatureHeader, out AccountRecord account, out LoginFailure failure)
    {
        account = null;

        if (string.IsNullOrEmpty(signatureHeader))
        {
            failure = LoginFailure.MissingSignature;
            return false;
        }

        if (!Red5Signature.TryParse(signatureHeader, out var signature))
        {
            failure = LoginFailure.MalformedSignature;
            return false;
        }

        account = GetByUid(signature.Uid);
        if (account == null)
        {
            failure = LoginFailure.UnknownAccount;
            return false;
        }

        if (!Red5Auth.Verify(account.Secret, signatureHeader))
        {
            account = null;
            failure = LoginFailure.SignatureMismatch;
            return false;
        }

        failure = LoginFailure.None;
        return true;
    }

    /// <summary>
    /// Create and persist a new account. Email/password confirmation and format
    /// checks happen here so every caller gets the same rules; failures return
    /// the original client error code plus a human readable message.
    /// </summary>
    public bool TryCreate(
        string email,
        string password,
        string country,
        string birthday,
        bool emailOptIn,
        string referralKey,
        out AccountRecord account,
        out string errorCode,
        out string errorMessage)
    {
        account = null;
        errorCode = null;
        errorMessage = null;

        var normalized = NormalizeEmail(email);
        if (normalized.Length == 0)
        {
            errorCode = AccountErrors.ErrNoEmail;
            errorMessage = "An email address is required";
            return false;
        }

        if (normalized.Length > MaxEmailLength || normalized.Any(char.IsWhiteSpace))
        {
            errorCode = AccountErrors.ErrUnknown;
            errorMessage = "That email address is not valid";
            return false;
        }

        if (string.IsNullOrEmpty(password))
        {
            errorCode = AccountErrors.ErrPasswordMismatch;
            errorMessage = "A password is required";
            return false;
        }

        lock (writeLock)
        {
            if (GetByEmail(normalized) != null)
            {
                errorCode = AccountErrors.ErrAccountExists;
                errorMessage = "An account with this email already exists";
                return false;
            }

            var uid = Red5Auth.GenerateUserId(normalized);
            if (GetByUid(uid) != null)
            {
                // Not expected (uid is a hash of the email), but a hand-edited
                // file could contain anything.
                errorCode = AccountErrors.ErrAccountExists;
                errorMessage = "An account with this email already exists";
                return false;
            }

            var accountId = NextAccountId();
            account = new AccountRecord
            {
                AccountId = accountId,
                Email = normalized,
                Uid = uid,
                Secret = Red5Auth.GenerateSecret(normalized, password),
                PasswordHash = HashPassword(password),
                Country = string.IsNullOrWhiteSpace(country) ? null : country.Trim(),
                Birthday = string.IsNullOrWhiteSpace(birthday) ? null : birthday.Trim(),
                EmailOptIn = emailOptIn,
                ReferralKey = string.IsNullOrWhiteSpace(referralKey) ? null : referralKey.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            accounts[accountId] = account;
            Save();
        }

        return true;
    }

    /// <summary>Record a successful login and persist it.</summary>
    public void RecordLogin(AccountRecord account)
    {
        ArgumentNullException.ThrowIfNull(account);
        account.LastLoginAt = DateTime.UtcNow;
        Save();
    }

    /// <summary>Store the account's UI language preference. Returns false when the account is unknown.</summary>
    public bool UpdateLanguage(ulong accountId, string language)
    {
        var account = Get(accountId);
        if (account == null)
        {
            return false;
        }

        account.Language = language;
        Save();
        return true;
    }

    /// <summary>Write the store back to disk (atomic temp-file + move, best effort).</summary>
    public void Save()
    {
        lock (writeLock)
        {
            try
            {
                var directory = Path.GetDirectoryName(storePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(accounts.Values.OrderBy(a => a.AccountId).ToList(), JsonOptions);
                var tempPath = storePath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, storePath, true);
            }
            catch (Exception ex)
            {
                // Persistence is best effort; never take a server down over it.
                Log.Warning(ex, "Failed to persist the account store at {StorePath}", storePath);
            }
        }
    }

    /// <summary>Next free account id: one above the highest allocated id (never below the admin id).</summary>
    private ulong NextAccountId()
    {
        var highest = accounts.IsEmpty ? 0 : accounts.Keys.Max();
        return Math.Max(highest, AdminAccountId) + 1;
    }

    private void Load()
    {
        try
        {
            if (File.Exists(storePath))
            {
                var json = File.ReadAllText(storePath);
                var loaded = JsonSerializer.Deserialize<List<AccountRecord>>(json, JsonOptions);
                if (loaded != null)
                {
                    foreach (var account in loaded)
                    {
                        if (!string.IsNullOrEmpty(account.Uid))
                        {
                            accounts[account.AccountId] = account;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // A corrupt or partially written store must not stop the servers from
            // booting. Move it aside (it may be repairable by hand) and reseed.
            Log.Warning(ex, "Account store at {StorePath} is unreadable; it has been kept as {CorruptPath} and the store was reseeded", storePath, storePath + ".corrupt");
            try
            {
                File.Copy(storePath, storePath + ".corrupt", true);
            }
            catch (Exception)
            {
                // Best effort only; never let it break the load.
            }

            accounts.Clear();
        }

        if (accounts.IsEmpty)
        {
            SeedDefault();
        }

        // At Warning, the level the WebHostManager shows by default: when a
        // client cannot log in, the first question is which file the accounts
        // were stored in and whether the account is in it at all.
        Log.Warning(
            "Account store {StorePath} holds {AccountCount} account(s): {Emails}",
            storePath,
            accounts.Count,
            string.Join(", ", accounts.Values.OrderBy(a => a.AccountId).Select(a => a.Email)));
    }

    private void SeedDefault()
    {
        accounts[AdminAccountId] = new AccountRecord
        {
            AccountId = AdminAccountId,
            Email = AdminEmail,
            Uid = Red5Auth.GenerateUserId(AdminEmail),
            Secret = Red5Auth.GenerateSecret(AdminEmail, AdminPassword),
            PasswordHash = HashPassword(AdminPassword),
            IsAdmin = true,
            CreatedAt = AdminCreatedAt
        };

        Log.Information("Seeded the default account {Email}/{Password} (id {AccountId})", AdminEmail, AdminPassword, AdminAccountId);
        Save();
    }
}
