using System;
using System.Threading.Tasks;

namespace Shared.Common;

/// <summary>
///     The server's own bounded parallelism: one place that decides how many threads a piece of
///     background work may use, and one place that runs an index range over them.
/// </summary>
/// <remarks>
///     <para>
///         Only the work that is <b>pure CPU over data nobody mutates</b> is meant to go through
///         here: the zone's navigation bake (the last heavy step of starting a shard) and the world
///         population plan build (the step that decides what a zone is populated with). Work that
///         touches the Bepu simulation, the entity tables or a client's channels stays on the thread
///         that owns it - those are not made faster by threads, only wrong by them.
///     </para>
///     <para>
///         <b>Determinism.</b> A pass that is run through <see cref="Range" /> must produce the same
///         result whatever the thread count is: every index writes only its own slot, and anything
///         that has to be merged is merged by the caller in index order afterwards. That is what
///         lets the parallel bake and the serial bake be compared in a test, and what keeps the
///         plan's promise that the same database and the same zone give the same world everywhere.
///     </para>
/// </remarks>
public static class ParallelWork
{
    /// <summary>
    ///     Ceiling for the automatic thread count. The typical setup runs the game client on the
    ///     same machine as the server, and every worker added here is a core taken from the client's
    ///     frame budget - so a machine with many cores gets a few threads, not all of them.
    /// </summary>
    public const int MaxAutomaticDegree = 8;

    /// <summary>
    ///     How many threads the server's background work uses when nothing is configured: one per
    ///     processor except the one the shard loop runs on, capped at
    ///     <see cref="MaxAutomaticDegree" />.
    /// </summary>
    public static int AutomaticDegree => Math.Clamp(Environment.ProcessorCount - 1, 1, MaxAutomaticDegree);

    /// <summary>
    ///     Turns a configured thread count into a degree of parallelism. Zero (or a negative value,
    ///     which is what a misread config gives) means automatic; a positive value is taken as given,
    ///     because an operator who writes a number has measured their machine.
    /// </summary>
    /// <param name="configuredThreads">The configured thread count; 0 for automatic.</param>
    /// <returns>How many threads the work may use; never less than 1.</returns>
    public static int Resolve(int configuredThreads) =>
        configuredThreads > 0 ? configuredThreads : AutomaticDegree;

    /// <summary>
    ///     Runs <paramref name="body" /> once for every index in <c>[0, count)</c>, on at most
    ///     <paramref name="degreeOfParallelism" /> threads. Index order is not an execution order
    ///     guarantee - the body must be written so that index <c>i</c> only writes state that index
    ///     <c>i</c> owns. An exception from the body is rethrown on the calling thread: a single
    ///     failure keeps its own type (so a caller's <c>catch (InvalidOperationException)</c> still
    ///     works), several are rethrown together.
    /// </summary>
    /// <param name="count">How many indices to run.</param>
    /// <param name="degreeOfParallelism">How many threads may run them; 1 runs them inline.</param>
    /// <param name="body">What to run for each index.</param>
    public static void Range(int count, int degreeOfParallelism, Action<int> body)
    {
        if (body == null)
        {
            throw new ArgumentNullException(nameof(body));
        }

        if (count <= 0)
        {
            return;
        }

        int degree = Math.Clamp(degreeOfParallelism, 1, count);

        if (degree == 1)
        {
            for (int i = 0; i < count; i++)
            {
                body(i);
            }

            return;
        }

        try
        {
            _ = Parallel.For(0, count, new ParallelOptions { MaxDegreeOfParallelism = degree }, body);
        }
        catch (AggregateException aggregate)
        {
            throw Unwrap(aggregate);
        }
    }

    /// <summary>
    ///     Runs <paramref name="body" /> once for every index in <c>[from, from + count)</c>.
    /// </summary>
    /// <param name="from">First index to run.</param>
    /// <param name="count">How many indices to run.</param>
    /// <param name="degreeOfParallelism">How many threads may run them; 1 runs them inline.</param>
    /// <param name="body">What to run for each index.</param>
    public static void Range(int from, int count, int degreeOfParallelism, Action<int> body)
    {
        Range(count, degreeOfParallelism, offset => body(from + offset));
    }

    /// <summary>
    ///     Splits <c>[0, count)</c> into at most <paramref name="degreeOfParallelism" /> contiguous
    ///     slices, as evenly as they divide. This is what a pass that needs per-thread state instead
    ///     of per-index slots uses: each slice runs on one thread and produces one piece of local
    ///     state, and the caller merges the pieces in slice order, which is index order - the order
    ///     the serial pass would have produced.
    /// </summary>
    /// <param name="count">How many items there are.</param>
    /// <param name="degreeOfParallelism">Desired number of slices; fewer are returned when there is less work.</param>
    /// <returns>Contiguous, non-overlapping, ascending slices covering the whole range.</returns>
    public static (int From, int Count)[] Slices(int count, int degreeOfParallelism)
    {
        if (count <= 0)
        {
            return [];
        }

        int slices = Math.Clamp(degreeOfParallelism, 1, count);
        var result = new (int From, int Count)[slices];
        int baseSize = count / slices;
        int remainder = count % slices;
        int from = 0;

        for (int i = 0; i < slices; i++)
        {
            int size = baseSize + (i < remainder ? 1 : 0);
            result[i] = (from, size);
            from += size;
        }

        return result;
    }

    private static Exception Unwrap(AggregateException aggregate)
    {
        var failures = aggregate.Flatten().InnerExceptions;
        return failures.Count == 1 ? failures[0] : aggregate;
    }
}
