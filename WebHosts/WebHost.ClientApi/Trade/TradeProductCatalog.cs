using System;
using System.Collections.Generic;
using System.Linq;
using WebHost.ClientApi.Trade.Models;

namespace WebHost.ClientApi.Trade;

/// <summary>
/// The cosmetics the client's garage and New You store panes list, answered by
/// <c>POST api/v3/trade/products</c> for the <c>types</c> the screen asked for
/// (battleframe side: warpaints, czi_patterns, decals, visual_overrides;
/// character side: head, head_accessory, ornaments).
///
/// PIN has no store back end — no catalogue database, no red bean balance and
/// no purchase flow — so this is the live service's own answer for build 1869,
/// transcribed from its captured responses (2015-05-02): names, remote ids,
/// unlock context and prices exactly as the client received them. The purchase
/// itself is the client's <c>purchase_and_update</c> call on the visual
/// loadout, which PIN applies for free like everything else it hands out.
/// </summary>
public static class TradeProductCatalog
{
    /// <summary>
    /// The products for <paramref name="types"/>. A null or empty request
    /// answers with the whole catalogue, which is also what the original
    /// service did — the types only tell it which panes are open.
    /// </summary>
    public static IReadOnlyList<TradeProduct> For(IReadOnlyCollection<string> types)
    {
        if (types == null || types.Count == 0)
        {
            return All;
        }

        return All.Where(product => types.Contains(product.RemoteType, StringComparer.OrdinalIgnoreCase)).ToList();
    }

    private static readonly TradeProduct[] All =
    [
        new() { Id = 213511, Name = "Brood", RemoteType = "decals", RemoteId = 10005, Prices = [new() { Id = 1308921, Amount = 50 }] },
        new() { Id = 213611, Name = "Heart", RemoteType = "decals", RemoteId = 10006, Prices = [new() { Id = 441211, Amount = 10 }] },
        new() { Id = 213711, Name = "Stellar Ponies", RemoteType = "decals", RemoteId = 10007, Prices = [new() { Id = 440511, Amount = 50 }] },
        new() { Id = 214811, Name = "Brood II", RemoteType = "decals", RemoteId = 10029, Prices = [new() { Id = 440211, Amount = 50 }] },
        new() { Id = 214311, Name = "Fragger", RemoteType = "decals", RemoteId = 10024, Prices = [new() { Id = 441011, Amount = 10 }] },
        new() { Id = 214611, Name = "Blood Falcons", RemoteType = "decals", RemoteId = 10027, Prices = [new() { Id = 440411, Amount = 50 }] },
        new() { Id = 213911, Name = "Skull 'n Bones", RemoteType = "decals", RemoteId = 10020, Prices = [new() { Id = 440911, Amount = 25 }] },
        new() { Id = 214711, Name = "Emerald Blades", RemoteType = "decals", RemoteId = 10028, Prices = [new() { Id = 440311, Amount = 50 }] },
        new() { Id = 214211, Name = "Happy Head", RemoteType = "decals", RemoteId = 10023, Prices = [new() { Id = 441111, Amount = 10 }] },
        new() { Id = 214511, Name = "Da Bomb", RemoteType = "decals", RemoteId = 10026, Prices = [new() { Id = 440711, Amount = 25 }] },
        new() { Id = 214011, Name = "Defender", RemoteType = "decals", RemoteId = 10021, Prices = [new() { Id = 440811, Amount = 25 }] },
        new() { Id = 293911, Name = "Leopard", RemoteType = "czi_patterns", RemoteId = 10014, Prices = [new() { Id = 630111, Amount = 100 }] },
        new() { Id = 293411, Name = "Camouflage", RemoteType = "czi_patterns", RemoteId = 10004, Prices = [new() { Id = 629811, Amount = 75 }] },
        new() { Id = 293711, Name = "Magma", RemoteType = "czi_patterns", RemoteId = 10012, Prices = [new() { Id = 629911, Amount = 100 }] },
        new() { Id = 293511, Name = "Digital Camo", RemoteType = "czi_patterns", RemoteId = 10010, Prices = [new() { Id = 1223721, Amount = 100 }] },
        new() { Id = 294311, Name = "Paint Splatter", RemoteType = "czi_patterns", RemoteId = 10018, Prices = [new() { Id = 1223821, Amount = 100 }] },
        new() { Id = 293811, Name = "Magma", RemoteType = "czi_patterns", RemoteId = 10013, Prices = [new() { Id = 630011, Amount = 75 }] },
        new() { Id = 293311, Name = "Camouflage", RemoteType = "czi_patterns", RemoteId = 10001, Prices = [new() { Id = 629711, Amount = 100 }] },
        new() { Id = 294111, Name = "Leopard", RemoteType = "czi_patterns", RemoteId = 10016, Prices = [new() { Id = 630211, Amount = 75 }] },
        new() { Id = 293611, Name = "Digital Camo", RemoteType = "czi_patterns", RemoteId = 10011, Prices = [new() { Id = 1223621, Amount = 75 }] },
        new() { Id = 503721, Name = "Storm", RemoteType = "warpaints", RemoteId = 77229, Prices = [new() { Id = 1217821, Amount = 30 }] },
        new() { Id = 503821, Name = "Bumblebee", RemoteType = "warpaints", RemoteId = 77231, Prices = [new() { Id = 1217921, Amount = 30 }] },
        new() { Id = 503921, Name = "Serpent", RemoteType = "warpaints", RemoteId = 77228, Prices = [new() { Id = 1218021, Amount = 10 }] },
        new() { Id = 504021, Name = "Army", RemoteType = "warpaints", RemoteId = 77227, Prices = [new() { Id = 1218121, Amount = 10 }] },
        new() { Id = 504121, Name = "Desert Storm", RemoteType = "warpaints", RemoteId = 77226, Prices = [new() { Id = 1218221, Amount = 10 }] },
        new() { Id = 504221, Name = "The Brood", RemoteType = "warpaints", RemoteId = 77225, Prices = [new() { Id = 1218321, Amount = 30 }] },
        new() { Id = 504321, Name = "Stellar Ponies", RemoteType = "warpaints", RemoteId = 77224, Prices = [new() { Id = 1218421, Amount = 50 }] },
        new() { Id = 504421, Name = "Blood Falcons", RemoteType = "warpaints", RemoteId = 77223, Prices = [new() { Id = 1218521, Amount = 50 }] },
        new() { Id = 504521, Name = "Emerald Blades", RemoteType = "warpaints", RemoteId = 77222, Prices = [new() { Id = 1218621, Amount = 30 }] },
        new() { Id = 504621, Name = "Jack-o'-lantern", RemoteType = "warpaints", RemoteId = 77233, Prices = [new() { Id = 1218721, Amount = 30 }] },
        new() { Id = 504721, Name = "Flamingo", RemoteType = "warpaints", RemoteId = 77230, Prices = [new() { Id = 1218821, Amount = 30 }] },
        new() { Id = 504821, Name = "Team Midair", RemoteType = "warpaints", RemoteId = 77433, Prices = [new() { Id = 1218921, Amount = 50 }] },
        new() { Id = 504921, Name = "Magma", RemoteType = "warpaints", RemoteId = 77438, Prices = [new() { Id = 1219021, Amount = 30 }] },
        new() { Id = 505021, Name = "Leopard", RemoteType = "warpaints", RemoteId = 77439, Prices = [new() { Id = 1219121, Amount = 10 }] },
        new() { Id = 505721, Name = "Holly Jolly", RemoteType = "warpaints", RemoteId = 77528, Prices = [new() { Id = 1220221, Amount = 30 }] },
        new() { Id = 83201, Name = "Tech Glasses", RemoteType = "ornaments", RemoteId = 10015, Prices = [new() { Id = 171201, Amount = 20 }] },
        new() { Id = 82701, Name = "Accord Monocle", RemoteType = "ornaments", RemoteId = 10010, Prices = [new() { Id = 170701, Amount = 40 }] },
        new() { Id = 83501, Name = "Omnidyne-M Visor", RemoteType = "ornaments", RemoteId = 10018, Prices = [new() { Id = 171501, Amount = 80 }] },
        new() { Id = 83001, Name = "The Bandit", RemoteType = "ornaments", RemoteId = 10013, Prices = [new() { Id = 1224921, Amount = 200 }] },
        new() { Id = 82501, Name = "Eye Shields", RemoteType = "ornaments", RemoteId = 10008, Prices = [new() { Id = 170501, Amount = 40 }] },
        new() { Id = 83301, Name = "Astrek Full Mask", RemoteType = "ornaments", RemoteId = 10016, Prices = [new() { Id = 171301, Amount = 80 }] },
        new() { Id = 82801, Name = "Zee Goggles", RemoteType = "ornaments", RemoteId = 10011, Prices = [new() { Id = 170801, Amount = 80 }] },
        new() { Id = 83601, Name = "Astrek Monocle", RemoteType = "ornaments", RemoteId = 10019, Prices = [new() { Id = 171601, Amount = 20 }] },
        new() { Id = 83101, Name = "Astrek Headset", RemoteType = "ornaments", RemoteId = 10014, Prices = [new() { Id = 171101, Amount = 40 }] },
        new() { Id = 82601, Name = "Monoclops", RemoteType = "ornaments", RemoteId = 10009, Prices = [new() { Id = 170601, Amount = 40 }] },
        new() { Id = 83401, Name = "Aviator Goggles", RemoteType = "ornaments", RemoteId = 10017, Prices = [new() { Id = 171401, Amount = 40 }] },
        new() { Id = 82901, Name = "Model 5 Helmet", RemoteType = "ornaments", RemoteId = 10012, Prices = [new() { Id = 1225021, Amount = 200 }] },
        new() { Id = 83701, Name = "Earrings", RemoteType = "ornaments", RemoteId = 10020, Prices = [new() { Id = 171701, Amount = 20 }] },
        new() { Id = 83801, Name = "Tech Tiara", RemoteType = "ornaments", RemoteId = 10021, Prices = [new() { Id = 171801, Amount = 40 }] },
        new() { Id = 83901, Name = "Straw Hat", RemoteType = "ornaments", RemoteId = 10026, Prices = [new() { Id = 171901, Amount = 40 }] },
        new() { Id = 88401, Name = "Purple Sunglasses", RemoteType = "ornaments", RemoteId = 10007, Prices = [new() { Id = 175301, Amount = 20 }] },
        new() { Id = 88301, Name = "Accord Headset", RemoteType = "ornaments", RemoteId = 10001, Prices = [new() { Id = 175201, Amount = 20 }] },
        new() { Id = 212611, Name = "Mouth Breather", RemoteType = "ornaments", RemoteId = 10031, Prices = [new() { Id = 628111, Amount = 75 }] },
        new() { Id = 357011, Name = "Hipster Glasses", RemoteType = "ornaments", RemoteId = 10057, Prices = [new() { Id = 1224321, Amount = 30 }] },
        new() { Id = 356811, Name = "Aviators", RemoteType = "ornaments", RemoteId = 10056, Prices = [new() { Id = 1224221, Amount = 30 }] },
        new() { Id = 359411, Name = "Crystite Earrings", RemoteType = "ornaments", RemoteId = 10073, Prices = [new() { Id = 1054331, Amount = 15 }] },
        new() { Id = 588321, Name = "Scanner Helm", RemoteType = "ornaments", RemoteId = 10309, Prices = [new() { Id = 1305121, Amount = 120 }] },
        new() { Id = 588421, Name = "Environmental Helm", RemoteType = "ornaments", RemoteId = 10310, Prices = [new() { Id = 1305221, Amount = 100 }] },
        new() { Id = 588521, Name = "Breather Helm", RemoteType = "ornaments", RemoteId = 10313, Prices = [new() { Id = 1305321, Amount = 80 }] },
        new() { Id = 590021, Name = "Omnivisor Helm", RemoteType = "ornaments", RemoteId = 10314, Prices = [new() { Id = 1307621, Amount = 90 }] },
        new() { Id = 590121, Name = "Optical Visor", RemoteType = "ornaments", RemoteId = 10316, Prices = [new() { Id = 1307721, Amount = 90 }] },
        new() { Id = 114201, Name = "Crazy", RemoteType = "head_accessory", RemoteId = 10097, Prices = [new() { Id = 232601, Amount = 40 }] },
        new() { Id = 114501, Name = "Bro", RemoteType = "head_accessory", RemoteId = 10098, Prices = [new() { Id = 232901, Amount = 20 }] },
        new() { Id = 114301, Name = "Dreads", RemoteType = "head_accessory", RemoteId = 10087, Prices = [new() { Id = 232701, Amount = 40 }] },
        new() { Id = 114401, Name = "Spike", RemoteType = "head_accessory", RemoteId = 10094, Prices = [new() { Id = 232801, Amount = 10 }] },
        new() { Id = 115201, Name = "Side Swipe", RemoteType = "head_accessory", RemoteId = 10091, Prices = [new() { Id = 233701, Amount = 40 }] },
        new() { Id = 115501, Name = "Wicked Hawk", RemoteType = "head_accessory", RemoteId = 10096, Prices = [new() { Id = 234001, Amount = 10 }] },
        new() { Id = 115401, Name = "Dude", RemoteType = "head_accessory", RemoteId = 10093, Prices = [new() { Id = 233901, Amount = 20 }] },
        new() { Id = 115601, Name = "Handsome", RemoteType = "head_accessory", RemoteId = 10086, Prices = [new() { Id = 234101, Amount = 10 }] },
        new() { Id = 115701, Name = "Rad", RemoteType = "head_accessory", RemoteId = 10092, Prices = [new() { Id = 234201, Amount = 20 }] },
        new() { Id = 212011, Name = "Wild Man", RemoteType = "head_accessory", RemoteId = 10103, Prices = [new() { Id = 438111, Amount = 20 }] },
        new() { Id = 211811, Name = "Wise", RemoteType = "head_accessory", RemoteId = 10101, Prices = [new() { Id = 437911, Amount = 10 }] },
        new() { Id = 211911, Name = "5 o'clock", RemoteType = "head_accessory", RemoteId = 10102, Prices = [new() { Id = 438011, Amount = 5 }] },
        new() { Id = 211711, Name = "Hawk", RemoteType = "head_accessory", RemoteId = 10090, Prices = [new() { Id = 627811, Amount = 10 }] },
        new() { Id = 212311, Name = "Rough Stuff", RemoteType = "head_accessory", RemoteId = 10107, Prices = [new() { Id = 438411, Amount = 5 }] },
        new() { Id = 212111, Name = "Serious Dude", RemoteType = "head_accessory", RemoteId = 10104, Prices = [new() { Id = 438211, Amount = 5 }] },
        new() { Id = 212211, Name = "Spicy Fella", RemoteType = "head_accessory", RemoteId = 10105, Prices = [new() { Id = 438311, Amount = 15 }] },
        new() { Id = 452231, Name = "Wild Child", RemoteType = "head_accessory", RemoteId = 10046, Prices = [new() { Id = 993031, Amount = 20 }] },
        new() { Id = 452331, Name = "Dreads", RemoteType = "head_accessory", RemoteId = 10110, Prices = [new() { Id = 993331, Amount = 40 }] },
        new() { Id = 452431, Name = "Contemporary", RemoteType = "head_accessory", RemoteId = 10113, Prices = [new() { Id = 993531, Amount = 20 }] },
        new() { Id = 452631, Name = "Feisty", RemoteType = "head_accessory", RemoteId = 10116, Prices = [new() { Id = 993731, Amount = 40 }] },
        new() { Id = 452531, Name = "Fierce Chop", RemoteType = "head_accessory", RemoteId = 10115, Prices = [new() { Id = 994231, Amount = 40 }] },
        new() { Id = 452731, Name = "Pixie", RemoteType = "head_accessory", RemoteId = 10117, Prices = [new() { Id = 993831, Amount = 10 }] },
        new() { Id = 452831, Name = "Lovely", RemoteType = "head_accessory", RemoteId = 10119, Prices = [new() { Id = 994131, Amount = 10 }] },
        new() { Id = 451431, Name = "Seraphic", RemoteType = "head", RemoteId = 10026, Prices = [new() { Id = 994831, Amount = 20 }] },
        new() { Id = 451731, Name = "Warrior", RemoteType = "head", RemoteId = 10030, Prices = [new() { Id = 1055231, Amount = 20 }] },
        new() { Id = 451831, Name = "Bold", RemoteType = "head", RemoteId = 10031, Prices = [new() { Id = 995131, Amount = 20 }] },
        new() { Id = 452031, Name = "Bruiser", RemoteType = "head", RemoteId = 10033, Prices = [new() { Id = 994531, Amount = 20 }] },
        new() { Id = 452131, Name = "Sage", RemoteType = "head", RemoteId = 10034, Prices = [new() { Id = 994631, Amount = 20 }] },
    ];
}
