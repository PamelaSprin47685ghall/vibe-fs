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
    let empty = { Cuts = [] }

    let cutFor (eventId: EventId) (receipt: AppendReceipt) =
        receipt.Cuts |> List.tryFind (fun cut -> cut.FailedEventId = eventId)

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
    let semanticCuts (error: AppendError) =
        match error with
        | AppendError.SemanticCut cut -> [ cut ]
        | AppendError.CommitUnknown evidence -> evidence.Prepared.Cuts
        | AppendError.StorageInvalid _
        | AppendError.AppendFailed _
        | AppendError.AppendNotAttempted _
        | AppendError.NoNewWriteReleaseFailed _ -> []

    let cause (error: AppendError) =
        match error with
        | AppendError.AppendNotAttempted evidence -> Some evidence.Primary.Cause
        | AppendError.CommitUnknown evidence -> Some evidence.Primary.Cause
        | AppendError.NoNewWriteReleaseFailed evidence -> Some evidence.Cause
        | AppendError.StorageInvalid _
        | AppendError.SemanticCut _
        | AppendError.AppendFailed _ -> None

    let describe (error: AppendError) =
        match error with
        | AppendError.StorageInvalid detail -> sprintf "storage invalid: %A" detail
        | AppendError.SemanticCut cut -> sprintf "semantic cut %s: %s" cut.Rule cut.Reason
        | AppendError.AppendFailed reason -> "append failed: " + reason
        | AppendError.AppendNotAttempted evidence ->
            sprintf "append not attempted at %A: %O" evidence.Primary.Phase evidence.Primary.Cause
        | AppendError.CommitUnknown evidence ->
            sprintf "commit unknown at %A: %O" evidence.Primary.Phase evidence.Primary.Cause
        | AppendError.NoNewWriteReleaseFailed evidence ->
            sprintf "no new write; store release failed: %O" evidence.Cause
