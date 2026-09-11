using System.Collections.Generic;
using Shared.Common.Characters;
using WebHost.ClientApi.Characters.Models;

namespace WebHost.ClientApi.Characters;

public interface ICharactersRepository
{
    CharactersList GetCharacters(ulong accountId);

    IReadOnlyList<PlayerVisualLoadout> GetVisualLoadouts(ulong characterGuid);
}
