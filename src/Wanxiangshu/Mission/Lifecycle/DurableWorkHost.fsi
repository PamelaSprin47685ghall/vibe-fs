namespace Wanxiangshu.Mission.Lifecycle

module DurableWorkHost =
    val journalPath: gitCommonDir: string -> string

    type HostReceipt =
        { RequestId: string
          Status: string
          Kind: string
          Reason: string
          Revision: float
          Result: obj }

    val observe: requestId: string -> durableResult: obj -> HostReceipt
    val admitted: receipt: HostReceipt -> bool
    val resultText: receipt: HostReceipt -> name: string -> string
    val mayStart: durableResult: obj -> bool
    val command: kind: string -> fields: (string * obj) list -> obj
    val planSequence: unit -> obj
    val executionSequence: unit -> obj
    val useWork: gitCommonDir: string -> workId: string -> body: (obj -> 'a) -> 'a
    val useEphemeralWork: workId: string -> records: string array -> body: (obj -> 'a) -> 'a
    val execute: gitCommonDir: string -> workId: string -> requestId: string -> command: obj -> HostReceipt
    val inspect: gitCommonDir: string -> workId: string -> obj

    type HostRunFence =
        { ProviderRunId: string
          ToolCallId: string }

    type HostRetirement =
        { RetirementId: string
          IncumbencyId: string
          Outcome: string
          Fence: HostRunFence
          ProjectionCut: bool }

    type HostAdvance =
        { IncumbencyId: string
          Expected: string
          Next: string }

    type InspectedWork =
        { Opened: bool
          Sequence: string list
          Stage: string
          ActiveId: string option
          OpeningStage: string option
          Reviewed: bool
          ReviewOutcome: string option
          OpeningCursor: float option
          PredecessorRange: (float * float) option
          DeliveryAuthorized: bool
          Delivered: bool
          PendingDelivery: bool
          PendingOperationId: string option
          PendingExecSessionId: string option
          PendingDigest: string option
          PendingPath: string option
          RoundCount: int
          RetiredRuns: string list
          DevOpsId: string option
          DevOpsTarget: string option
          Authority: string
          Confirmed: bool
          BlockerDigest: string option
          Root: string option
          Retirements: HostRetirement list
          Confirmation: HostRunFence option
          ProjectionCut: HostRunFence option
          Advances: HostAdvance list }

    val inspected: gitCommonDir: string -> workId: string -> InspectedWork option

    val ensureOpened:
        gitCommonDir: string ->
        workId: string ->
        requestId: string ->
        root: string ->
        sequence: obj ->
        ownerId: string ->
        openingCursor: float ->
            Result<InspectedWork, string>

    val recover: gitCommonDir: string -> workId: string -> obj

    type OwnershipLease =
        { RegistryId: string
          ResourceId: string
          Token: string }

    type OwnershipBlocker =
        { Lease: OwnershipLease
          ScopeId: string
          Kind: string
          Phase: string
          ExternalId: string option
          IncarnationId: string option
          Outcome: string option
          Reason: string option
          CancellationRequested: bool }

    val ownershipMessage: kind: string -> fields: (string * obj) list -> obj
    val leaseObject: lease: OwnershipLease -> obj
    val bindingObject: externalId: string -> incarnationId: string -> obj
    val parseLease: value: obj -> OwnershipLease option
    val parseBlockers: raw: obj -> OwnershipBlocker list
    val createOwnership: registryId: string -> records: string array -> obj
    val executeOwnershipInMemory: ownership: obj -> requestId: string -> message: obj -> HostReceipt
    val ownershipBlockersInMemory: ownership: obj -> scopeId: string -> OwnershipBlocker list
    val ownershipScopePhaseInMemory: ownership: obj -> scopeId: string -> string option

    val executeOwnershipDurable:
        gitCommonDir: string -> registryId: string -> requestId: string -> message: obj -> HostReceipt

    val mayStartResource: receipt: HostReceipt -> OwnershipLease option
    val ownershipBlockersDurable: gitCommonDir: string -> registryId: string -> scopeId: string -> OwnershipBlocker list
