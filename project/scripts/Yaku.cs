using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public delegate bool EvaluateHandHandler(ReadOnlySpan<TileInfo> sortedHand, ReadOnlySpan<int> tileOccurrences);
public delegate bool EvaluateYakuHandler(ReadOnlySpan<TileInfo> sortedHand, ReadOnlySpan<int> tileOccurrences, PlayerInfo playerInfo, TableInfo tableInfo);

public record Yaku(string Name, int HanValue, EvaluateYakuHandler EvaluateYakuHandler, RemarkFlags Remarks = RemarkFlags.Always);

[Flags]
public enum RemarkFlags
{
    Always,
    ClosedHandOnly = 1,
    RequiresRiichi = 2,
    DealerHandOnly = 4,
    NonDealerHandOnly = 8,
}

public class PlayerInfo
{
    public Seat Seat { get; set; }
    public bool IsHandClosed { get; set; }
    public bool CalledRiichi { get; set; }

    public bool IsDealer => Seat == Seat.East;
}

public class TableInfo
{

}

public enum Seat: byte
{
    North,
    East,
    South,
    West
}

// TODO: move contents to better location and actually give it more love & care
class Game
{
    Yaku[] _enabledYakus = [
        new Yaku("Thirteen Orphans", 8, (hand, occurrences, _, _) => IsThirteenOrphans(hand, occurrences))
    ];

    public bool IsWinningHand(ReadOnlySpan<TileInfo> hand, out IReadOnlyList<Yaku> activeYakus, PlayerInfo playerInfo, TableInfo tableInfo)
    {
        Span<TileInfo> sortedHand = stackalloc TileInfo[hand.Length];
        hand.CopyTo(sortedHand);
        sortedHand.Sort();

        Span<int> tileOccurrenceBuffer = stackalloc int[14];
        tileOccurrenceBuffer.Fill(1);
        int differentTiles = 0;

        // group tiles together by type and value
        var currentTile = sortedHand[0];
        for (int i = 1; i < sortedHand.Length; i++)
        {
            if (currentTile.PairsWith(sortedHand[i]))
            {
                tileOccurrenceBuffer[differentTiles]++;
                continue;
            }

            currentTile = sortedHand[i];
            differentTiles++;
        }
        differentTiles++;

        ReadOnlySpan<int> tileOccurrences = tileOccurrenceBuffer[..differentTiles];

        // at this point we have 2 spans, one containing the sorted tiles
        // and one containing how many times a specific tile appears
        // because of this, you can use math to traverse the sorted tiles to detect sequences


        if (!IsCompleteHand(sortedHand, tileOccurrences))
        {
            activeYakus = [];
            return false;
        }

        List<Yaku> foundYaku = new();
        foreach (var yaku in _enabledYakus)
        {
            if (yaku.EvaluateYakuHandler(sortedHand, tileOccurrences, playerInfo, tableInfo))
            {
                foundYaku.Add(yaku);
            }
        }

        activeYakus = foundYaku;
        return foundYaku.Count != 0;
    }

    public bool IsCompleteHand(ReadOnlySpan<TileInfo> sortedHand, ReadOnlySpan<int> tileOccurrences)
    {
        if (CanFormHand(sortedHand, tileOccurrences))
        {
            return true;
        }

        if (IsSevenPairs(sortedHand, tileOccurrences))
        {
            return true;
        }

        if (IsThirteenOrphans(sortedHand, tileOccurrences))
        {
            return true;
        }

        return false;
    }

    private static void CollapseOccurrences(ref Span<int> occurrences)
    {
        int count = 0;
        for (int i = 0; i < occurrences.Length; i++)
        {
            if (occurrences[i] != 0)
                occurrences[count++] = occurrences[i];
        }

        occurrences = occurrences[..count];
    }

    public static bool CanFormHand(ReadOnlySpan<TileInfo> sortedHand, ReadOnlySpan<int> tileOccurrences)
    {
        if (sortedHand.Length == 2)
            return sortedHand[0].PairsWith(sortedHand[1]);

        var (handIndex, occurrenceIndex) = FindTriplet(sortedHand, tileOccurrences);
        if (handIndex != -1)
        {
            Span<TileInfo> remainingTiles = stackalloc TileInfo[sortedHand.Length - 3];
            sortedHand[..handIndex].CopyTo(remainingTiles);
            sortedHand[(handIndex + 2)..].CopyTo(remainingTiles[handIndex..]);

            Span<int> remainingTileOccurrences = stackalloc int[tileOccurrences.Length];
            tileOccurrences.CopyTo(remainingTileOccurrences);
            remainingTileOccurrences[occurrenceIndex] -= 3;
            CollapseOccurrences(ref remainingTileOccurrences);

            if (CanFormHand(remainingTiles, remainingTileOccurrences))
            {
                return true;
            }
        }

        (handIndex, occurrenceIndex) = FindMendSequence(sortedHand, tileOccurrences);
        if (handIndex != -1)
        {
            Span<TileInfo> remainingTiles = stackalloc TileInfo[sortedHand.Length - 3];

            Span<int> remainingTileOccurrences = stackalloc int[tileOccurrences.Length];
            tileOccurrences.CopyTo(remainingTileOccurrences);
            remainingTileOccurrences[occurrenceIndex] -= 1;
            remainingTileOccurrences[occurrenceIndex + 1] -= 1;
            remainingTileOccurrences[occurrenceIndex + 2] -= 1;

            int index = 0;
            int insertIndex = 0;
            for (int i = 0; i < tileOccurrences.Length; i++)
            {
                int remainingOccurrenceCount = remainingTileOccurrences[i];

                for (int j = 0; j < remainingOccurrenceCount; j++)
                {
                    remainingTiles[insertIndex++] = sortedHand[index + j];
                }

                // determine if the index skips entries
                index += tileOccurrences[i] - remainingOccurrenceCount;
            }

            CollapseOccurrences(ref remainingTileOccurrences);

            if (CanFormHand(remainingTiles, remainingTileOccurrences))
            {
                return true;
            }
        }

        return false;
    }

    static readonly int[] SEVEN_PAIR_TILE_OCCURRENCE_SEQUENCE = [2, 2, 2, 2, 2, 2, 2];

    public static bool IsSevenPairs(ReadOnlySpan<TileInfo> _, ReadOnlySpan<int> tileOccurrences)
    {
        return tileOccurrences.Length == 7 && tileOccurrences.SequenceEqual(SEVEN_PAIR_TILE_OCCURRENCE_SEQUENCE);
    }

    static readonly TileInfo[] THIRTHEEN_ORPHANS_SEQUENCE = [
        new TileInfo(TileValue.Manzu | TileValue.One),
        new TileInfo(TileValue.Manzu | TileValue.Nine),
        new TileInfo(TileValue.Pinzu | TileValue.One),
        new TileInfo(TileValue.Pinzu | TileValue.Nine),
        new TileInfo(TileValue.Sozu | TileValue.One),
        new TileInfo(TileValue.Sozu | TileValue.Nine),
        new TileInfo(TileValue.EastWind),
        new TileInfo(TileValue.SouthWind),
        new TileInfo(TileValue.WestWind),
        new TileInfo(TileValue.NorthWind),
        new TileInfo(TileValue.WhiteDragon),
        new TileInfo(TileValue.GreenDragon),
        new TileInfo(TileValue.RedDragon)
    ];

    public static bool IsThirteenOrphans(ReadOnlySpan<TileInfo> sortedHand, ReadOnlySpan<int> tileOccurrences)
    {
        if (tileOccurrences.Length != 13)
            return false;

        int pairIndex = FindPair(sortedHand, tileOccurrences).handIndex;


        Span<TileInfo> tiles = stackalloc TileInfo[13];
        sortedHand[..pairIndex].CopyTo(tiles);
        sortedHand[(pairIndex + 1)..].CopyTo(tiles[pairIndex..]);

        return tiles.SequenceEqual(THIRTHEEN_ORPHANS_SEQUENCE);
    }

    public static (int handIndex, int occurrenceIndex) FindOccurrence(ReadOnlySpan<TileInfo> _, ReadOnlySpan<int> tileOccurrences, int occurrenceCount)
    {
        int index = 0;
        for (int i = 0; i < tileOccurrences.Length; i++)
        {
            if (tileOccurrences[i] == occurrenceCount)
            {
                return (index, i);
            }

            index += tileOccurrences[i];
        }

        return (-1, -1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (int handIndex, int occurrenceIndex) FindPair(ReadOnlySpan<TileInfo> sortedHand, ReadOnlySpan<int> tileOccurrences) =>
        FindOccurrence(sortedHand, tileOccurrences, 2);


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (int handIndex, int occurrenceIndex) FindTriplet(ReadOnlySpan<TileInfo> sortedHand, ReadOnlySpan<int> tileOccurrences) =>
        FindOccurrence(sortedHand, tileOccurrences, 3);

    public static (int handIndex, int occurrenceIndex) FindSequence(ReadOnlySpan<TileInfo> sortedHand, ReadOnlySpan<int> tileOccurrences, int desiredLength)
    {
        TileInfo lastTile = default;
        int sequenceLength = default;
        int handIndex = 0;
        (int, int) startIndices = default;
        for (int i = 0; i < tileOccurrences.Length; i++)
        {
            var tile = sortedHand[handIndex];

            // because honor tiles are at the end of the sorted hand,
            // we stop early as they cannot make a sequence.
            if (tile.IsHonorTile)
                return (-1, -1);

            if (lastTile.GetSuitType == tile.GetSuitType && tile.GetSuitValue - lastTile.GetSuitValue == 1)
            {
                sequenceLength++;

                if (sequenceLength == desiredLength)
                    return startIndices;
            }
            else
            {
                startIndices = (handIndex, i);
                sequenceLength = 1;
            }

            lastTile = tile;
            handIndex += tileOccurrences[i];
        }

        return (-1, -1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (int handIndex, int occurrenceIndex) FindMendSequence(ReadOnlySpan<TileInfo> sortedHand, ReadOnlySpan<int> tileOccurrences) =>
        FindSequence(sortedHand, tileOccurrences, 3);
}

