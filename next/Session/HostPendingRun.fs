namespace Wanxiangshu.Next.Session

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.OpenCode

type PendingHostRun =
    { Token: obj
      AgentId: string
      ChildId: SessionId
      Role: AgentRole
      Source: TaskCompletionSource<AgentCompletionOutcome>
      mutable Subscription: IDisposable option
      mutable Ready: bool
      mutable Finished: bool }

module HostPendingRun =
    let completionSource () =
        TaskCompletionSource<AgentCompletionOutcome>(TaskCreationOptions.RunContinuationsAsynchronously)

    let resolveModel resolver journal childId =
        match resolver, journal with
        | Some resolver, Some journal ->
            ModelResolver.resolveForSession resolver childId (AgentJournal.snapshot journal)
        | _ -> None

    /// New AgentOwnerRoot / omit-model default. Never inherits previous Side B.
    let resolveAuthorityDefault resolver journal childId =
        match resolver, journal with
        | Some resolver, Some journal ->
            ModelResolver.resolveAuthorityDefault
                (Some resolver)
                childId
                (AgentJournal.snapshot journal)
        | Some resolver, None -> Some resolver.SideA
        | _ -> None

    /// Fail-closed gate: a session is dead after 4 consecutive fallback failures
    /// (DurableFallback.nextDecision = FallbackDecision.Dead). Returns a refusal
    /// reason when the linked child is dead so callers can skip installs and sends.
    let sessionDeadRefusal (journal: AgentJournal option) (childId: SessionId) : string option =
        match journal with
        | Some j when DurableFallback.isDead childId (AgentJournal.snapshot j) ->
            Some(sprintf "Session %s is dead after 4 consecutive fallback failures" (SessionId.value childId))
        | _ -> None
