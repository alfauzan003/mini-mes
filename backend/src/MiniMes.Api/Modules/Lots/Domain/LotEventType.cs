namespace MiniMes.Api.Modules.Lots.Domain;

public enum LotEventType
{
    Register,
    Create,
    TrackIn,
    TrackOut,
    CarrierLoad,
    CarrierUnload,
    Inspect,
    Hold,
    Release,
    Scrap,
    Finish
}
