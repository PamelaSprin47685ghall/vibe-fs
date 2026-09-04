namespace Wanxiangshu.Execution.Delegation.Fork.OpenCode

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

/// EXEC-016: join-capable roles must join outstanding work before terminal idle.
module HostJoinGuard =

    [<RequireQualifiedAccess>]
    type JoinGuardNudgeOutcome =
        | Sent of PromptKey
        | AlreadyOutstanding
        | AdmissionRejected of QuiescencePermitFailure
        | Superseded
        | NotSent
        | Failed of reason: string

    /// Send JoinGuard Continuation. The business caller has already proven that
    /// background work remains outstanding. Transport dedupes only the exact
    /// terminal occasion and consumes the fresh idle permit at physical send.
    val nudge:
        sessionPort: ISessionHostPort ->
        rootWorkspace: IRootWorkspaceReader ->
        journal: AgentJournal option ->
        nudgeKeys: HashSet<string> ->
        physicalAdmission: (unit -> Result<unit, QuiescencePermitFailure>) ->
        releaseAdmission: (unit -> Result<unit, QuiescencePermitFailure>) ->
        sessionId: SessionId ->
        terminalProviderRun: ProviderRunIdentity ->
        directory: string option ->
            Task<JoinGuardNudgeOutcome>
