using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Objects.Core.Serialization;

namespace CUE4Parse_Replay;

public class FLocalFileSerializationInfo
{
    public ELocalFileReplayCustomVersion FileVersion;
    public string FileFriendlyName;
    public FCustomVersionContainer FileCustomVersions;

    private FGuid Guid = new FGuid(0x95A4f03E, 0x7E0B49E4, 0xBA43D356, 0x94FF87D9);

    public ELocalFileReplayCustomVersion GetLocalFileReplayVersion()
    {
        return (ELocalFileReplayCustomVersion)FileCustomVersions.GetVersion(Guid);
    }
}