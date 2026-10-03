// TypeScript mirrors of the backend DTOs. The API serializes enums as SCREAMING_SNAKE strings,
// decimals and ints as numbers, Guids as strings and DateTimeOffsets as ISO-8601 strings.

export type Role = 'PLANNER' | 'OPERATOR' | 'QC' | 'ADMIN'
export type Polarity = 'CATHODE' | 'ANODE'
export type OperationCode = 'MIX' | 'COAT' | 'CAL' | 'SLIT'
export type LotType = 'RAW' | 'FOIL' | 'SLURRY' | 'ELECTRODE' | 'PANCAKE'
export type LotStatus = 'WAIT' | 'RUN' | 'HOLD' | 'CONSUMED' | 'SCRAPPED' | 'FINISHED'
export type QualityStatus = 'NONE' | 'PASS' | 'FAIL'
export type LotEventType =
  | 'REGISTER'
  | 'CREATE'
  | 'TRACK_IN'
  | 'TRACK_OUT'
  | 'CARRIER_LOAD'
  | 'CARRIER_UNLOAD'
  | 'INSPECT'
  | 'HOLD'
  | 'RELEASE'
  | 'SCRAP'
  | 'FINISH'
export type WorkOrderStatus = 'PLANNED' | 'RELEASED' | 'RUNNING' | 'HOLD' | 'COMPLETED'
export type EquipmentStatus = 'IDLE' | 'RUNNING' | 'DOWN' | 'MAINTENANCE'
export type CarrierStatus = 'EMPTY' | 'FULL'
export type RunInputRole = 'PRIMARY' | 'SECONDARY'

// Identity
export interface LoginRequest {
  username: string
  password: string
}

export interface DemoLoginRequest {
  username: string
}

export interface LoginResponse {
  token: string
  username: string
  displayName: string
  role: Role
  expiresAt: string
}

export interface UserDto {
  username: string
  displayName: string
  role: Role
}

// Products and materials
export interface RouteStepDto {
  operation: OperationCode
  name: string
  seq: number
  uom: string
}

export interface ProductDto {
  code: string
  name: string
  polarity: Polarity
  route: RouteStepDto[]
}

export interface MaterialDto {
  code: string
  name: string
  kind: LotType
  polarity: Polarity
  uom: string
}

export interface RegisterMaterialRequest {
  materialCode: string
  qty: number
}

// Lots and genealogy
export interface LotDto {
  lotId: string
  type: LotType
  polarity: Polarity
  productCode: string | null
  materialCode: string | null
  workOrderNumber: string | null
  qty: number
  uom: string
  status: LotStatus
  quality: QualityStatus
  currentOperation: OperationCode | null
  nextOperation: OperationCode | null
  currentEquipment: string | null
  currentCarrier: string | null
  createdAt: string
}

export interface LotEventDto {
  id: number
  type: LotEventType
  operation: OperationCode | null
  equipment: string | null
  carrier: string | null
  runId: string | null
  user: string
  qty: number | null
  note: string | null
  occurredAt: string
}

export interface GenealogyEdge {
  parentLotId: string
  childLotId: string
}

export interface GenealogyGraph {
  rootLotId: string
  nodes: LotDto[]
  edges: GenealogyEdge[]
}

// Carriers
export interface CarrierDto {
  code: string
  type: string
  status: CarrierStatus
  lotId: string | null
}

// Work orders
export interface WorkOrderOperationDto {
  id: string
  operation: OperationCode
  seq: number
  equipmentCode: string
  runCount: number
  outputQty: number
}

export interface WorkOrderDto {
  id: string
  number: string
  productCode: string
  productName: string
  polarity: Polarity
  targetQty: number
  goodCount: number
  status: WorkOrderStatus
  plannedStart: string
  plannedEnd: string
  operations: WorkOrderOperationDto[]
}

export interface OperationAssignment {
  operation: OperationCode
  equipmentCode: string
}

export interface CreateWorkOrderRequest {
  productCode: string
  targetQty: number
  plannedStart: string
  plannedEnd: string
  operations: OperationAssignment[]
}

export interface UpdateWorkOrderRequest {
  targetQty: number
  plannedStart: string
  plannedEnd: string
  operations: OperationAssignment[]
}

// Execution
export interface RunInputDto {
  lotId: string
  type: LotType
  role: RunInputRole
  qty: number
  uom: string
  consumedQty: number | null
}

export interface RunOutputDto {
  lotId: string | null
  carrierCode: string | null
  lane: number | null
  goodQty: number
  rejectQty: number
}

export interface RunDto {
  id: string
  equipmentCode: string
  workOrderNumber: string
  workOrderOperationId: string
  operation: OperationCode
  operator: string
  startedAt: string
  endedAt: string | null
  goodQty: number
  rejectQty: number
  parentLotId: string | null
  inputs: RunInputDto[]
  outputs: RunOutputDto[]
}

export interface TrackInRequest {
  equipmentCode: string
  workOrderOperationId: string
  inputs: string[]
}

export interface OutputLine {
  carrierCode: string | null
  lane: number | null
  goodQty: number
  rejectQty: number
}

export interface ProduceOutputRequest {
  outputs: OutputLine[]
}

export interface Consumption {
  lotId: string
  consumedQty: number
}

export interface TrackOutRequest {
  consumptions: Consumption[] | null
}

// Equipment
export interface EquipmentDto {
  code: string
  name: string
  operation: OperationCode
  laneCount: number | null
  status: EquipmentStatus
  openRun: RunDto | null
}

export interface AssignmentDto {
  workOrderOperationId: string
  workOrderNumber: string
  productCode: string
  status: WorkOrderStatus
  targetQty: number
  goodCount: number
}

// Quality
export type Judgment = 'OK' | 'NG'
export type InspectionResult = 'PASS' | 'FAIL'
export type Disposition = 'RELEASE' | 'SCRAP'

/** Limits are inclusive: a value equal to LSL or USL is OK. */
export interface InspectionSpecDto {
  id: string
  productCode: string
  operation: OperationCode
  itemName: string
  unit: string
  lsl: number
  usl: number
  seq: number
}

export interface UpdateSpecLimitsRequest {
  lsl: number
  usl: number
}

/** operation is null for a general code that applies to every operation. */
export interface DefectCodeDto {
  code: string
  description: string
  operation: OperationCode | null
}

export interface MeasurementDto {
  itemName: string
  unit: string
  lsl: number
  usl: number
  value: number
  judgment: Judgment
}

export interface InspectionDto {
  id: string
  lotId: string
  operation: OperationCode
  inspector: string
  inspectedAt: string
  result: InspectionResult
  defectCode: string | null
  defectDescription: string | null
  reason: string | null
  rejectQty: number | null
  disposition: Disposition | null
  dispositionBy: string | null
  dispositionAt: string | null
  dispositionReason: string | null
  measurements: MeasurementDto[]
}

export interface MeasurementInput {
  specId: string
  value: number
}

export interface RecordInspectionRequest {
  measurements: MeasurementInput[]
  defectCode: string | null
  reason: string | null
  rejectQty: number | null
}

export interface HoldLotRequest {
  reason: string
}

export interface DispositionRequest {
  decision: Disposition
  reason: string
}

// Errors (RFC 7807 ProblemDetails with an errorCode extension)
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  errorCode?: string
}

// Alarms
export type AlarmSeverity = 'WARNING' | 'MAJOR' | 'CRITICAL'

export interface AlarmDto {
  id: string
  equipmentCode: string
  code: string
  message: string
  severity: AlarmSeverity
  raisedAt: string
  clearedAt: string | null
  /** Seconds from raise to clear; null while the alarm is still active. */
  durationSeconds: number | null
  acknowledgedBy: string | null
  acknowledgedAt: string | null
}

// Machine parameters and readings
export type ParameterKind = 'TEMPERATURE' | 'SPEED' | 'PRESSURE'

/** Latest value of one equipment parameter; also the ParameterReading real-time payload. */
export interface LiveReadingDto {
  equipmentCode: string
  parameter: string
  kind: ParameterKind
  unit: string
  value: number
  low: number
  high: number
  at: string
}

export interface ReadingPointDto {
  at: string
  value: number
}

export interface ParameterSeriesDto {
  parameter: string
  kind: ParameterKind
  unit: string
  low: number
  high: number
  points: ReadingPointDto[]
}

export interface EquipmentStatusLogDto {
  from: EquipmentStatus
  to: EquipmentStatus
  reason: string
  changedAt: string
}

// Real-time hub payloads
export interface EquipmentStatusEvent {
  code: string
  status: EquipmentStatus
}

export interface LotChangedEvent {
  lotId: string
  status: LotStatus
  quality: QualityStatus
}

export interface WorkOrderProgressEvent {
  id: string
  number: string
  status: WorkOrderStatus
  goodCount: number
  targetQty: number
}
