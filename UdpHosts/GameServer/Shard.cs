using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AeroMessages.Control;
using GameServer.Entities;
using GameServer.Entities.Outpost;
using GameServer.Physics;
using GameServer.Systems.Admin;
using GameServer.Systems.Ai;
using GameServer.Systems.Aptitude;
using GameServer.Systems.CharacterLifecycle;
using GameServer.Systems.Chat;
using GameServer.Systems.Cheats;
using GameServer.Systems.Combat;
using GameServer.Systems.CombatLog;
using GameServer.Systems.Encounters;
using GameServer.Systems.EntityManager;
using GameServer.Systems.MovementRelay;
using GameServer.Systems.NpcDeath;
using GameServer.Systems.PlayerRespawn;
using GameServer.Systems.ProjectileSim;
using GameServer.Systems.SystemEvents;
using GameServer.Systems.WeaponSim;
using Shared.Common;
using Shared.Udp;

namespace GameServer;

public class Shard : IShard
{
    /// <summary>
    ///     How often the network half of every client (reliable acks, queued sends, movement confirms)
    ///     is serviced. The constant this replaces was <c>1.0 / 20.0</c> — a value in *seconds* compared
    ///     against *milliseconds*, so the gate opened every 50 µs and the network tick ran on virtually
    ///     every spin of the loop. 10 ms is twice the cadence that value was meant to express, and it
    ///     bounds a movement confirm (input queue → ConfirmedPoseUpdate on the wire) to ≤ 10 ms.
    /// </summary>
    private const double _networkTickIntervalMs = 10.0;

    /// <summary>
    ///     Target cadence of the shard loop. The loop used to spin unbounded — a full core at 100% even
    ///     on an idle shard, and the typical setup runs the game client on the same machine, so the spin
    ///     raced the client for CPU. Every subsystem is gate- or accumulator-driven, so an exact cadence
    ///     is not required: 5 ms keeps the fastest gate (the 5 ms entity update flush) at its intended
    ///     rate, and everything coarser simply fires on the first loop iteration past its own interval.
    /// </summary>
    private const double _loopIntervalMs = 5.0;

    /// <summary>Silence on a client's receive path that marks it as gone.</summary>
    private static readonly TimeSpan ClientReceiveTimeout = TimeSpan.FromSeconds(60);

    /// <summary>How often to look for clients whose receive path went silent.</summary>
    private static readonly TimeSpan ClientSweepInterval = TimeSpan.FromSeconds(5);

    private long _startTime;
    private double _lastNetTick;
    private DateTime _lastClientSweep = DateTime.MinValue;

    public Shard(double gameTickRate, ulong instanceId, GameServerSettings settings, IPacketSender sender, Serilog.ILogger logger)
    {
        InstanceId = instanceId;
        Settings = settings;
        ZoneId = settings.ZoneId;
        Sender = sender;
        Logger = logger;
        Clients = new ConcurrentDictionary<uint, INetworkPlayer>();
        Entities = new ConcurrentDictionary<ulong, IEntity>();
        Encounters = new ConcurrentDictionary<ulong, IEncounter>();
        Outposts = new ConcurrentDictionary<uint, IDictionary<uint, OutpostEntity>>();
        EventBus = new EventBus();
        var debugCallbacks = new DebugProjectileHitCallbacks(this);
        Physics = new PhysicsEngine(EventBus, Settings.ZoneId, Settings.MapsPath, Settings.AssetDBPath, Settings.LoadMapsCollision, debugCallbacks, false, Settings.CachePath, Settings.ForceReloadZone);
        AI = new AiEngine(this, EventBus);
        Movement = new MovementRelay(this);
        Abilities = new AbilitySystem(this);
        EntityMan = new EntityManager(this);
        EncounterMan = new EncounterManager(this);
        WeaponSim = new WeaponSim(this);
        ProjectileSim = new ProjectileSim(this, debugCallbacks);
        Chat = new ChatService(this, EventBus);
        Admin = new AdminService(this);
        var npcDeathRules = new StandardNpcDeathRules();
        Cheats = new CheatService(this);
        Damage = new DamageSystem(EventBus, this, npcDeathRules);
        Combat = new CombatSim(EventBus, Damage, this);
        CombatLog = new CombatLogSink();
        FallDamage = new FallDamageSystem(this, Damage, new StandardFallDamageRules());
        CharacterLifecycle = new CharacterLifecycleService(this, EventBus, new StandardCharacterLifecycleRules());
        PlayerRespawn = new PlayerRespawnService(this, EventBus, new StandardPlayerRespawnRules(), CharacterLifecycle);
        NpcDeath = new NpcDeathService(this, EventBus, npcDeathRules);
    }

    public DateTime StartTime => DateTimeExtensions.Epoch.AddSeconds(_startTime);
    public IDictionary<ulong, IEntity> Entities { get; protected set; }
    public IDictionary<ulong, IEncounter> Encounters { get; protected set; }
    public IDictionary<uint, IDictionary<uint, OutpostEntity>> Outposts { get; protected set; }
    public IDictionary<uint, INetworkPlayer> Clients { get; }
    public EventBus EventBus { get; }
    public PhysicsEngine Physics { get; }
    public AiEngine AI { get; }
    public MovementRelay Movement { get; }
    public EntityManager EntityMan { get; }
    public EncounterManager EncounterMan { get; }
    public AbilitySystem Abilities { get; }
    public ProjectileSim ProjectileSim { get; }
    public WeaponSim WeaponSim { get; }
    public ChatService Chat { get; }
    public AdminService Admin { get; }
    public CheatService Cheats { get; }
    public DamageSystem Damage { get; }
    public CombatSim Combat { get; }
    public ICombatLogSink CombatLog { get; }
    public FallDamageSystem FallDamage { get; }
    public CharacterLifecycleService CharacterLifecycle { get; }
    public PlayerRespawnService PlayerRespawn { get; }
    public NpcDeathService NpcDeath { get; }
    public ulong InstanceId { get; }
    public uint ZoneId { get; private set; }
    public ulong CurrentTimeLong { get; private set; }
    public uint CurrentTime => unchecked((uint)CurrentTimeLong);
    public ushort CurrentShortTime => unchecked((ushort)CurrentTime);
    public Serilog.ILogger Logger { get; }
    public GameServerSettings Settings { get; }
    private IPacketSender Sender { get; }

    public void Run(CancellationToken ct)
    {
        Utils.RunThread(RunThread, ct);
    }

    public void NetworkTick(double deltaTime, ulong currentTime, CancellationToken ct)
    {
        // Handle timeout, reliable retransmission, normal rx/tx
        foreach (var client in Clients.Values)
        {
            try
            {
                client.NetworkTick(deltaTime, currentTime, ct);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // Unpacking and dispatching messages is where a not yet complete controller path can throw. Keep
                // that to a single client: the other clients in the zone still get their traffic processed, and the
                // shard thread (and its UDP socket) stays alive.
                Logger.Error(e, "Shard {ShardId} failed to process network traffic for client {SocketId}", InstanceId, client.SocketId);
            }
        }

        DropSilentClients();
    }

    public bool Tick(double deltaTime, ulong currentTime, CancellationToken ct)
    {
        CurrentTimeLong = currentTime;

        AI.Tick(deltaTime, currentTime, ct);
        Physics.Tick(deltaTime, currentTime, ct);
        EntityMan.Tick(deltaTime, currentTime, ct);
        EncounterMan.Tick(deltaTime, currentTime, ct);
        Abilities.Tick(deltaTime, currentTime, ct);
        WeaponSim.Tick(deltaTime, currentTime, ct);
        ProjectileSim.Tick(deltaTime, currentTime, ct);
        Cheats.Tick(deltaTime, currentTime, ct);
        Damage.Tick(deltaTime, currentTime, ct);
        FallDamage.Tick(deltaTime, currentTime, ct);
        CharacterLifecycle.Tick(deltaTime, currentTime, ct);
        PlayerRespawn.Tick(deltaTime, currentTime, ct);
        EventBus.Flush();

        return true;
    }

    public bool MigrateOut(INetworkPlayer player)
    {
        // TryRemove is the check-and-remove in one atomic step: ContainsKey followed by Remove
        // races with a concurrent MigrateOut of the same player (both threads can pass the
        // ContainsKey check and then run the cleanup twice).
        if (((ConcurrentDictionary<uint, INetworkPlayer>)Clients).TryRemove(new KeyValuePair<uint, INetworkPlayer>(player.SocketId, player)))
        {
            if (Entities.ContainsKey(player.CharacterId))
            {
                EntityMan.Remove(player.CharacterId);
            }

            Admin.ClearPlayer(player);
            Cheats.Forget(player);
            return true;
        }

        return false;
    }

    public bool MigrateIn(INetworkPlayer player)
    {
        // TryAdd keeps the ContainsKey/Add pair atomic, so Init cannot run twice for the
        // same player when two threads race to migrate the same socket in.
        if (((ConcurrentDictionary<uint, INetworkPlayer>)Clients).TryAdd(player.SocketId, player))
        {
            player.Init(this);
            return true;
        }

        return true;
    }

    public async Task<bool> SendAsync(Memory<byte> packet, IPEndPoint endPoint)
    {
        return await Sender.SendAsync(packet, endPoint);
    }

    public ulong GetNextGuid(byte type = 0)
    {
        return GuidService.GetNext(this, type);
    }

    protected virtual bool ShouldNetworkTick(double deltaTime, ulong currentTime)
    {
        // deltaTime is the number of milliseconds since the last network tick (see RunThread).
        return deltaTime >= _networkTickIntervalMs;
    }

    /// <summary>
    ///     Waits until the stopwatch reaches <paramref name="targetTotalMs" />. Coarse
    ///     <see cref="Thread.Sleep(int)" /> while a comfortable margin remains (cheap, and safe to
    ///     overshoot — every consumer of the loop is gate- or accumulator-driven, so a late iteration
    ///     just fires the gates a little later), then a <see cref="SpinWait" /> for the last couple of
    ///     milliseconds so the cadence stays tight without busy-spinning the whole interval.
    /// </summary>
    private static void WaitForNextTick(Stopwatch stopwatch, double targetTotalMs, CancellationToken ct)
    {
        var spinner = new SpinWait();

        while (!ct.IsCancellationRequested)
        {
            var remaining = targetTotalMs - stopwatch.Elapsed.TotalMilliseconds;
            if (remaining <= 0)
            {
                return;
            }

            if (remaining > 2.0)
            {
                Thread.Sleep((int)(remaining - 1.5));
                spinner.Reset();
            }
            else
            {
                spinner.SpinOnce();
            }
        }
    }

    private void RunThread(CancellationToken ct)
    {
        _startTime = (long)DateTime.Now.UnixTimestamp();
        _lastNetTick = 0;

        var stopwatch = new Stopwatch();
        var lastTime = 0.0;

        stopwatch.Start();

        while (!ct.IsCancellationRequested)
        {
            // (ulong)(DateTime.Now.UnixTimestamp() * 1000);
            var currentUnixTimestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var currentTime = unchecked((ulong)stopwatch.Elapsed.TotalMilliseconds);
            var delta = currentTime - lastTime;

            try
            {
                if (ShouldNetworkTick(currentTime - _lastNetTick, currentUnixTimestamp))
                {
                    NetworkTick(currentTime - _lastNetTick, currentUnixTimestamp, ct);
                    _lastNetTick = currentTime;
                }

                if (!Tick(delta, currentUnixTimestamp, ct))
                {
                    break;
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // This thread is a raw Thread, so an exception escaping a tick does not just skip the tick, it
                // tears the whole shard down while the process and its UDP socket keep running: every client in
                // the zone then sees a dead server ("Connection Problem") with nothing in the log pointing at
                // the cause. Keep serving the other clients and report what broke instead.
                Logger.Error(e, "Shard {ShardId} threw during the tick at {Time} ms, continuing", InstanceId, currentTime);
            }

            lastTime = currentTime;

            // Pace the loop instead of spinning: the unbounded spin this replaced burned a full core
            // even on an idle shard, and in the usual server-plus-client-on-one-machine setup that
            // core came straight out of the client's frame budget.
            WaitForNextTick(stopwatch, currentTime + _loopIntervalMs, ct);
        }

        stopwatch.Stop();
    }

    /// <summary>
    ///     Retires clients whose receive path has been silent past the timeout. Nothing else removes a
    ///     client that vanished without a <c>CloseConnection</c> (crash, network drop, alt-F4): it stays
    ///     in the client map and scoped to every entity it can see, so pose broadcasts and view flushes
    ///     keep allocating and sending to a ghost forever. <see cref="NetworkClient.NetLastActive" />
    ///     cannot detect this because it is also refreshed by *sends*;
    ///     <see cref="NetworkClient.NetLastReceive" /> tracks receives only. A connected client always
    ///     talks (movement, acks, time sync), so a full minute of one-way silence means it is gone.
    /// </summary>
    private void DropSilentClients()
    {
        var now = DateTime.Now;
        if (now - _lastClientSweep < ClientSweepInterval)
        {
            return;
        }

        _lastClientSweep = now;

        foreach (var client in Clients.Values)
        {
            // Test fakes and any other INetworkPlayer that is not the real network client have no
            // receive path to check. ConcurrentDictionary enumeration tolerates the removal below.
            if (client is not NetworkClient networkClient || networkClient.NetChannels == null)
            {
                continue;
            }

            if (networkClient.NetClientStatus is ClientStatus.Disconnecting or ClientStatus.Aborted)
            {
                continue;
            }

            if (now - networkClient.NetLastReceive <= ClientReceiveTimeout)
            {
                continue;
            }

            Logger.Information(
                "Client {SocketId} went silent for {Seconds:F0}s (last receive {LastReceive}); disconnecting it",
                client.SocketId,
                ClientReceiveTimeout.TotalSeconds,
                networkClient.NetLastReceive);

            try
            {
                networkClient.NetChannels[ChannelType.Control].SendMessage(new CloseConnection { Unk = [0, 0, 0, 0] });
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Could not send CloseConnection to silent client {SocketId}", client.SocketId);
            }

            networkClient.NetClientStatus = ClientStatus.Disconnecting;
            MigrateOut(client);
        }
    }
}