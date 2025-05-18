using System;

public record struct TileInfo(TileValue Value) : IComparable<TileInfo>
{
    public readonly bool IsHonorTile => (Value & TileValue.SuitMask) == 0;
    public readonly bool IsSuitTile => (Value & TileValue.SuitMask) != 0;
    public readonly bool IsAkaDora => IsSuitTile && (Value & TileValue.AkaDoraMask) == TileValue.AkaDora;
    public readonly TileValue GetSuitType => Value & TileValue.SuitMask;
    public readonly TileValue GetHonorType => Value & TileValue.HonorMask;
    public readonly TileValue GetSuitValue => Value & TileValue.ValueMask;

    public readonly int CompareTo(TileInfo other)
    {
        if (this.IsHonorTile && other.IsHonorTile)
        {
            return this.Value - other.Value;
        }

        if (this.IsSuitTile && other.IsHonorTile)
        {
            return -1;
        }

        if (this.IsHonorTile && other.IsSuitTile)
        {
            return 1;
        }

        return (this.Value & ~TileValue.AkaDoraFlag) - (other.Value & ~TileValue.AkaDoraFlag);
    }

    public readonly bool PairsWith(TileInfo other)
    {
        return (this.Value & other.Value ^ ~TileValue.AkaDoraMask) == 0;
    }
}

[Flags]
public enum TileValue : byte
{
    One = 1,
    Two = 2,
    Three = 3,
    Four = 4,
    Five = 5,
    Six = 6,
    Seven = 7,
    Eight = 8,
    Nine = 9,
    ValueMask = 15,

    AkaDoraFlag = 16,
    AkaDoraMask = 16,
    AkaDora = AkaDoraFlag | Five,

    Manzu = 32,
    Pinzu = 64,
    Sozu = 96,
    SuitMask = 96,

    Wind = 0,
    Dragon = 128,
    HonorMask = 128,

    EastWind = Wind | One,
    SouthWind = Wind | Two,
    WestWind = Wind | Three,
    NorthWind = Wind | Four,

    WhiteDragon = Dragon | One,
    GreenDragon = Dragon | Two,
    RedDragon = Dragon | Three
}
