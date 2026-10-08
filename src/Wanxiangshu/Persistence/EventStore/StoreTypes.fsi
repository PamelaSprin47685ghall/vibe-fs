namespace Wanxiangshu.Persistence.EventStore

open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type StorageInvalid =
    | IdentityCollision of EventId
    | NonCanonical of reason: string
    | MalformedEnvelope of reason: string
    | MissingParent of EventId
    | CyclicParents
    | MissingPayload of PayloadRef
    | UnknownEventType of eventType: string

[<RequireQualifiedAccess>]
type DomainConflict = ConcurrentHeads of streamId: EventStreamId * heads: EventId list

type SemanticCut =
    { Rule: string
      FailedEventId: EventId
      Reason: string
      CutEventId: EventId }

type AppendReceipt = { Cuts: SemanticCut list }

[<RequireQualifiedAccess>]
module AppendReceipt =
    val empty: AppendReceipt
    val cutFor: eventId: EventId -> receipt: AppendReceipt -> SemanticCut option

[<RequireQualifiedAccess>]
type AppendPhase =
    | GateAcquire
    | Preparation
    | BeforePhysicalAppend
    | PhysicalAppend
    | DurabilityOpen
    | DurabilityBarrier
    | DurabilityClose
    | CurrentCommit
    | StoreRelease

type PreparedAppend =
    { DurableEvents: EventEnvelope list
      Cuts: SemanticCut list }

type AppendFault = { Phase: AppendPhase; Cause: exn }

[<RequireQualifiedAccess>]
type AppendPreWriteRejection =
    | StorageInvalid of StorageInvalid
    | PreparationRejected of reason: string

type AppendNotAttemptedEvidence =
    { Requested: EventEnvelope list
      Prepared: PreparedAppend option
      Primary: AppendFault
      CleanupFailures: AppendFault list
      PriorRejection: AppendPreWriteRejection option }

type AppendCommitUnknownEvidence =
    { Requested: EventEnvelope list
      Prepared: PreparedAppend
      Primary: AppendFault
      CleanupFailures: AppendFault list }

type AppendNoNewWriteReleaseFailure =
    { Requested: EventEnvelope list
      Prepared: PreparedAppend option
      Cause: exn }

[<RequireQualifiedAccess>]
type AppendError =
    | StorageInvalid of StorageInvalid
    | SemanticCut of SemanticCut
    | AppendFailed of reason: string
    | AppendNotAttempted of AppendNotAttemptedEvidence
    | CommitUnknown of AppendCommitUnknownEvidence
    | NoNewWriteReleaseFailed of AppendNoNewWriteReleaseFailure

[<RequireQualifiedAccess>]
module AppendError =
    val semanticCuts: error: AppendError -> SemanticCut list
    val cause: error: AppendError -> exn option
    val describe: error: AppendError -> string
