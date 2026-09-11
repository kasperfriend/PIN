using System;
using System.Collections.Concurrent;
using System.Linq;
using GameServer.Extensions;

namespace GameServer.Controllers;

public static class Factory
{
    private static ConcurrentDictionary<(int Ns, int ViewOrdinal), Base> _controllers;

    /// <summary>
    ///     (ns, ordinal) pairs known to have no controller. A client can talk on typecodes the
    ///     server's protocol version routes to nothing (and does so on every packet it sends in
    ///     that state), and resolving a miss used to scan the assembly reflectively each time.
    ///     Remembering the miss turns that per-packet scan into one per unknown typecode.
    /// </summary>
    private static ConcurrentDictionary<(int Ns, int ViewOrdinal), bool> _unresolved;

    public static void Init()
    {
        _controllers = new ConcurrentDictionary<(int Ns, int ViewOrdinal), Base>();
        _unresolved = new ConcurrentDictionary<(int Ns, int ViewOrdinal), bool>();
    }

    public static T Get<T>()
        where T : Base, new()
    {
        var attr = typeof(T).GetAttribute<TypecodeAttribute>();

        if (attr == null)
        {
            throw new ArgumentNullException(nameof(T), "Type [" + typeof(T).FullName + "] does not have a Typecode Attribute.");
        }

        return _controllers.AddOrUpdate((attr.Namespace, attr.ViewOrdinal), new T(), (_, nc) => nc) as T;
    }

    public static Base Get(int ns, int viewOrdinal)
    {
        var key = (ns, viewOrdinal);

        if (_controllers.TryGetValue(key, out var controller))
        {
            return controller;
        }

        if (_unresolved.ContainsKey(key))
        {
            return null;
        }

        var t = ForTypecode(ns, viewOrdinal);

        if (t == null)
        {
            _unresolved[key] = true;
            return null;
        }

        return _controllers.AddOrUpdate(key, _ => Activator.CreateInstance(t) as Base, (_, nc) => nc);
    }

    private static Type ForTypecode(int ns, int viewOrdinal)
    {
        var ts = ReflectionUtils.FindTypesByAttribute<TypecodeAttribute>();

        return ts.FirstOrDefault(t =>
        {
            var attr = t.GetAttribute<TypecodeAttribute>();

            return attr != null && attr.Namespace == ns && attr.ViewOrdinal == viewOrdinal;
        });
    }
}
