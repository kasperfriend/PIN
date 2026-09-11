using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WebHost.ClientApi.Trade.Models;

namespace WebHost.ClientApi.Trade;

[ApiController]
public class TradeController : ControllerBase
{
    /// <summary>
    /// The cosmetics the client's garage / New You store panes ask for when the
    /// screen opens (<c>{"types":["head","head_accessory","ornaments"]}</c> on
    /// the character side, warpaints/patterns/decals on the battleframe side —
    /// captured from the live client, 2015-05-02).
    ///
    /// The body is read by hand rather than through <c>[FromBody]</c>: a body
    /// the framework refuses would be answered with the framework's own
    /// non-JSON 400/415, which the client cannot read, and this endpoint has to
    /// keep working for any request shape.
    /// </summary>
    [Route("api/v3/trade/products")]
    [HttpPost]
    [Produces("application/json")]
    public async Task<IReadOnlyList<TradeProduct>> TradeProducts()
    {
        var types = await ReadRequestedTypes();

        return TradeProductCatalog.For(types);
    }

    [Route("api/v3/trade/products/garage_slot_perk_respec")]
    [HttpGet]
    public object GarageSlotPerkRespec()
    {
        return new { };
    }

    private async Task<IReadOnlyList<string>> ReadRequestedTypes()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            var request = JsonSerializer.Deserialize<TradeProductsRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return request?.Types;
        }
        catch (JsonException)
        {
            // Anything the client sends that is not the request model still has
            // to get the catalogue back.
            return null;
        }
    }
}
