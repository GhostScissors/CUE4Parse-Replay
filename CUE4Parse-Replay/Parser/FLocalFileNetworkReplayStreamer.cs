using CUE4Parse.UE4.Exceptions;
using CUE4Parse.UE4.Objects.Core.Serialization;
using CUE4Parse.UE4.Readers;
using Serilog;

namespace CUE4Parse_Replay.Parser;

public class FLocalFileNetworkReplayStreamer
{
    public uint MagicNumber;
    public ELocalFileReplayCustomVersion DoNotUse_FileVersion;
    public FLocalFileReplayInfo Info;
    public FLocalFileSerializationInfo SerializationInfo;
    
    private const uint FileMagic = 0x1CA2E27F;
    private const int MaxEncryptionKeySizeBytes = 4096;

    public FLocalFileNetworkReplayStreamer(FArchive Ar)
    {
        Info = new FLocalFileReplayInfo();
        SerializationInfo = new FLocalFileSerializationInfo();

        if (Ar.Length == 0)
            throw new ParserException(Ar, "Length is zero");
        
        MagicNumber = Ar.Read<uint>();
        DoNotUse_FileVersion = Ar.Read<ELocalFileReplayCustomVersion>();
        
        if (MagicNumber != FileMagic)
            throw new ParserException(Ar, "MagicNumber != FileMagic");

        var fileCustomVersions = new FCustomVersionContainer();

        if (DoNotUse_FileVersion >= ELocalFileReplayCustomVersion.CustomVersions)
        {
            fileCustomVersions = new FCustomVersionContainer(Ar);
        }

        SerializationInfo.FileVersion = DoNotUse_FileVersion;
        SerializationInfo.FileCustomVersions = fileCustomVersions;

        Info.LengthInMS = Ar.Read<int>();
        Info.NetworkVersion = Ar.Read<uint>();
        Info.Changelist = Ar.Read<uint>();

        var friendlyName = Ar.ReadFString();
        
        SerializationInfo.FileFriendlyName = friendlyName;

        if (SerializationInfo.GetLocalFileReplayVersion() >= ELocalFileReplayCustomVersion.FixedSizeFriendlyName)
        {
            Info.FriendlyName = friendlyName.TrimEnd();
        }
        else
        {
            throw new ParserException(Ar, "ReadReplayInfo - Loading an old replay, friendly name length must not be changed.");
        }

        Info.bIsLive = Ar.ReadBoolean();

        if (SerializationInfo.GetLocalFileReplayVersion() >= ELocalFileReplayCustomVersion.RecordingTimestamp)
        {
            Info.Timestamp = new FDateTime(Ar);
        }

        if (SerializationInfo.GetLocalFileReplayVersion() >= ELocalFileReplayCustomVersion.CompressionSupport)
        {
            Info.bCompressed = Ar.ReadBoolean();
        }

        if (SerializationInfo.GetLocalFileReplayVersion() >= ELocalFileReplayCustomVersion.EncryptionSupport)
        {
            Info.bEncrypted = Ar.ReadBoolean();

            var keyPos = Ar.Position;
            var keySize = Ar.Read<int>();

            if (keySize >= 0 && keySize <= MaxEncryptionKeySizeBytes)
            {
                Ar.Seek(keyPos, SeekOrigin.Begin);

                Info.EncryptionKey = Ar.ReadArray<byte>();
            }
            else
            {
                throw new ParserException(Ar, $"ReadReplayInfo: Serialized an invalid encryption key size: {keySize}");
            }
        }

        if (!Info.bIsLive && Info.bEncrypted && Info.EncryptionKey.Length == 0)
        {
            throw new ParserException(Ar, "ReadReplayInfo: Completed replay is marked encrypted but has no key!");
        }
        
        var totalSize = Ar.Length;

        while (Ar.Position != Ar.Length)
        {
            var typeOffset = Ar.Position;

            var chunkType = Ar.Read<ELocalFileChunkType>();
            
            Array.Resize(ref Info.Chunks, Info.Chunks.Length + 1);
            var idx = Info.Chunks.Length - 1;

            var chunk = new FLocalFileChunkInfo()
            {
                ChunkType = chunkType,
                SizeInBytes = Ar.Read<int>(),
                TypeOffset = typeOffset,
                DataOffset = Ar.Position
            };
            
            Info.Chunks[idx] = chunk;

            if ((chunk.SizeInBytes < 0) || (chunk.DataOffset + chunk.SizeInBytes) > totalSize)
                throw new ParserException(Ar, $"ReadReplayInfo: Invalid chunk size: {chunk.SizeInBytes}");

            switch (chunkType)
            {
                case ELocalFileChunkType.Header:
                {
                    if (Info.HeaderChunkIndex == -1)
                    {
                        Info.HeaderChunkIndex = idx;
                    }
                    else
                    {
                        throw new ParserException(Ar, "ReadReplayInfo: Found multiple header chunks");
                    }
                    
                    break;
                }
                case ELocalFileChunkType.Checkpoint:
                {
                    Array.Resize(ref Info.Checkpoints, Info.Checkpoints.Length + 1);
                    var checkpointIdx = Info.Checkpoints.Length - 1;

                    var checkpoint = new FLocalFileEventInfo()
                    {
                        ChunkIndex = idx,
                        Id = Ar.ReadFString(),
                        Group = Ar.ReadFString(),
                        Metadata = Ar.ReadFString(),
                        Time1 = Ar.Read<uint>(),
                        Time2 = Ar.Read<uint>(),
                        SizeInBytes = Ar.Read<int>(),
                        EventDataOffset = Ar.Position
                    };
                    
                    Info.Checkpoints[checkpointIdx] = checkpoint;

                    if (checkpoint.SizeInBytes < 0 || checkpoint.EventDataOffset + checkpoint.SizeInBytes > totalSize)
                        throw new ParserException(Ar, $"ReadReplayInfo: Invalid checkpoint disk size: {checkpoint.SizeInBytes}");
                    
                    break;
                }
                case ELocalFileChunkType.ReplayData:
                {
                    Array.Resize(ref Info.DataChunks, Info.DataChunks.Length + 1);
                    var dataIdx = Info.DataChunks.Length - 1;

                    var dataChunk = new FLocalFileReplayDataInfo()
                    {
                        ChunkIndex = idx,
                        StreamOffset = Info.TotalDataSizeInBytes
                    };

                    if (SerializationInfo.GetLocalFileReplayVersion() >= ELocalFileReplayCustomVersion.StreamChunkTimes)
                    {
                        dataChunk.Time1 = Ar.Read<uint>();
                        dataChunk.Time2 = Ar.Read<uint>();
                        dataChunk.SizeInBytes = Ar.Read<int>();
                    }
                    else
                    {
                        dataChunk.SizeInBytes = chunk.SizeInBytes;
                    }

                    if (SerializationInfo.GetLocalFileReplayVersion() < ELocalFileReplayCustomVersion.EncryptionSupport)
                    {
                        dataChunk.ReplayDataOffset = Ar.Position;

                        if (Info.bCompressed)
                        {
                            dataChunk.MemorySizeInBytes = 0;
                        }
                        else
                        {
                            dataChunk.MemorySizeInBytes = dataChunk.SizeInBytes;
                        }
                    }
                    else
                    {
                        dataChunk.MemorySizeInBytes = Ar.Read<int>();
                        dataChunk.ReplayDataOffset = Ar.Position;
                    }

                    Info.DataChunks[dataIdx] = dataChunk;

                    if (dataChunk.SizeInBytes < 0 || dataChunk.ReplayDataOffset + dataChunk.SizeInBytes > totalSize)
                        throw new ParserException(Ar, $"ReadReplayInfo: Invalid stream chunk disk size: {dataChunk.SizeInBytes}");

                    if (dataChunk.MemorySizeInBytes < 0)
                        throw new ParserException(Ar, $"ReadReplayInfo: Invalid stream chunk memory size: {dataChunk.MemorySizeInBytes}");

                    Info.TotalDataSizeInBytes += dataChunk.MemorySizeInBytes;
                    
                    break;
                }
                case ELocalFileChunkType.Event:
                {
                    Array.Resize(ref Info.Events, Info.Events.Length + 1);
                    var eventIdx = Info.Events.Length - 1;

                    var evnt = new FLocalFileEventInfo()
                    {
                        ChunkIndex = idx,
                        Id = Ar.ReadFString(),
                        Group = Ar.ReadFString(),
                        Metadata = Ar.ReadFString(),
                        Time1 = Ar.Read<uint>(),
                        Time2 = Ar.Read<uint>(),
                        SizeInBytes = Ar.Read<int>(),
                        EventDataOffset = Ar.Position
                    };
                    
                    Info.Events[eventIdx] = evnt;

                    if (evnt.SizeInBytes < 0 || evnt.EventDataOffset + evnt.SizeInBytes > totalSize)
                        throw new ParserException(Ar, $"ReadReplayInfo: Invalid event disk size: {evnt.SizeInBytes}");
                    
                    break;
                }
                case ELocalFileChunkType.Unknown:
                    Log.Verbose("ReadReplayInfo: Skipping unknown (cleared) chunk");
                    break;
                default:
                    Log.Warning("ReadReplayInfo: Unhandled file chunk type: {val}", (uint)chunkType);
                    break;
            }
            
            Ar.Seek(chunk.DataOffset + chunk.SizeInBytes, SeekOrigin.Begin);
        }

        if (SerializationInfo.GetLocalFileReplayVersion() < ELocalFileReplayCustomVersion.StreamChunkTimes)
        {
            for (var i = 0; i < Info.DataChunks.Length; i++)
            {
                var checkpointStartIdx = i - 1;

                if (Utils.IsValidIndex(checkpointStartIdx, Info.Checkpoints.Length))
                {
                    Info.DataChunks[i].Time1 = Info.Checkpoints[checkpointStartIdx].Time1;
                }
                else
                {
                    Info.DataChunks[i].Time1 = 0;
                }

                if (Utils.IsValidIndex(i, Info.Checkpoints.Length))
                {
                    Info.DataChunks[i].Time2 = Info.Checkpoints[i].Time1;
                }
                else
                {
                    Info.DataChunks[i].Time2 = (uint) Info.LengthInMS;
                }
            }
        }
    }
}

public enum ELocalFileReplayCustomVersion : uint
{
    // Before any version changes were made
    BeforeCustomVersionWasAdded = 0,

    FixedSizeFriendlyName = 1,
    CompressionSupport = 2,
    RecordingTimestamp = 3,
    StreamChunkTimes = 4,
    FriendlyNameCharEncoding = 5,
    EncryptionSupport = 6,
    CustomVersions = 7,

    // -----<new versions can be added above this line>-------------------------------------------------
    VersionPlusOne,
    LatestVersion = VersionPlusOne - 1
}

public enum ELocalFileChunkType : uint
{
    Header,
    ReplayData,
    Checkpoint,
    Event,
    Unknown = 0xFFFFFFFF
}