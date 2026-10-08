using GameServer.Entities.Character;
using GameServer.Entities.Deployable;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Duration;

/// <summary>
///     <c>aptfs::BattleFrameDurationCommandDef</c> (573 rows): a duration gate that keeps an effect
///     alive only while the character's battleframe still qualifies. Two independent asks, combined
///     the way the <c>Require*</c> gates combine their columns (each one that is set must hold):
///     <list type="bullet">
///         <item>
///             <c>Classtype</c> (nonzero in 151 rows) - the equipped battleframe's
///             <c>dbitems::Battleframe.Archtype</c> must equal it. The two value sets are the same
///             (0/5/6/11/13 in the command def against 0/2/5/6/7/8/9/11/13 in the battleframe table),
///             which is what identifies the column.
///         </item>
///         <item>
///             <c>Notchanged</c> (1 in 475 rows) - the character must not have changed battleframe
///             since the effect was applied, answered from
///             <see cref="CharacterEntity.LastLoadoutChangeTime" /> against the effect's own start
///             time.
///         </item>
///     </list>
///     <c>Negate</c> (1 in 39 rows) inverts the whole answer.
///     <para>
///         The command used to return <c>true</c> unconditionally, so an effect gated on "still in
///         this frame" survived a frame swap that was supposed to end it - 73 such nodes sit in the
///         stock battleframe kits alone.
///     </para>
///     <para>
///         A self that is not a character (or a character with no chassis equipped) holds the gate
///         open rather than closing it: there is no battleframe to change, and closing would expire
///         effects that nothing in the data asks to expire.
///     </para>
/// </summary>
public class BattleFrameDurationCommand : Command, ICommand
{
    private BattleFrameDurationCommandDef Params;

    public BattleFrameDurationCommand(BattleFrameDurationCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var character = context.Self as CharacterEntity ?? (context.Self as DeployableEntity)?.Owner;
        if (character == null)
        {
            return true;
        }

        uint chassisId = character.CurrentLoadout?.ChassisID ?? 0;
        if (chassisId == 0)
        {
            return true;
        }

        bool result = true;

        if (Params.Classtype != 0)
        {
            byte archtype = SDBInterface.GetBattleframe(chassisId)?.Archtype ?? (byte)0;
            result = result && archtype == Params.Classtype;
        }

        if (Params.Notchanged == 1)
        {
            // Duration chains run on the ability system's tick with their own InitTime, so the
            // effect's lifetime starts at EffectStartTime (see Context.EffectStartTime); falling
            // back to InitTime keeps a chain that has no effect context of its own comparable.
            uint effectStart = context.EffectStartTime ?? context.InitTime;
            result = result && character.LastLoadoutChangeTime <= effectStart;
        }

        if (Params.Negate == 1)
        {
            result = !result;
        }

        return result;
    }
}
