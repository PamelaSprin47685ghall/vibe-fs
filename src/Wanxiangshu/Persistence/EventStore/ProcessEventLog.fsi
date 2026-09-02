namespace Wanxiangshu.Persistence.EventStore

open System.Threading.Tasks

[<Sealed>]
type ProcessEventLog

[<Class>]
type StoreFileGate =
    member Release: unit -> Task<unit>

[<RequireQualifiedAccess>]
module ProcessEventLog =
    type WriterPhysicalMetadata =
        { Name: string
          StatIdentity: string
          LastActivityMs: float }

    val processAwareFs: obj
    val acquireStoreLock: commonDir: string -> Task<StoreFileGate>
    val withStoreLock<'T> : commonDir: string -> work: (unit -> Task<'T>) -> Task<'T>
    val create: commonDir: string -> writerId: string -> ProcessEventLog
    val writerId: log: ProcessEventLog -> string
    val filePath: log: ProcessEventLog -> string
    val append: log: ProcessEventLog -> events: EventEnvelope list -> unit
    val decodeWriterText: label: string -> text: string -> Result<EventEnvelope list, StorageInvalid>
    val readLastCompleteLine: path: string -> Result<string option, string>
    val writerRetentionMilliseconds: unit -> float
    val isWriterActiveAt: nowMs: float -> lastActivityMs: float -> bool
    val physicalFingerprint: commonDir: string -> string
    val writerPhysicalStats: commonDir: string -> (string * string) list
    val writerPhysicalMetadata: commonDir: string -> WriterPhysicalMetadata list
    val payloadPhysicalStats: commonDir: string -> (string * string) list
    val readWriterFileBytes: commonDir: string -> name: string -> byte[]
    val readPayloadFileBytes: commonDir: string -> name: string -> byte[]
    val readWriterTexts: commonDir: string -> (string * string) list

    val mergeWriterTextWithActivity:
        commonDir: string ->
        writerId: string ->
        incoming: string ->
        incomingActivityMs: float option ->
            Result<unit, string>

    val mergeWriterText: commonDir: string -> writerId: string -> incoming: string -> Result<unit, string>
    val removeWriterFile: commonDir: string -> name: string -> unit
    val readStreamsAt: commonDir: string -> nowMs: float -> Result<(string * EventEnvelope list) list, StorageInvalid>
    val readStreams: commonDir: string -> Result<(string * EventEnvelope list) list, StorageInvalid>
    val writePayload: commonDir: string -> content: byte[] -> PayloadRef
    val readPayload: commonDir: string -> payloadRef: PayloadRef -> byte[] option
    val payloadExists: commonDir: string -> payloadRef: PayloadRef -> bool
    val readPayloadFiles: commonDir: string -> (string * byte[]) list
    val mergePayloadFile: commonDir: string -> name: string -> content: byte[] -> Result<unit, string>
