using System.Collections.Generic;

namespace Shared.Web.Config;

/// <summary>
///     The <c>Firefall:Assets</c> configuration section: which folders the web asset host serves from.
/// </summary>
public class FirefallAssets
{
    /// <summary>
    ///     Extra roots the web asset host serves from, after <c>Assets</c> next to the binary.
    /// </summary>
    /// <remarks>
    ///     A relative path is taken from the host's content root, an absolute one (or a UNC share on a second
    ///     machine) from wherever it points, and every root is searched in the order given until one holds the
    ///     file - so this is how a server answers the client's high-resolution texture requests without keeping
    ///     a second copy of a dozen gigabytes: the chunks stay where the client's install already has them and
    ///     the folder is merely named here. A root that does not exist is reported at startup and skipped, not
    ///     a reason for the host to refuse to start. See <c>Docs/ASSETS.md</c>.
    /// </remarks>
    public IList<string> Paths { get; set; } = new List<string>();
}
