namespace Wanxiangshu.Mission.Lifecycle

open System.Threading.Tasks
open Wanxiangshu.Persistence.EventStore

[<RequireQualifiedAccess>]
type CommitOutcome =
    | AppendDurable
    | AppendNotAttempted
    | CommitUnknown

[<RequireQualifiedAccess>]
type KernelDecision =
    | Refuse of string
    | Write of LifecycleEvent list * string
    | Done of string

type HostFence =
    { ProviderRunId: string option
      ToolCallId: string option }

type KnownSession =
    { ExecSessionId: string
      WorkId: string option }

[<RequireQualifiedAccess>]
type SessionLookup =
    | Unknown
    | None
    | One of KnownSession
    | Many

type DeliverDraft =
    { Missing: bool
      Empty: bool
      Digest: string
      Path: string }

[<RequireQualifiedAccess>]
module LifecycleKernel =
    val classifyCommit: Result<unit, AppendError> -> Result<CommitOutcome, string>
    val commit: IEventStore -> string -> LifecycleEvent list -> Task<Result<CommitOutcome, string>>
    /// The unified lifecycle work already folded into a held store. A missing
    /// current or a missing work is None; the caller keeps the store for commit.
    val tryWorkOfStore: store: IEventStore -> workId: string -> LifecycleWork option
    /// Every unified lifecycle work folded into a held store. A missing current
    /// yields None; an empty fold yields Some empty.
    val tryWorksOfStore: store: IEventStore -> Map<string, LifecycleWork> option
    val openWork: (string -> string) -> OpeningKind -> firstPromptLine: string option -> workId: string -> root: string -> alreadyOpened: bool -> observedHead: int64 option -> KernelDecision
    val bindDevOps: LifecycleWork -> BindTrigger -> devopsId: string -> target: string option -> KernelDecision
    val review: LifecycleWork -> incumbencyId: string -> ReviewOutcome -> KernelDecision
    val deliverOneshot:
        (string -> string)
        -> LifecycleWork
        -> HostFence
        -> observedHead: int64 option
        -> blockerDigest: string option
        -> SessionLookup
        -> DeliverDraft option
        -> startedSessionId: string option
        -> KernelDecision
    val deliverExecution:
        (string -> string)
        -> LifecycleWork
        -> HostFence
        -> observedHead: int64 option
        -> blockerDigest: string option
        -> KernelDecision
    val reopen: LifecycleWork -> incumbencyId: string -> KernelDecision
    val advance: LifecycleWork -> expected: string -> next: string -> KernelDecision
    val refuseDetachedOpening: LifecycleWork -> KernelDecision
    val spawnPrompt: draft: string -> string
    val readRecovery: string -> draftExists: bool -> Result<RecoveryPosition * RecoveryNext, ReadFault>
