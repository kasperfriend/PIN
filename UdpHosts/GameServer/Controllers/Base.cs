#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using Aero.Protocol;
using GameServer.Extensions;
using GameServer.Packets;
using Serilog;

namespace GameServer.Controllers;

public abstract class Base
{
    private static readonly ProtocolRoute CharacterRoute = new(GssTables.Ns.Character, GssTables.Kind.Command, typeof(GssCharacterCommand));
    private static readonly ProtocolRoute VehicleRoute = new(GssTables.Ns.Vehicle, GssTables.Kind.Command, typeof(GssVehicleCommand));
    private static readonly ProtocolRoute TurretRoute = new(GssTables.Ns.Turret, GssTables.Kind.Command, typeof(GssTurretCommand));
    private static readonly ProtocolRoute RootRoute = new(GssTables.Ns.Root, GssTables.Kind.Message, typeof(GssMessage));

    private Dictionary<byte, MethodInfo>? _dispatch;
    private GssVersion _dispatchVersion;

    protected Base()
    {
        var attr = GetType().GetAttribute<TypecodeAttribute>();

        if (attr == null)
        {
            throw new MissingMemberException(GetType().FullName, "Missing required Typecode attribute");
        }

        Namespace = attr.Namespace;
        ViewOrdinal = attr.ViewOrdinal;
        TypecodeName = attr.TypecodeName;
    }

    public int Namespace { get; }
    public int ViewOrdinal { get; }
    public string TypecodeName { get; }

    public abstract void Init(INetworkClient client, IPlayer player, IShard shard, ILogger logger);

    public void HandlePacket(INetworkClient client, IPlayer player, ulong entityId, byte msgId, GamePacket packet, ILogger logger)
    {
        var version = client.AssignedShard.Settings.GssProtocolVersion;

        if (!GetDispatchTable(version).TryGetValue(msgId, out var method))
        {
            logger.Warning("Unhandled message {TypecodeName}::{MessageName} (tc-{Typecode} mid-{MessageId}) from Entity 0x{EntityId:X8}", TypecodeName, GetUnhandledMessageLookup(version, msgId), GetTypecode(version), msgId, entityId);
            if (logger.IsEnabled(Serilog.Events.LogEventLevel.Warning))
            {
                logger.Warning(">  {PacketData}", BitConverter.ToString(packet.Peek(packet.BytesRemaining).ToArray()).Replace("-", " "));
            }

            return;
        }

        try
        {
            _ = method.Invoke(this, [client, player, entityId, packet]);
        }
        catch (TargetInvocationException e)
        {
            if (e.InnerException != null)
            {
                logger.Error("HandlePacket Caught {ExceptionMessage}", e.InnerException.Message);
                logger.Error("{StackTrace}", e.InnerException.StackTrace);
            }
        }
    }

    protected void LogMissingImplementation<TController>(string endpointName, ulong entityId, GamePacket packet, ILogger logger)
    {
        logger.Warning("Unimplemented Endpoint was called by entity 0x{EntityId:X8}: {ControllerFullName}.{Endpoint}", entityId, typeof(TController).FullName, endpointName);
        if (logger.IsEnabled(Serilog.Events.LogEventLevel.Warning))
        {
            logger.Warning(">  {PacketData}", BitConverter.ToString(packet.PacketData.ToArray()).Replace("-", " "));
        }
    }

    private static ProtocolRoute? GetProtocolRoute(int ns)
    {
        switch (ns)
        {
            case GssTables.Ns.Character:
                return CharacterRoute;
            case GssTables.Ns.Vehicle:
                return VehicleRoute;
            case GssTables.Ns.Turret:
                return TurretRoute;
            case GssTables.Ns.Root:
                return RootRoute;
            default:
                return null;
        }
    }

    private byte GetTypecode(GssVersion version)
    {
        return ViewOrdinal >= 0
            ? GssTables.GetMessageId(version, Namespace, GssTables.Kind.View, ViewOrdinal)
            : GssTables.GetNamespaceTypecode(version, Namespace);
    }

    private Dictionary<byte, MethodInfo> GetDispatchTable(GssVersion version)
    {
        if (_dispatch != null && _dispatchVersion == version)
        {
            return _dispatch;
        }

        var table = new Dictionary<byte, MethodInfo>();
        var route = GetProtocolRoute(Namespace);

        if (route != null)
        {
            foreach (var method in ReflectionUtils.FindMethodsByAttribute<MessageIDAttribute>(this))
            {
                var protocolId = method.GetAttribute<MessageIDAttribute>().ProtocolId;

                if (protocolId.GetType() != route.ProtocolEnum)
                {
                    continue;
                }

                var wireId = GssTables.GetMessageId(version, route.Namespace, route.Kind, Convert.ToInt32(protocolId));

                if (wireId != 0)
                {
                    table[wireId] = method;
                }
            }
        }

        _dispatch = table;
        _dispatchVersion = version;
        return table;
    }

    private string GetUnhandledMessageLookup(GssVersion version, byte messageId)
    {
        var route = GetProtocolRoute(Namespace);

        if (route == null)
        {
            return "Unknown";
        }

        var lookupTypecode = GssTables.GetNamespaceTypecode(version, route.Namespace);
        var ordinal = GssTables.FindMessage(version, lookupTypecode, route.Kind, messageId);

        return ordinal >= 0 ? Enum.GetName(route.ProtocolEnum, ordinal) ?? "Unknown" : "Unknown";
    }

    private sealed record ProtocolRoute(int Namespace, int Kind, Type ProtocolEnum);
}
