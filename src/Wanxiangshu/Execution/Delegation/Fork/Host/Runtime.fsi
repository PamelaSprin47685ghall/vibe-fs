namespace Wanxiangshu.Execution.Delegation.Fork.Host

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Context.Trace
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Session
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Process

type HostForkRuntime =
    new:
        parentId: SessionId *
        sessions: ISessionHostPort *
        childWorkRecordForRun: (SessionId -> XTraceRange -> ProviderRunIdentity -> Task<string option>) *
        createMailbox: (obj -> ForkCompletionMailbox) *
        ?journal: AgentJournal *
        ?onChildCreated: (string -> Role -> SessionId -> unit) *
        ?onChildCreatedDir: (string -> SessionId -> string option -> unit) *
        ?ptyPort: PtyPort *
        ?directoryFor: (string -> string option) *
        ?onRunStarted: (SessionId -> Role -> string option -> unit) *
        ?parentWorkRecordFor: (SessionId -> Task<string option>) *
        ?childWorkRecordFor: (SessionId -> Task<string option>) *
        ?handoff: ReusableHandoffPort *
        ?sessionSnapshot: ISessionSnapshotPort *
        ?cancelSignals: (SessionId seq -> unit) *
        ?managerOpensReviewBarrier: bool *
        ?treeHashFor: (string -> GitTreeHash option) *
        ?ownership: HandleOwnership *
        ?clock: IClockPort ->
            HostForkRuntime

    member internal Runtime: ForkRuntime
    member internal Children: Dictionary<string, SessionId>
    member internal DormantChildren: Dictionary<string, SessionId>
    member internal PendingRuns: Dictionary<string, PendingHostRun>
    member internal PtyRuns: HashSet<string>
    member SnapshotOutstandingAgentRuns: unit -> (string * SessionId) list
    member SnapshotOutstandingPtyRuns: unit -> string list
    member SubscribePtyCompletion: listener: (PtyJoinItem -> unit) -> IDisposable
    member internal HandleOwnership: HandleOwnership

    member internal DeferredFirstPrompts:
        Dictionary<
            string,
            {| ChildId: SessionId
               IdentitySeed: PromptAuthority.IdentitySeed
               Prompt: string |}
         >

    member internal Clock: IClockPort
    member internal Now: unit -> DateTimeOffset
    member internal AdoptChild: agentId: string * childId: SessionId -> unit
    member SendDeferredFirstPrompt: agentId: string -> Task<Result<unit, string>>
    member DiscardDeferredFirstPrompt: agentId: string -> unit
    member internal Gate: obj
    member internal TerminalByName: Dictionary<string, string>
    member internal Sessions: ISessionHostPort
    member internal Journal: AgentJournal option
    member internal SessionSnapshot: ISessionSnapshotPort option
    member internal ParentId: SessionId
    member internal ParentKey: string
    member internal TryAcquireJoin: unit -> bool
    member internal ReleaseJoin: unit -> unit
    member internal PtyPort: PtyPort
    member internal DirectoryOf: (string -> string option)
    member internal RunStarted: (SessionId -> Role -> string option -> unit)
    member internal ChildCreated: (string -> Role -> SessionId -> unit)
    member internal ChildCreatedDir: (string -> SessionId -> string option -> unit)
    member internal ParentWorkRecordOf: (SessionId -> Task<string option>)
    member internal ChildWorkRecordOf: (SessionId -> Task<string option>)
    member internal ChildWorkRecordOfRun: (SessionId -> XTraceRange -> ProviderRunIdentity -> Task<string option>)
    member internal XTraceHead: (SessionId -> XTraceCursor)
    member internal HandoffPort: ReusableHandoffPort option
    member internal PrepareHandoff: route: DelegationHandoffRoute -> Task<Result<PreparedDelegationHandoff, string>>
    member internal TrackOwnedWork: work: (unit -> Task) -> unit

    member internal SendChildPrompt:
        (string
            -> SessionId
            -> Role
            -> PromptAuthority.IdentitySeed
            -> string
            -> (PhysicalUserMessageId -> unit)
            -> Task<HostForkRunLifecycle.AgentOwnerDispatchOutcome>)

    member internal SendBusyNudge: (string -> SessionId -> Role -> string -> string -> Task<Result<unit, string>>)
    member internal ParentAbortToken: int
    member internal ManagerOpensReviewBarrier: bool
    member internal TreeHashFor: (string -> GitTreeHash option)
    member IsRetiredHandle: agentId: string -> bool option
    member Complete: run: PendingHostRun * outcome: TerminalOutcome -> unit

    member InstallRun:
        agentId: string * childId: SessionId * role: Role * ?preparedHandoff: PreparedDelegationHandoff ->
            PendingHostRun

    member FailRun: run: PendingHostRun * error: string -> Task
    member MarkReady: run: PendingHostRun -> unit
    member internal AwaitCurrentWorkRecord: agentId: string -> Task<Result<string, string>>
    member CancelAndDrain: unit -> Task
    member DetachAndDrain: unit -> Task
    member Cancel: unit -> unit
    member List: unit -> AgentRecord list * PtyRecord list
    member TryFindAgent: agentId: string -> AgentRecord option
    member internal OwnsAgent: agentId: string -> bool
    member AdoptExisting: agentId: string * childId: SessionId * role: Role * agent: string -> unit
    member internal TryReusableChild: agentId: string -> (SessionId * bool) option
    member internal ActivateDormantChild: agentId: string * childId: SessionId * role: Role -> unit

    member internal ActivateDormantChildIfNeeded:
        wasDormant: bool * agentId: string * childId: SessionId * role: Role -> unit

    member TryChildSession: agentId: string -> SessionId option
    member PendingRunCount: int
    member PendingCompletionCount: int
    member IsCancelled: bool
