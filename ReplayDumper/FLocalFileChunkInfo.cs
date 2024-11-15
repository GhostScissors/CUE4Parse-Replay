namespace CUE4Parse_Replay.ReplayDumper;

public class FLocalFileChunkInfo
{
    public ELocalFileChunkType ChunkType = ELocalFileChunkType.Unknown;
    public int SizeInBytes;
    public long TypeOffset;
    public long DataOffset;
}