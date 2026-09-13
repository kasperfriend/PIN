using AeroMessages.GSS.Character;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.dbcharacter;
using Xunit;

namespace GameServer.Tests;

public class MonsterVisualOptionsMathTests
{
    [Fact]
    public void Select_PicksOneOfEachTypeFromTheSeed()
    {
        var header = new MonsterVisualOptions { Id = 1, Female = 0, Male = 2 };
        var options = new[]
        {
            new MonsterVisualOption { Parent = 1, Type = 0, Value = 10 },
            new MonsterVisualOption { Parent = 1, Type = 0, Value = 11 },
            new MonsterVisualOption { Parent = 1, Type = 1, Value = 20 },
            new MonsterVisualOption { Parent = 1, Type = 1, Value = 21 },
            new MonsterVisualOption { Parent = 1, Type = 2, Value = 30 },
        };

        var first = MonsterVisualOptionsMath.Select(header, options, female: false, seed: 7);
        var again = MonsterVisualOptionsMath.Select(header, options, female: false, seed: 7);

        Assert.Equal(3, first.Count);
        Assert.Equal(first[0].Value, again[0].Value);
        Assert.Equal(first[1].Value, again[1].Value);
        Assert.Equal(first[2].Value, again[2].Value);
        Assert.Contains(first, row => row.Type == 0 && (row.Value == 10 || row.Value == 11));
        Assert.Contains(first, row => row.Type == 1 && (row.Value == 20 || row.Value == 21));
        Assert.Contains(first, row => row.Type == 2 && row.Value == 30);
    }

    [Fact]
    public void Select_EmptyWhenThisGenderHasNoVariants()
    {
        var header = new MonsterVisualOptions { Id = 1, Female = 0, Male = 2 };
        var options = new[] { new MonsterVisualOption { Type = 0, Value = 10 } };

        Assert.Empty(MonsterVisualOptionsMath.Select(header, options, female: true, seed: 1));
    }

    [Fact]
    public void Apply_WritesHeadAndSkinColor()
    {
        var info = new StaticInfoData
        {
            HeadMain = 1,
            Visuals = new VisualsBlock
            {
                Decals = [],
                Gradients = [],
                Colors = [0x11111111, 0x22222222],
                Palettes = [],
                Patterns = [],
                OrnamentGroupIds = [],
                CziMapAssetIds = [],
                MorphWeights = [],
                Overlays = [],
            },
        };

        MonsterVisualOptionsMath.Apply(info,
        [
            new MonsterVisualOption { Type = MonsterVisualOptionsMath.HeadType, Value = 99 },
            new MonsterVisualOption { Type = MonsterVisualOptionsMath.ColorType, Value = 0xAABBCCDD },
            new MonsterVisualOption { Type = 2, Value = 7 },
        ]);

        Assert.Equal(99u, info.HeadMain);
        Assert.Equal(0xAABBCCDDu, info.Visuals.Colors[0]);
        Assert.Equal(0x22222222u, info.Visuals.Colors[1]);
    }
}
