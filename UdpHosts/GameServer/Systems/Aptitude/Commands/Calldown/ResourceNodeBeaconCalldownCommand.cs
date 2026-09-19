using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Calldown;

public class ResourceNodeBeaconCalldownCommand : Command, ICommand
{
    /// <summary>
    ///     The <c>dbzonemetadata::ResourceNodeType</c> id of the vein a player thumper
    ///     mines: 20 = "Default, Thumper Sifted Earth - Resource Vein 0". No column of
    ///     <c>aptfs::ResourceNodeBeaconCalldownCommandDef</c> or
    ///     <c>dbitems::ResourceNodeBeacon</c> carries a node type, so the calldown always
    ///     spawns the default player vein - zone thumpers use the encounter tables instead.
    /// </summary>
    private const uint PlayerThumperNodeType = 20;

    private ResourceNodeBeaconCalldownCommandDef Params;

    public ResourceNodeBeaconCalldownCommand(ResourceNodeBeaconCalldownCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var caller = context.Self;
        var request = context.Abilities.TryConsumeResourceNodeBeaconCalldownRequest(caller.EntityId);
        if (request != null)
        {
            var encounterMan = context.Shard.EncounterMan;
            var position = request.Position;
            encounterMan.CreateThumper(PlayerThumperNodeType, position, caller as CharacterEntity, Params);
            return true;
        }
        else
        {
            return false;
        }
    }
}