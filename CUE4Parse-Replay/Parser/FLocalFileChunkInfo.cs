namespace CUE4Parse_Replay.Parser;

public class FLocalFileChunkInfo
{
    public ELocalFileChunkType ChunkType = ELocalFileChunkType.Unknown;
    public int SizeInBytes;
    public long TypeOffset;
    public long DataOffset;
}