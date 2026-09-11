using AeroMessages.GSS.Character;
using AeroMessages.GSS.Character.Event;
using GameServer.Entities;
using Serilog;

namespace GameServer.Systems.MovementRelay;

public class MovementRelay
{
    private static readonly ILogger Logger = Log.ForContext<MovementRelay>();

    private readonly Shard _shard;

    public MovementRelay(Shard shard)
    {
        _shard = shard;
    }

    public void CharacterMovementInput(INetworkClient client, IEntity entity, AeroMessages.GSS.Character.Command.MovementInput input)
    {
        var character = entity as Entities.Character.CharacterEntity;

        // Update our data based on the clients input
        var poseData = input.PoseData;
        var posRotState = poseData.PosRotState;
        character.SetPoseData(poseData, input.ShortTime);

        // Record a sample so the pose can be interpolated/predicted between updates
        character.RecordMovementSample(new MovementSample
        {
            ShortTime = input.ShortTime,
            Position = poseData.PosRotState.Pos,
            Orientation = poseData.PosRotState.Rot,
            Velocity = poseData.Velocity,
            MovementState = posRotState.MovementState,
            HorizontalInput = input.HorizontalInput,
            VerticalInput = input.VerticalInput,
            InputFlags = input.InputFlags
        });

        // The client counts the time since its last jump in 16 bit milliseconds and restarts the counter at
        // zero on every jump, so "a new jump happened" means the value went backwards. Comparing the raw signed
        // shorts said that every time the counter passed 32767 (about 33 seconds in the air, which is exactly
        // what a boost panel launch and a failed glider deploy look like), because there the value simply wraps
        // into the negative range. The client then got a JumpActioned for its own character while it was still
        // falling, restarted its jump handling and dropped the wings deployment the launch had just started.
        bool sendJumpActioned = IsJumpCounterReset(character.TimeSinceLastJump, poseData.TimeSinceLastJump);
        character.TimeSinceLastJump = poseData.TimeSinceLastJump;

        character.IsAirborne = poseData.GroundTimePositiveAirTimeNegative < 0;

        // A pose from the client is the truth the aptitude gates must use from here on. If the server was still
        // inside the provisional window it opened when it pushed this character (glider pad launch), a pose
        // closes that window once it can answer the question the window exists for: the client reports the
        // character airborne (the launch worked), or the forced movement the server commanded has ended and
        // the pose is the client's own again. Poses received while the forced movement may still be playing
        // are the client's in-between state and do not end the window — and, as far as the authoring client is
        // concerned, are not confirmed back yet either (see ShouldHoldAuthoringConfirmation below). Log what
        // the client actually reported so a launch that never produced an airborne pose is distinguishable
        // from one the server tore down too early.
        bool closedServerLaunch = character.IsServerLaunchPending
            && (!character.IsServerLaunchForcedWindowActive || poseData.GroundTimePositiveAirTimeNegative < 0);
        if (closedServerLaunch)
        {
            character.ClearServerLaunchPending();
            Logger.Debug(
                "[Glider] Launch handoff: first MovementInput after server push MoveState={MoveState} AirTime={AirTime} Airborne={Airborne} VelocityZ={VelocityZ}",
                posRotState.MovementState, poseData.GroundTimePositiveAirTimeNegative, character.IsAirborne, poseData.Velocity.Z);
        }

        // Feed the pose into the fall damage tracker (applies damage on landings)
        _shard.FallDamage.OnMovementInput(character, poseData);

        var movementStateValue = posRotState.MovementState;
        character.MovementStateContainer.MovementStateValue = (ushort)movementStateValue;

        // Update with physics
        _shard.Physics.UpdateEntity(character);

        // Confirm the pose with the client. Do *not* confirm a still-grounded pose to the authoring client
        // while a launch the server just commanded is still pending and inside its forced window: the client
        // receives the ForcedMovement impulse asynchronously, and if its first post-push MovementInput (still
        // grounded, because the impulse has not been applied yet) is confirmed immediately, the client treats
        // that grounded pose as authoritative and drops the pending launch - the observed `MoveState=4096 ...
        // Airborne=False VelocityZ=0` failure. Holding the confirm gives the client a handful of milliseconds
        // to apply the impulse; the next input (or the first one after the forced window ends) then gets the
        // normal authoritative confirmation.
        bool holdAuthoringConfirmation = ShouldHoldAuthoringConfirmation(character, poseData);
        var confirmedPose = new ConfirmedPoseUpdate
        {
            PoseData = new MovementPoseData
            {
                ShortTime = input.ShortTime,
                MovementType = MovementDataType.PosRotState,
                WaterLevelAndDesc = poseData.WaterLevelAndDesc,
                PosRotState = new MovementPosRotState
                            {
                                Pos = character.Position,
                                Rot = character.Orientation,
                                MovementState = movementStateValue
                            },
                Velocity = character.Velocity,
                JetpackEnergy = poseData.JetpackEnergy,
                GroundTimePositiveAirTimeNegative = poseData.GroundTimePositiveAirTimeNegative, // Somehow affects gravity
                TimeSinceLastJump = poseData.TimeSinceLastJump,
                HaveDebugData = 0
            },
            NextShortTime = unchecked((ushort)(input.ShortTime + 90)) // This value has to be in the future, nobody cares why.
        };
        if (!holdAuthoringConfirmation)
        {
            client.NetChannels[ChannelType.UnreliableGss].SendMessage(confirmedPose, character.EntityId);
        }
        else
        {
            Logger.Debug("[Glider] Held authoring pose confirm: pending launch, reported AirTime={AirTime} MoveState={MoveState}",
                poseData.GroundTimePositiveAirTimeNegative, movementStateValue);
        }

        // Forward update to remote clients
        // Built once for the whole broadcast instead of per recipient: SendMessage only reads the
        // message (it serializes a fresh copy per channel), so one instance can serve every client.
        JumpActioned jumpActioned = sendJumpActioned ? new JumpActioned { ShortTime = input.ShortTime } : null;

        var currentPose = new CurrentPoseUpdate
        {
            Data = new AeroMessages.GSS.CurrentPoseUpdateData
            {
                Flags = 0x00,
                ShortTime = character.MovementShortTime,
                UnkAlwaysPresent = 0x79,
                MovementState = (ushort)character.MovementState,
                Position = character.Position,
                Rotation = character.Orientation,
                Aim = character.AimDirection,
            }
        };
        foreach (var remoteClient in _shard.Clients.Values)
        {
            if (!remoteClient.Status.Equals(IPlayer.PlayerStatus.Playing))
            {
                continue;
            }

            bool isSelf = remoteClient.SocketId == client.SocketId;

            // Never re-apply the "remote avatar" CurrentPoseUpdate to the client that authored it:
            // it already got the authoritative answer as a ConfirmedPoseUpdate above (or is deliberately
            // waiting for the pending launch to be confirmed), and re-applying its own pose on every
            // movement tick is what made the first person animation flicker between states while sprinting.
            // So only the pose broadcast is skipped for self.
            if (!isSelf)
            {
                remoteClient.NetChannels[ChannelType.UnreliableGss].SendMessage(currentPose, character.EntityId);
            }

            // But the authoring client still needs the JumpActioned acknowledgement to commit a
            // self-initiated jump/launch (a glider pad reports its launch as a jump via
            // TimeSinceLastJump resetting). Without it the client starts the launch/wings state and
            // aborts when the ack never arrives. Remote clients need it too.
            if (jumpActioned != null)
            {
                remoteClient.NetChannels[ChannelType.UnreliableGss].SendMessage(jumpActioned, character.EntityId);
            }
        }
    }

    public void VehicleMovementInput(INetworkClient client, IEntity entity, AeroMessages.GSS.Vehicle.Command.MovementInput input)
    {
        var vehicle = entity as Entities.Vehicle.VehicleEntity;
        vehicle.SetPoseData(input);

        // Update with physics
        _shard.Physics.UpdateEntity(vehicle);

        if (vehicle.ControllingPlayer?.CharacterEntity != null)
        {
            var character = vehicle.ControllingPlayer.CharacterEntity;
            character.SetPosition(input.Position);
            CharacterMovementInput(client, character, new AeroMessages.GSS.Character.Command.MovementInput()
            {
                ShortTime = client.AssignedShard.CurrentShortTime,
                PoseData = new MovementPoseData()
                {
                    ShortTime = client.AssignedShard.CurrentShortTime,
                    MovementType = MovementDataType.PosRotState,
                    WaterLevelAndDesc = 0,
                    PosRotState = new MovementPosRotState()
                    {
                        Pos = input.Position,
                        Rot = character.Orientation,
                        MovementState = unchecked((short)0xd000)
                    },
                    Velocity = character.Velocity,
                    JetpackEnergy = 0x639c,
                    GroundTimePositiveAirTimeNegative = 0,
                    TimeSinceLastJump = character.TimeSinceLastJump,
                    HaveDebugData = 0
                }
            });
        }
    }

    /// <summary>
    /// Whether the client's 16 bit "time since last jump" counter went backwards, which is what a new jump looks
    /// like. The comparison is modular on purpose: the counter runs 0 .. 65535 milliseconds and wraps without
    /// pausing, so only a step *backwards* of more than half the range may be treated as a reset. A jump that
    /// happens after ~32.8 s in the air is indistinguishable from the wrap and goes unnoticed, which is the one
    /// case the previous comparison got wrong in the other direction (it reported a jump at every wrap).
    /// </summary>
    internal static bool IsJumpCounterReset(short previous, short current)
    {
        var delta = unchecked((ushort)(current - previous));

        return delta >= 0x8000;
    }

    /// <summary>
    ///     True while the authoring client's confirmed pose must be held back: a launch (<c>ForcePush</c>) is
    ///     pending, its forced-window grace is still active, and the client's latest reported pose is still
    ///     grounded. The client applies the ForcedMovement impulse asynchronously, so a grounded input can
    ///     legitimately arrive before that impulse has taken effect; confirming that grounded pose immediately
    ///     makes it authoritative and makes the client drop the pending launch. Once the client reports
    ///     airborne, or once the forced window ends (the launch failed), confirmation resumes.
    /// </summary>
    internal static bool ShouldHoldAuthoringConfirmation(Entities.Character.CharacterEntity character, MovementPoseData poseData)
    {
        return character.IsServerLaunchPending
            && character.IsServerLaunchForcedWindowActive
            && poseData.GroundTimePositiveAirTimeNegative >= 0;
    }
}