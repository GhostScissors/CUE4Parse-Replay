﻿using CUE4Parse.UE4.Readers;

namespace CUE4Parse_Replay.ReplayDumper;

public class FDateTime
{
    public long Ticks;

    public FDateTime(FArchive Ar)
    {
        Ticks = Ar.Read<long>();
    }
}