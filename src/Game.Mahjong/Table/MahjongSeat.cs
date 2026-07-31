namespace Game.Mahjong.Table;

public enum MahjongSeat
{
    East,
    South,
    West,
    North,
}

public static class MahjongSeats
{
    public static MahjongSeat Next(this MahjongSeat seat)
    {
        Validate(seat);
        return (MahjongSeat)(((int)seat + 1) % 4);
    }

    public static int DistanceFrom(this MahjongSeat seat, MahjongSeat origin)
    {
        Validate(seat);
        Validate(origin);
        return ((int)seat - (int)origin + 4) % 4;
    }

    private static void Validate(MahjongSeat seat)
    {
        if (!Enum.IsDefined(seat))
        {
            throw new ArgumentOutOfRangeException(nameof(seat));
        }
    }
}
