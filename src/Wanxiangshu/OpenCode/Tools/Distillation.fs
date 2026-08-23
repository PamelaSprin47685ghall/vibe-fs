namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Text
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Host
open Wanxiangshu.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Process
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica

/// Fixed-cost tail distillation for spooled command output.
module Distillation =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let FragmentPrompt = "tool/distill/fragment-prompt"

        [<Literal>]
        let InputTruncated = "tool/distill/input-truncated"

        [<Literal>]
        let CondensationFailed = "tool/distill/condensation-failed"

    type IDistillationRuntime = DistillationRuntime.IDistillationRuntime

    let asDistillationRuntime = DistillationRuntime.asDistillationRuntime
    let ofForkRuntime = DistillationRuntime.ofForkRuntime

    let distillFragmentPrompt (lang: ProviderLanguage) =
        ProviderProse.render lang Path.FragmentPrompt Map.empty

    let private agentId (processId: string) =
        sprintf "exec-%s" (HostDigest.sha256Hex processId)

    let private completionText (completion: RunCompletion) =
        match completion.Outcome with
        | AgentCompleted payload when not (String.IsNullOrWhiteSpace payload.WorkRecord) -> payload.WorkRecord
        | AgentCompleted _ -> raise (InvalidOperationException "DISTILL_EMPTY_WORK_RECORD")
        | AgentFailed payload -> raise (InvalidOperationException payload.Message)
        | AgentAbandoned(_, reason) -> raise (InvalidOperationException reason)

    /// Join budget for the one bounded-tail Distiller.
    [<Literal>]
    let AwaitAgentTimeoutMs = 600_000

    let private awaitJournalAdvanceOrDeadline (changed: Task<JournalChange>) (remainingMs: int) : Task<bool> =
        emitJsExpr
            (changed, PtyTiming.timerTask remainingMs)
            "Promise.race([$0.then(function(){return!0}),$1.then(function(){return!1})])"

    type private AwaitStep =
        | Completed of RunCompletion
        | Retry
        | GiveUp

    let private raiseAwaitTimeout () =
        raise (InvalidOperationException "DISTILL_AWAIT_TIMEOUT")

    let private remainingMs (deadline: DateTimeOffset) =
        int (deadline - DateTimeOffset.UtcNow).TotalMilliseconds

    let private onTimedOut (runtime: IDistillationRuntime) fromRevision (deadline: DateTimeOffset) =
        task {
            let remaining = remainingMs deadline

            if remaining <= 0 then
                return GiveUp
            else
                let changed = runtime.AwaitJournalChangeFrom fromRevision
                let! journalAdvanced = awaitJournalAdvanceOrDeadline changed remaining
                return (if journalAdvanced then Retry else GiveUp)
        }

    let private stepJoin (runtime: IDistillationRuntime) fromRevision deadline joined =
        match joined with
        | Ok completion -> Task.FromResult(Completed completion)
        | Error ForkError.TimedOut -> onTimedOut runtime fromRevision deadline
        | Error error -> raise (InvalidOperationException(error.ToString()))

    let private applyAwaitStep (step: AwaitStep) (retry: unit -> Task<RunCompletion>) =
        match step with
        | Completed completion -> Task.FromResult completion
        | Retry -> retry ()
        | GiveUp -> raiseAwaitTimeout ()

    /// Permit-gated targeted await (Journal-authoritative via HostForkRuntime).
    /// FamilyWaiting → ForkError.TimedOut: wait for a journal advance within one deadline.
    /// FamilyBlocked / real join timeout → ForkError.NotFound: hard fail, no retry.
    let awaitAgentWithPermit (runtime: IDistillationRuntime) (agentId: string) =
        let deadline = DateTimeOffset.UtcNow.AddMilliseconds(float AwaitAgentTimeoutMs)

        let rec loop () : Task<RunCompletion> =
            task {
                let remaining = remainingMs deadline

                if remaining <= 0 then
                    return raiseAwaitTimeout ()
                else
                    let fromRevision = runtime.CurrentJournalRevision()
                    let! joined = runtime.AwaitAgentWithPermit(agentId, Some remaining)
                    let! step = stepJoin runtime fromRevision deadline joined
                    return! applyAwaitStep step loop
            }

        loop ()

    type private DistillerFailure =
        { OwnedAgentId: string option
          Message: string }

    type private TailInput = { Bytes: byte[]; Truncated: bool }

    let private retainLatestBytes (limit: int) (current: byte[]) (next: byte[]) =
        let nextLength = min limit (current.Length + next.Length)
        let nextBytes = min next.Length nextLength
        let currentBytes = nextLength - nextBytes
        let retained = Array.zeroCreate<byte> nextLength

        if currentBytes > 0 then
            Array.blit current (current.Length - currentBytes) retained 0 currentBytes

        if nextBytes > 0 then
            Array.blit next (next.Length - nextBytes) retained currentBytes nextBytes

        retained

    let private awaitForkedDistiller
        (runtime: IDistillationRuntime)
        (result: ForkResult)
        : Task<Result<string, DistillerFailure>> =
        task {
            try
                let! completion = awaitAgentWithPermit runtime result.AgentId
                return Ok(completionText completion)
            with ex ->
                return
                    Error
                        { OwnedAgentId = Some result.AgentId
                          Message = ex.Message }
        }

    let private runDistillerPrompt
        (runtime: IDistillationRuntime)
        (id: string)
        (prompt: string)
        (payload: string)
        : Task<Result<string, DistillerFailure>> =
        task {
            let! forked = runtime.Fork(id, Role.Distiller, prompt, Some payload)

            match forked with
            | Error error -> return Error { OwnedAgentId = None; Message = error }
            | Ok result -> return! awaitForkedDistiller runtime result
        }

    let private readLatestTail (spoolPath: string) : Task<TailInput> =
        task {
            // DSL-MUTABLE: algorithm-scratch — rolling bounded spool tail.
            let mutable latest = [||]
            // DSL-MUTABLE: algorithm-scratch — total observed bytes establish truncation honestly.
            let mutable observedBytes = 0L

            do!
                Spool.readChunks spoolPath (fun chunk ->
                    task {
                        observedBytes <- observedBytes + int64 chunk.Length
                        latest <- retainLatestBytes Spool.ChunkSizeBytes latest chunk
                    })

            return
                { Bytes = latest
                  Truncated = observedBytes > int64 Spool.ChunkSizeBytes }
        }

    let private renderTruncationBoundary (lang: ProviderLanguage) (tail: TailInput) (account: string) =
        if tail.Truncated then
            ProviderProse.render lang Path.InputTruncated (Map [ "account", account ])
        else
            account

    let private renderFailure (lang: ProviderLanguage) (tail: TailInput) (message: string) =
        let failed =
            ProviderProse.render
                lang
                Path.CondensationFailed
                (Map [ "error", message; "raw_tail", Encoding.UTF8.GetString tail.Bytes ])

        renderTruncationBoundary lang tail failed

    let private finishDistillation
        (runtime: IDistillationRuntime)
        (lang: ProviderLanguage)
        (tail: TailInput)
        (outcome: Result<string, DistillerFailure>)
        =
        match outcome with
        | Ok account -> renderTruncationBoundary lang tail account
        | Error failure ->
            failure.OwnedAgentId |> Option.iter runtime.CancelAgent
            renderFailure lang tail failure.Message

    let private distillNonEmptyTail
        (runtime: IDistillationRuntime)
        (spoolPath: string)
        (lang: ProviderLanguage)
        (tail: TailInput)
        =
        task {
            let payload = Encoding.UTF8.GetString tail.Bytes
            let id = agentId (sprintf "%s|%s" spoolPath (HostDigest.sha256Hex payload))
            let! outcome = runDistillerPrompt runtime id (distillFragmentPrompt lang) payload
            return finishDistillation runtime lang tail outcome
        }

    /// Consume the spool with fixed model cost: retain only the latest 200 KiB
    /// window and run at most one Distiller. Output growth never creates more
    /// provider sessions or a reduce tree.
    let distillSpool (runtime: IDistillationRuntime) (spoolPath: string) (lang: ProviderLanguage) =
        task {
            let! tail = readLatestTail spoolPath

            if tail.Bytes.Length = 0 then
                return ""
            else
                return! distillNonEmptyTail runtime spoolPath lang tail
        }
