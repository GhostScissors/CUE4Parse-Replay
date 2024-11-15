namespace CUE4Parse_Replay.ReplayDumper;

public class FLocalFileReplayInfo
{
    public int LengthInMS;
    public uint NetworkVersion;
    public uint Changelist;
    public string FriendlyName;
    public FDateTime Timestamp;
    public long TotalDataSizeInBytes;
    public bool bIsLive;
    public bool bIsValid;
    public bool bCompressed;
    public bool bEncrypted;
    public byte[] EncryptionKey = [];
    public int HeaderChunkIndex = -1;
    public FLocalFileChunkInfo[] Chunks = [];
    public FLocalFileEventInfo[] Checkpoints = [];
    public FLocalFileEventInfo[] Events = [];
    public FLocalFileReplayDataInfo[] DataChunks = [];
}