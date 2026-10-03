namespace MiniMes.Api.Shared.Results;

public static class ErrorCodes
{
    public const string WoNotActive = "WO_NOT_ACTIVE";
    public const string WoNotEditable = "WO_NOT_EDITABLE";
    public const string WoInvalidTransition = "WO_INVALID_TRANSITION";
    public const string WoInvalidAssignment = "WO_INVALID_ASSIGNMENT";
    public const string InvalidDateRange = "INVALID_DATE_RANGE";
    public const string EquipmentNotAvailable = "EQUIPMENT_NOT_AVAILABLE";
    public const string EquipmentInvalidTransition = "EQUIPMENT_INVALID_TRANSITION";
    public const string EquipmentNotAssigned = "EQUIPMENT_NOT_ASSIGNED";
    public const string LotNotAvailable = "LOT_NOT_AVAILABLE";
    public const string LotQualityPending = "LOT_QUALITY_PENDING";
    public const string RouteViolation = "ROUTE_VIOLATION";
    public const string LotWoMismatch = "LOT_WO_MISMATCH";
    public const string PolarityMismatch = "POLARITY_MISMATCH";
    public const string InvalidInputSet = "INVALID_INPUT_SET";
    public const string InvalidOutputSet = "INVALID_OUTPUT_SET";
    public const string InvalidQuantity = "INVALID_QUANTITY";
    public const string CarrierTypeMismatch = "CARRIER_TYPE_MISMATCH";
    public const string CarrierNotEmpty = "CARRIER_NOT_EMPTY";
    public const string QtyExceedsLot = "QTY_EXCEEDS_LOT";
    public const string RunNotOpen = "RUN_NOT_OPEN";
    public const string ScanNotResolved = "SCAN_NOT_RESOLVED";
    public const string LotSequenceExhausted = "LOT_SEQUENCE_EXHAUSTED";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
    public const string AuthInvalidCredentials = "AUTH_INVALID_CREDENTIALS";
    public const string NoInspectionSpec = "NO_INSPECTION_SPEC";
    public const string InvalidMeasurements = "INVALID_MEASUREMENTS";
    public const string DefectRequired = "DEFECT_REQUIRED";
    public const string DefectCodeNotFound = "DEFECT_CODE_NOT_FOUND";
    public const string ReasonRequired = "REASON_REQUIRED";
    public const string ReasonTooLong = "REASON_TOO_LONG";
    public const string InvalidSpecLimits = "INVALID_SPEC_LIMITS";
    public const string WoNotFound = "WO_NOT_FOUND";
    public const string LotNotFound = "LOT_NOT_FOUND";
    public const string CarrierNotFound = "CARRIER_NOT_FOUND";
    public const string EquipmentNotFound = "EQUIPMENT_NOT_FOUND";
    public const string ProductNotFound = "PRODUCT_NOT_FOUND";
    public const string MaterialNotFound = "MATERIAL_NOT_FOUND";
    public const string RunNotFound = "RUN_NOT_FOUND";
    public const string SpecNotFound = "SPEC_NOT_FOUND";
    public const string AlarmCodeNotFound = "ALARM_CODE_NOT_FOUND";
    public const string AlarmNotFound = "ALARM_NOT_FOUND";
    public const string AlarmAlreadyAcknowledged = "ALARM_ALREADY_ACKNOWLEDGED";
    public const string AlarmNotActive = "ALARM_NOT_ACTIVE";
}
