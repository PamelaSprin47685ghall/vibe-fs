namespace Wanxiangshu.Host

open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// What a delegation command answers. Both cases are one instruction for the
/// calling participant; the host decides how to put it on its wire.
[<RequireQualifiedAccess>]
type DelegationReply =
    /// The charge was carried, sent as guidance, or commissioned.
    | Placed of instruction: string
    /// The command refused, or the placement failed or is uncertain.
    | Refused of instruction: string

/// Host-neutral port for the fork / resume / commission commands
/// (`Wanxiangshu.Execution.Delegation.Fork.ForkCommand`), bound to one call.
type IDelegationCommandHost =
    inherit IDelegationCallerHost
    abstract WorkspaceDirectory: string option
    /// The rendered view of a session, used as an attachment.
    abstract ViewForSession: sessionId: string -> Task<string option>
    abstract ManagerCapabilityFactsFor: sessionId: string -> Task<ManagerCapabilityFacts>
    /// The caller's role from its authority root; None when it has none.
    abstract CallerRole: unit -> Task<Role option>
    /// A fresh handle id for a new child or manager job.
    abstract NewHandleId: unit -> string

    /// Open a Manager job for the calling Orchestrator.
    abstract OpenManagerJob:
        jobId: ManagerJobId * managerAgent: string * prompt: string * byname: string * expectedToolCalls: int option ->
            Task<Result<string, string>>

    /// Carry a new charge to an existing Manager job for this exact tool call.
    abstract ContinueManagerJob:
        jobId: ManagerJobId *
        prompt: string *
        callerProviderRun: ProviderRunIdentity *
        callerToolCallId: ToolCallId *
        expectedToolCalls: int option ->
            Task<Result<string, string>>
