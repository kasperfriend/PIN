using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Shared.Common.Accounts;
using Shared.Web;
using WebHost.ClientApi.Accounts.Models;
using WebHost.ClientApi.Characters.Models;

namespace WebHost.ClientApi.Accounts;

[ApiController]
public class AccountsController : ControllerBase
{
    private readonly ILogger<AccountsController> _logger;

    public AccountsController(ILogger<AccountsController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Create a new account. The client's account creation form POSTs the
    /// email/password pair here; the account is stored with the Red5 uid/secret
    /// derived from the credentials, so the client can log in with them
    /// immediately. Failures return the original client error codes
    /// (<c>ERR_ACCOUNT_EXISTS</c>, <c>ERR_EMAIL_MISMATCH</c>, ...) with HTTP 500.
    /// </summary>
    /// <remarks>
    /// The shipped client sends no <c>confirm_email</c>/<c>confirm_password</c>
    /// (it validates its own confirmation boxes), so a missing confirmation is
    /// not a mismatch — see <see cref="AccountCreation"/>. The body is read by
    /// <see cref="Shared.Web.AccountCreationEndpoint"/> so that no framework
    /// error (a 415 for a content type the client picked, say) can leave the
    /// client waiting for an answer it cannot parse; a successful creation
    /// answers with the empty object the original service returned.
    /// </remarks>
    [Route("api/v2/accounts")]
    [HttpPost]
    public async Task<IActionResult> CreateAccount()
    {
        return await AccountCreationEndpoint.HandleAsync(Request, _logger);
    }

    /// <summary>
    /// Login check. The client never sends the password: it signs the request
    /// with a secret derived from email + password (the Red5 signature scheme).
    /// The account is looked up by the uid in the signature and the signature is
    /// verified against the stored secret — a wrong password means a wrong
    /// signature, so this returns the original <c>ERR_INCORRECT_USERPASS</c>
    /// error instead of letting everyone in.
    /// </summary>
    [Route("api/v2/accounts/login")]
    [HttpPost]
    public IActionResult Login()
    {
        var header = Request.Headers.TryGetValue(Red5Auth.SignatureHeaderName, out var values) && values.Count > 0
                         ? values[0]
                         : null;

        if (!AccountStore.Default.TryVerifyLogin(header, out var account, out var failure))
        {
            // Same response for unknown account and bad password, so the
            // error does not leak which of the two was wrong. The log carries
            // the difference (and at Warning, the level the WebHostManager
            // shows by default): UnknownAccount means no account with that
            // email is stored — a creation that never landed — while
            // SignatureMismatch means the account exists and the password is
            // wrong.
            _logger.LogWarning("Rejected a login: {Reason} (uid {Uid})", failure, SignedUid(header));
            return Error(AccountErrors.ErrIncorrectUserPass, "Login failed, check your username and password");
        }

        AccountStore.Default.RecordLogin(account);

        _logger.LogInformation("Account {AccountId} ({Email}) logged in", account.AccountId, account.Email);

        return Ok(new AccountStatus
                  {
                      AccountId = account.AccountId,
                      CanLogin = true,
                      IsDev = account.IsDev,
                      SteamAuthPrompt = false,
                      SkipPrecursor = false,
                      CaisStatus = new CaisStatus { Duration = 0, ExpiresAt = 0, State = "disabled" },
                      CharacterLimit = account.CharacterLimit,
                      IsVip = true,
                      VipExpiration = 0,
                      CreatedAt = new DateTimeOffset(account.CreatedAt).ToUnixTimeSeconds(),
                      Events = LoginEvents.FixedEvents()
                  });
    }

    [Route("api/v2/accounts/current/status")]
    [HttpGet]
    public object CurrentStatus()
    {
        var account = HttpContext.TryGetRed5Account();

        if (account == null)
        {
            // Unauthenticated callers (tools, probes) keep seeing the generic
            // pre-login status the endpoint always returned.
            return new CurrentStatus { IsActive = true, CanLogin = true, IsDev = false, IsBanned = false };
        }

        return new CurrentStatus { IsActive = true, CanLogin = true, IsDev = account.IsDev, IsBanned = false };
    }

    [Route("api/v2/accounts/change_language")]
    [HttpPost]
    public void ChangeLanguage([FromBody] ChangeLanguageRequest request)
    {
        if (!string.IsNullOrEmpty(request?.Language) && request.Language.Length == 2)
        {
            var account = HttpContext.TryGetRed5Account();
            if (account != null)
            {
                AccountStore.Default.UpdateLanguage(account.AccountId, request.Language.ToLowerInvariant());
            }
        }

        Ok();
    }

    [Route("api/v2/accounts/character_slots")]
    [HttpGet]
    public object CharacterSlots()
    {
        return new List<Gear>
               {
                   new() { SlotTypeId = 1, SdbId = 86969, ItemGuid = 5068916056568384765 },
                   new() { SlotTypeId = 2, SdbId = 87918, ItemGuid = 5068916056568385021 },
                   new() { SlotTypeId = 6, SdbId = 91770, ItemGuid = 5068923373180718589 },
                   new() { SlotTypeId = 116, SdbId = 126000, ItemGuid = 5068916056568385277 },
                   new() { SlotTypeId = 122, SdbId = 129359, ItemGuid = 5068916056568385533 },
                   new() { SlotTypeId = 126, SdbId = 127501, ItemGuid = 5068916056568385789 },
                   new() { SlotTypeId = 127, SdbId = 128271, ItemGuid = 5068916056568386045 },
                   new() { SlotTypeId = 128, SdbId = 126731, ItemGuid = 5068916056568386301 },
                   new() { SlotTypeId = 129, SdbId = 129067, ItemGuid = 5068916056568386557 }
               };
    }

    // Temporary location
    [Route("api/v3/characters/{characterId}/titles")]
    [HttpGet]
    public object CharacterTitles(string characterId)
    {
        if (string.IsNullOrEmpty(characterId))
        {
            return new { };
        }

        var characterTitles = new object[]
        {
            new Titles { Id = 117, Name = "Founder" },
            new Titles { Id = 128, Name = "Beta Commando" },
            new Titles { Id = 133, Name = "Beta Vanguard" },
            new Titles { Id = 135, Name = "Commander" },
            new Titles { Id = 136, Name = "Lieutenant" },
            new Titles { Id = 137, Name = "Ensign" },
            new Titles { Id = 144, Name = "Master Blaster" },
            new Titles { Id = 149, Name = "The Gun Show" },
            new Titles { Id = 150, Name = "Arc Runner" },
            new Titles { Id = 152, Name = "Pyromaniac" },
            new Titles { Id = 156, Name = "Barricade" },
            new Titles { Id = 158, Name = "Herald of Decay" },
            new Titles { Id = 171, Name = "Beta Trooper" }
        };

        return characterTitles;
    }

    [Route("api/v3/characters/{characterId:ulong}/garage_slots")]
    [HttpGet]
    [Produces("application/json")]
    public object GarageSlots(string characterId)
    {
        if (string.IsNullOrEmpty(characterId))
        {
            return new { };
        }

        var garageSlots = new ConcurrentDictionary<uint, GarageSlots>();

        var craftingStation = new GarageSlots
                              {
                                  Id = 123987212,
                                  Name = "Crafting Station",
                                  CharacterGuid = ulong.Parse(characterId),
                                  GarageType = "crafting_station",
                                  ItemGuid = 9161555162510396669,
                                  EquippedSlots = Array.Empty<Array>(),
                                  Limits = new ItemLimits { Abilities = 4 },
                                  Decals = Array.Empty<Array>(),
                                  VisualLoadoutId = 0,
                                  WarpaintId = 0,
                                  Warpaintpatterns = Array.Empty<Array>(),
                                  VisualOverrides = Array.Empty<Array>(),
                                  Unlocked = true,
                                  ExpiresInSecs = 0
                              };
        garageSlots.AddOrUpdate(craftingStation.Id, craftingStation, (k, nc) => nc);

        var firecat = new GarageSlots
                      {
                          Id = 184534131,
                          Name = "Astrek \"Firecat\"",
                          CharacterGuid = ulong.Parse(characterId),
                          GarageType = "battleframe",
                          ItemGuid = 9215052991608503805,
                          EquippedSlots = Array.Empty<Array>(),
                          Limits = new ItemLimits { Abilities = 4 },
                          Decals = new object[]
                                   {
                                       new Decals { SdbId = 10000, Color = 4294967295, Transform = new object[] { 0.05246, 0.019623, 0.0, 0.007484, -0.02002, -0.051758, 0.018127, -0.048492, 0.021362, 0.108154, -0.105469, 1.495117 } }
                                   },
                          VisualLoadoutId = 31240112,
                          WarpaintId = 77307,
                          Warpaintpatterns = new object[] { new Warpaintpatterns { SdbId = 10022, Transform = new object[] { 0.0, 16384.0, 418.0, 0.0 }, Usage = 0 } },
                          VisualOverrides = Array.Empty<Array>(),
                          Unlocked = true,
                          ExpiresInSecs = 0
                      };
        garageSlots.AddOrUpdate(firecat.Id, firecat, (k, nc) => nc);

        return garageSlots.Values;
    }

    [Route("api/v3/characters/{characterId}/garage_slots/{frameId}/perks")]
    [HttpGet]
    [Produces("application/json")]
    public object GarageSlotPerks(string characterId, string frameId)
    {
        if (string.IsNullOrEmpty(characterId) || string.IsNullOrEmpty(frameId))
        {
            return new { };
        }

        var framePerks = new GarageSlotPerks()
        {
            Perks = Array.Empty<Array>(),
            Respecs = 45,
            MaxPoints = 5
        };

        return framePerks;
    }

    // Temporary location
    [Route("api/v3/ui_actions")]
    [HttpPost]
    public void UIActions()
    {
        /*
         * POST is the following (example):
         *[
         *  {
         *    "screen_reference_id":48808253,
         *    "screen":"crafting",
         *    "action":"open"
         *  }
         *]
         *
         * What is this for? Logging?
         * Return seems to always have been empty.
         */

        Ok();
    }

    private ObjectResult Error(string code, string message)
    {
        var result = new ObjectResult(new ApiError { Code = code, Message = message })
                     {
                         StatusCode = 500
                     };
        return result;
    }

    /// <summary>The account uid a request was signed for, for the rejection log.</summary>
    private static string SignedUid(string header)
    {
        return Red5Signature.TryParse(header, out var signature) ? signature.Uid : "(none)";
    }
}
