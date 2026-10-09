using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Other;

/// <summary>
///     <c>aptfs::AddPhysicsCommandDef</c> (63 rows): puts a destructible body on the character —
///     the frame-borne barrier family. Its chain position says how it is used:
///     <c>AbilityToggled → AddPhysics → ParticleEffectAsset → AudioFeedback → SetAnimCtrlParam</c>, i.e.
///     a toggled ability raises it alongside its visuals and animation, and the body stands for as long
///     as the effect that carried this command does.
///     <para>
///         <c>Hitpoints</c> (combined with the register through <c>HitpointsRegop</c>) is the pool that
///         absorbs incoming damage before it reaches the character, and <c>DamageResponse</c> is the row
///         used when the body itself is hit. <c>Posetype</c> names the pose the body collides with —
///         the same <c>PoseType</c> record the character's own collision component carries.
///         <c>Staticobj</c>, <c>BlockEnemiesOnly</c>, <c>Aimorient</c> and <c>InheritScale</c> are
///         recorded on the body; the server's damage path acts on the pool and the enemy-only flag, and
///         the rest describe the body to the client.
///     </para>
/// </summary>
public class AddPhysicsCommand : Command, ICommand
{
    private AddPhysicsCommandDef Params;

    public AddPhysicsCommand(AddPhysicsCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Self is not CharacterEntity character)
        {
            Logger.Debug("AddPhysics {CommandId}: self is not a character, nothing to attach a body to", Params.Id);
            return true;
        }

        context.Actives.Add(this, new AddPhysicsActiveContext { Character = character });

        return true;
    }

    public void OnApply(Context context, ICommandActiveContext activeCommandContext)
    {
        if (activeCommandContext is not AddPhysicsActiveContext active || active.Character == null)
        {
            return;
        }

        float hitpoints = AbilitySystem.RegistryOp(context.Register, Params.Hitpoints, (Operand)Params.HitpointsRegop);

        var body = new AbilityPhysicsBody
        {
            PoseTypeId = Params.Posetype,
            Hitpoints = Params.Hitpoints,
            CurrentHitpoints = hitpoints,
            DamageResponse = Params.DamageResponse,
            Static = Params.Staticobj != 0,
            BlockEnemiesOnly = Params.BlockEnemiesOnly != 0,
            AimOrient = Params.Aimorient != 0,
            InheritScale = Params.InheritScale != 0,
        };

        active.Character.AbilityPhysics = body;
        active.Body = body;

        Logger.Debug(
            "AddPhysics {CommandId}: body on {Entity} with {Hitpoints} hp (pose {Pose}, damage response {Response}, static={Static}, enemiesOnly={EnemiesOnly})",
            Params.Id, active.Character.EntityId, hitpoints, Params.Posetype, Params.DamageResponse,
            body.Static, body.BlockEnemiesOnly);
    }

    public void OnRemove(Context context, ICommandActiveContext activeCommandContext)
    {
        if (activeCommandContext is not AddPhysicsActiveContext { Body: not null } active)
        {
            return;
        }

        // Only take the body off if it is still the one this command installed; a later AddPhysics on
        // the same character must not be undone by an earlier effect ending.
        if (active.Character != null && ReferenceEquals(active.Character.AbilityPhysics, active.Body))
        {
            active.Character.AbilityPhysics = null;
        }

        active.Body = null;
    }

    private class AddPhysicsActiveContext : ICommandActiveContext
    {
        public CharacterEntity Character;
        public AbilityPhysicsBody Body;
    }
}
