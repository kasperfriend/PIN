using System;
using System.Collections.Generic;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace GameServer.Tests.Fakes;

/// <summary>
///     A Serilog logger that keeps what it was told, for tests that assert a system said something -
///     or held its tongue. <see cref="Log.Logger"/> is deliberately left alone: xunit runs test
///     classes in parallel, so no test may reconfigure the process-wide logger to capture a message.
/// </summary>
public sealed class CapturingLogger
{
    private readonly Sink _sink = new();

    public CapturingLogger() =>
        Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(_sink)
            .CreateLogger();

    /// <summary>The logger to hand to the system under test (or to a shard the system reads it from).</summary>
    public ILogger Logger { get; }

    /// <summary>Every message, rendered with its property values, in the order it was logged.</summary>
    public IReadOnlyList<string> Messages => _sink.Messages;

    /// <summary>How many logged messages contain <paramref name="fragment"/>.</summary>
    public int CountContaining(string fragment)
    {
        int count = 0;
        foreach (string message in _sink.Messages)
        {
            if (message.Contains(fragment, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private sealed class Sink : ILogEventSink
    {
        public List<string> Messages { get; } = [];

        public void Emit(LogEvent logEvent) => Messages.Add(logEvent.RenderMessage());
    }
}
