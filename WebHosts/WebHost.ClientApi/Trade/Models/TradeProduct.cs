using System.Collections.Generic;

namespace WebHost.ClientApi.Trade.Models;

/// <summary>
/// The body of <c>POST api/v3/trade/products</c>: the cosmetic types the
/// client's garage / New You store panes want (<c>warpaints</c>,
/// <c>czi_patterns</c>, <c>decals</c>, <c>visual_overrides</c>, <c>head</c>,
/// <c>head_accessory</c>, <c>ornaments</c>).
/// </summary>
public class TradeProductsRequest
{
    public List<string> Types { get; set; }
}

/// <summary>
/// One row of the <c>POST api/v3/trade/products</c> answer. <c>remote_type</c>
/// + <c>remote_id</c> name the SDB item the client equips when the entry is
/// picked; the shape is the live service's, captured 2015-05-02 (build 1869).
/// </summary>
public class TradeProduct
{
    public uint Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string RemoteType { get; set; } = string.Empty;

    public uint RemoteId { get; set; }

    public int Quantity { get; set; } = 1;

    public string UnlockContext { get; set; } = "account";

    public int Duration { get; set; }

    public List<TradePrice> Prices { get; set; } = [];
}

/// <summary>One entry of a <see cref="TradeProduct"/>'s price list.</summary>
public class TradePrice
{
    public uint Id { get; set; }

    public string CurrencyType { get; set; } = "redbean";

    public uint CurrencyRemoteId { get; set; }

    public int Amount { get; set; }
}
