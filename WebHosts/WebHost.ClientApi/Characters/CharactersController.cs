using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Shared.Common.Accounts;
using Shared.Common.Characters;
using WebHost.ClientApi.Accounts;
using WebHost.ClientApi.Accounts.Models;
using WebHost.ClientApi.Characters.Models;

namespace WebHost.ClientApi.Characters;

[ApiController]
public class CharactersController : ControllerBase
{
    private readonly ICharactersRepository _charactersRepository;
    private readonly ILogger<CharactersController> _logger;

    public CharactersController(ICharactersRepository charactersRepository, ILogger<CharactersController> logger)
    {
        _charactersRepository = charactersRepository;
        _logger = logger;
    }

    /// <summary>
    /// The character selection list of the account that signed the request
    /// (identified via its X-Red5-Signature, same as the login). A fresh
    /// account's list is empty by design — the client then walks the player
    /// through character creation. Unauthenticated callers keep seeing the
    /// built-in admin account's entries (its zone picker), which is what
    /// everyone got before the account system existed.
    /// </summary>
    [Route("api/v2/characters/list")]
    [HttpGet]
    public CharactersList GetCharactersList()
    {
        var account = HttpContext.TryGetRed5Account()
                      ?? AccountStore.Default.Get(AccountStore.AdminAccountId)
                      ?? AccountStore.Default.GetAll().FirstOrDefault();

        return _charactersRepository.GetCharacters(account?.AccountId ?? AccountStore.AdminAccountId);
    }

    [Route("api/v1/characters/{characterId}/data")]
    [HttpGet]
    [Produces("application/json")]
    public object CharacterData(string characterId)
    {
        if (string.IsNullOrEmpty(characterId))
        {
            return new { };
        }

        var data = new CharacterData
                   {
                       Key = "76336_0",        // ToDo: Game is pushing this through QueryStrings, why?
                       Namespace = "bfAbiMap", // ToDo: Game is pushing this through QueryStrings, why?
                       Value = "0,1,2,3"       // ToDo: What are these?
                   };

        return data;
    }

    [Route("api/v3/characters/{characterId:ulong}/inventories/bag")]
    [HttpGet]
    [Produces("application/json")]
    public object InventoriesBag(string characterId)
    {
        if (string.IsNullOrEmpty(characterId))
        {
            return new { };
        }

        var bag = new InventoriesBag
                  {
                      Items = new object[]
                              {
                                  new Items
                                  {
                                      ItemId = 1536337344062485245,
                                      ItemSdbId = 84238,
                                      OwnerGuid = ulong.Parse(characterId),
                                      TypeCode = 248,
                                      Quality = 0,
                                      CharacterGuid = ulong.Parse(characterId),
                                      BoundToOwner = false,
                                      CreatedAt = "2013-09-07T05:10:11+00:00",
                                      UpdatedAt = "2013-09-07T05:10:11+00:00"
                                  },
                                  new Items
                                  {
                                      ItemId = 2256965954967900669,
                                      ItemSdbId = 79789,
                                      OwnerGuid = ulong.Parse(characterId),
                                      TypeCode = 248,
                                      Quality = 0,
                                      CharacterGuid = ulong.Parse(characterId),
                                      BoundToOwner = false,
                                      CreatedAt = "2013-09-07T05:10:11+00:00",
                                      UpdatedAt = "2013-09-07T05:10:11+00:00",
                                      CreatorGuid = ulong.Parse(characterId)
                                  },
                                  new Items
                                  {
                                      ItemId = 8885818829252295677,
                                      ItemSdbId = 30346,
                                      OwnerGuid = ulong.Parse(characterId),
                                      TypeCode = 248,
                                      Quality = 685,
                                      CharacterGuid = ulong.Parse(characterId),
                                      BoundToOwner = false,
                                      CreatedAt = "2013-09-07T05:10:11+00:00",
                                      UpdatedAt = "2013-09-07T05:10:11+00:00",
                                      CreatorGuid = ulong.Parse(characterId)
                                  }
                              },
                      Resources = new object[]
                                  {
                                      new Resources { ItemSdbId = 78007, OwnerGuid = ulong.Parse(characterId), ResourceType = "0", Quantity = 1 },
                                      new Resources { ItemSdbId = 75269, OwnerGuid = ulong.Parse(characterId), ResourceType = "0", Quantity = 1 },
                                      new Resources { ItemSdbId = 77343, OwnerGuid = ulong.Parse(characterId), ResourceType = "0", Quantity = 1 },
                                      new Resources { ItemSdbId = 77344, OwnerGuid = ulong.Parse(characterId), ResourceType = "0", Quantity = 1 }
                                  }
                  };

        return bag;
    }

    [Route("api/v3/characters/{characterId:ulong}/inventories/gear/items")]
    [HttpGet]
    [Produces("application/json")]
    public object InventoriesGearItems(string characterId)
    {
        if (string.IsNullOrEmpty(characterId))
        {
            return new { };
        }

        var temp = new object[]
                   {
                       new Items
                       {
                           ItemId = 815797474160817405,
                           ItemSdbId = 83945,
                           OwnerGuid = ulong.Parse(characterId),
                           TypeCode = 244,
                           Quality = 885,
                           CharacterGuid = ulong.Parse(characterId),
                           BoundToOwner = false,
                           CreatedAt = "2013-09-07T05:10:11+00:00",
                           UpdatedAt = "2013-09-07T05:10:11+00:00",
                           Durability = new Durability { Current = 1000, Pool = 0 },
                           AttributeModifiers = new Dictionary<uint, double> { { 950, 0.0 }, { 951, -174.84620344827584 }, { 952, -42.6656 }, { 1072, 125.0 } }
                       },
                       new Items
                       {
                           ItemId = 815797474160966141,
                           ItemSdbId = 82924,
                           OwnerGuid = ulong.Parse(characterId),
                           TypeCode = 244,
                           Quality = 159,
                           CharacterGuid = ulong.Parse(characterId),
                           BoundToOwner = false,
                           CreatedAt = "2013-09-07T05:10:11+00:00",
                           UpdatedAt = "2013-09-07T05:10:11+00:00",
                           Durability = new Durability { Current = 1000, Pool = 0 },
                           AttributeModifiers = new Dictionary<uint, double>
                                                {
                                                    { 23, 2.4827167 },
                                                    { 952, -42.45361210150184 },
                                                    { 950, 0.0 },
                                                    { 951, -63.9984 },
                                                    { 956, 6.0 },
                                                    { 954, 153.0 }
                                                }
                       }
                   };
        return temp;
    }

    /// <summary>
    /// Character name check for the creation form. Mirrors the original
    /// service's rules (length bounds, letters/digits/spaces only, no leading
    /// digit, name not taken) and reports each violated rule with the original
    /// client error codes in <c>reason</c>.
    /// </summary>
    [Route("api/v1/characters/validate_name")]
    [HttpPost]
    [Produces("application/json")]
    public object ValidateCharacterName([FromBody] CharacterName characterName)
    {
        // Real characters reserve their names across all accounts; the built-in
        // zone-picker seed entries do not.
        var takenNames = CharacterStore.GetAll()
                                       .Where(c => !CharacterCreation.IsZoneSeedEntry(c))
                                       .Select(c => c.Name);

        var reasons = CharacterCreation.ValidateName(characterName?.Name, takenNames);

        return new ValidateNameResponse
               {
                   Name = characterName?.Name,
                   Valid = reasons.Count == 0,
                   Code = reasons.Count == 0 ? string.Empty : AccountErrors.ErrNameInvalid,
                   Reason = reasons
               };
    }

    /// <summary>
    /// Character creation (<c>POST api/v1/characters</c>). The request carries
    /// the name, starting battleframe and the head/voice/color choices of the
    /// creation screen; the created character takes the account's next free
    /// slot in the spawn zone (New Eden) — the first one replaces the admin
    /// account's untouched "New Eden" zone-picker entry, further ones get their
    /// own guids — and shows up in the character list the client re-fetches
    /// afterwards. The account is taken from the request's X-Red5-Signature,
    /// exactly like the original service.
    /// </summary>
    [Route("api/v1/characters")]
    [HttpPost]
    [Produces("application/json")]
    public IActionResult CreateCharacter([FromBody] CharacterCreate characterCreateData)
    {
        if (characterCreateData == null)
        {
            return Error(AccountErrors.ErrUnknown, "No character data received");
        }

        var account = HttpContext.TryGetRed5Account();
        if (account == null)
        {
            return Error(AccountErrors.ErrIncorrectUserPass, "Login failed, check your username and password");
        }

        // The client only offers the create flow while the account is under its
        // slot limit; enforce the same limit server-side for hand-crafted
        // requests (the admin account's zone-picker entries count against it).
        if (CharacterStore.GetAll(account.AccountId).Count >= account.CharacterLimit)
        {
            return Error(AccountErrors.ErrDuplicateCharacter, "This account has no free character slot");
        }

        var gender = string.Equals(characterCreateData.Gender, "female", StringComparison.OrdinalIgnoreCase) ? 1u : 0u;

        if (!CharacterStore.TryCreateCharacter(
                account.AccountId,
                characterCreateData.Name,
                gender,
                (uint)characterCreateData.StartClassId,
                (uint)characterCreateData.Head,
                (uint)characterCreateData.VoiceSet,
                (uint)characterCreateData.SkinColorId,
                (uint)characterCreateData.EyeColorId,
                (uint)characterCreateData.HairColorId,
                (uint)characterCreateData.HeadAccessoryA,
                out var character,
                out var errorCode,
                out var errorMessage))
        {
            return Error(errorCode, errorMessage);
        }

        _logger.LogWarning(
            "Created character {CharacterGuid} ({Name}) for account {AccountId}: frame {FrameId}, gender {Gender}; the client will now refresh api/v2/characters/list",
            character.CharacterGuid,
            character.Name,
            account.AccountId,
            character.CurrentBattleframeSDBId,
            gender == 1 ? "female" : "male");

        // Keep this response byte-for-byte compatible with the service the
        // client was built against. In particular, account_id and
        // character_guid intentionally remain zero: the original creation
        // endpoint did not fill them in and the client obtains the real guid
        // from the character-list refresh. Returning PIN's unsigned 0xaa...
        // guid here as a signed long produced a negative id, which the client
        // tried to use during the transition and could crash before rendering
        // the refreshed character.
        return Ok(new CreateCharacterResponse
                  {
                      CreatedAt = character.CreatedAt,
                      UpdatedAt = character.CreatedAt,
                      HeadAccAId = characterCreateData.HeadAccessoryA,
                      HeadAccBId = characterCreateData.HeadAccessoryB,
                      HeadMainId = characterCreateData.Head,
                      IsActive = true,
                      IsDev = characterCreateData.IsDev,
                      MaxFrameLevel = 0,
                      Name = character.Name,
                      NeedsNameChange = false,
                      PoolId = 0,
                      Race = 0,
                      TimePlayedSecs = 0,
                      TitleId = 0,
                      UniqueName = character.Name.ToUpperInvariant(),
                      VoiceSetId = characterCreateData.VoiceSet,
                      Gender = gender == 1 ? "female" : "male"
                  });
    }

    private ObjectResult Error(string code, string message)
    {
        var result = new ObjectResult(new ApiError { Code = code, Message = message })
                     {
                         StatusCode = 500
                     };
        return result;
    }
}