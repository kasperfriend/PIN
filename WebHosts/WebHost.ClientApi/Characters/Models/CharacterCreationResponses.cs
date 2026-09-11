using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WebHost.ClientApi.Characters.Models;

/// <summary>
/// Response of <c>POST api/v1/characters/validate_name</c>: the checked name,
/// whether it is valid, and the list of violated rules as the original client
/// error codes (<c>ERR_NAME_TOO_SHORT</c>, <c>ERR_NAME_IN_USE</c>, ...) with
/// the umbrella code <c>ERR_NAME_INVALID</c> when invalid.
/// </summary>
public class ValidateNameResponse
{
    public string Name { get; set; }

    public bool Valid { get; set; }

    public string Code { get; set; } = string.Empty;

    public List<string> Reason { get; set; } = new();
}

/// <summary>
/// Response of <c>POST api/v1/characters</c>: the created character as the
/// client expects it (shape of the original service's response, which left
/// <c>character_guid</c> at 0 — the client re-fetches the character list after
/// creation, so the guid is a bonus, not a contract).
/// </summary>
public class CreateCharacterResponse
{
    public long AccountId { get; set; }

    public long CharacterGuid { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime DeletedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    // The original service used mixed-case names for these fields (kept
    // byte-identical via JsonPropertyName; the snake_case policy would mangle
    // them into head_acc_aid / head_main_id).
    [JsonPropertyName("head_accAId")]
    public int HeadAccAId { get; set; }

    [JsonPropertyName("head_accBId")]
    public int HeadAccBId { get; set; }

    [JsonPropertyName("head_mainId")]
    public int HeadMainId { get; set; }

    public int Id { get; set; }

    public bool IsActive { get; set; }

    public bool IsDev { get; set; }

    public DateTime LastSeenAt { get; set; }

    public long LoadoutId { get; set; }

    public int MaxFrameLevel { get; set; }

    public string Name { get; set; }

    public bool NeedsNameChange { get; set; }

    public int PoolId { get; set; }

    public short Race { get; set; }

    public long TimePlayedSecs { get; set; }

    public int TitleId { get; set; }

    public string UniqueName { get; set; }

    [JsonPropertyName("voice_setId")]
    public int VoiceSetId { get; set; }

    public string Xdata { get; set; }

    public string Gender { get; set; }
}
