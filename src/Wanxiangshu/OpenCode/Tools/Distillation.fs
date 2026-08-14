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

/// Bounded hierarchical map/reduce distillation for spooled command output.
module Distillation =

    [<Literal>]
    let ReduceFanIn = 8

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let FragmentPrompt = "tool/distill/fragment-prompt"

        [<Literal>]
        let MergePrompt = "tool/distill/merge-prompt"

        [<Literal>]
        let CondensationUnavailable = "tool/distill/condensation-unavailable"

        [<Literal>]
        let CondensationIncomplete = "tool/distill/condensation-incomplete"

        [<Literal>]
        let CondensationFailed = "tool/distill/condensation-failed"

    type IDistillationRuntime = DistillationRuntime.IDistillationRuntime

    let asDistillationRuntime = DistillationRuntime.asDistillationRuntime
    let ofForkRuntime = DistillationRuntime.ofForkRuntime

    let distillFragmentPrompt (lang: ProviderLanguage) =
        ProviderProse.render lang Path.FragmentPrompt Map.empty

    let mergeDistillationsPrompt (lang: ProviderLanguage) =
        ProviderProse.render lang Path.MergePrompt Map.empty

    let private agentId (processId: string) (level: int) (startChunk: int) (endChunk: int) =
        sprintf "exec-%s" (HostDigest.sha256Hex (sprintf "%s|%d|%d|%d" processId level startChunk endChunk))

    let private completionText (completion: RunCompletion) =
        match completion.Outcome with
        | AgentCompleted payload when not (String.IsNullOrWhiteSpace payload.WorkRecord) -> payload.WorkRecord
        | AgentCompleted _ -> raise (InvalidOperationException "DISTILL_EMPTY_WORK_RECORD")
        | AgentFailed payload -> raise (InvalidOperationException payload.Message)
        | AgentAbandoned(_, reason) -> raise (InvalidOperationException reason)

    /// Per-chunk join budget. Prevents a single hung distiller from blocking the
    /// whole map/reduce forever (Part 1: unblock; Part 2 owns richer partial text).
    [<Literal>]
    let AwaitAgentTimeoutMs = 600_000

    let private awaitJournalAdvanceOrDeadline (changed: Task<JournalChange>) (remainingMs: int) : Task<bool> =
        emitJsExpr
            (changed, PtyTiming.timerTask remainingMs)
            "Promise.race([$0.then(function(){return!0}),$1.then(function(){return!1})])"

    /// Permit-gated targeted await (Journal-authoritative via HostForkRuntime).
    /// FamilyWaiting → ForkError.TimedOut: wait for a journal advance within one deadline.
    /// FamilyBlocked / real join timeout → ForkError.NotFound: hard fail, no retry.
    let awaitAgentWithPermit (runtime: IDistillationRuntime) (agentId: string) =
        let deadline = DateTimeOffset.UtcNow.AddMilliseconds(float AwaitAgentTimeoutMs)

        let rec loop () : Task<RunCompletion> =
            task {
                let remainingMs = int (deadline - DateTimeOffset.UtcNow).TotalMilliseconds

                if remainingMs <= 0 then
                    return raise (InvalidOperationException "DISTILL_AWAIT_TIMEOUT")
                else
                    let fromRevision = runtime.CurrentJournalRevision()
                    let! joined = runtime.AwaitAgentWithPermit(agentId, Some remainingMs)

                    match joined with
                    | Ok completion -> return completion
                    | Error ForkError.TimedOut ->
                        let remainingMs = int (deadline - DateTimeOffset.UtcNow).TotalMilliseconds

                        if remainingMs <= 0 then
                            return raise (InvalidOperationException "DISTILL_AWAIT_TIMEOUT")
                        else
                            let changed = runtime.AwaitJournalChangeFrom fromRevision
                            let! journalAdvanced = awaitJournalAdvanceOrDeadline changed remainingMs

                            if journalAdvanced then
                                return! loop ()
                            else
                                return raise (InvalidOperationException "DISTILL_AWAIT_TIMEOUT")
                    | Error error -> return raise (InvalidOperationException(error.ToString()))
            }

        loop ()

    let private runDistillerPrompt
        (runtime: IDistillationRuntime)
        (forkedIds: ResizeArray<string>)
        (processId: string)
        (level: int)
        (startChunk: int)
        (endChunk: int)
        (prompt: string)
        (payload: string option)
        =
        task {
            let id = agentId processId level startChunk endChunk
            // Track before fork so sibling cancel covers in-flight map/reduce agents.
            forkedIds.Add id
            let! fork = runtime.Fork(id, Role.Distiller, prompt, payload)

            match fork with
            | Error error -> return raise (InvalidOperationException error)
            | Ok result ->
                let! completion = awaitAgentWithPermit runtime result.AgentId
                return completionText completion
        }

    let distillFragment
        (runtime: IDistillationRuntime)
        (forkedIds: ResizeArray<string>)
        (spoolPath: string)
        (chunk: byte[])
        (index: int)
        (lang: ProviderLanguage)
        =
        let content = Encoding.UTF8.GetString chunk

        let rootDigest = HostDigest.sha256Hex (sprintf "%s|%d" spoolPath index)

        runDistillerPrompt runtime forkedIds rootDigest 0 index index (distillFragmentPrompt lang) (Some content)

    let mergeDistillations
        (runtime: IDistillationRuntime)
        (forkedIds: ResizeArray<string>)
        (lang: ProviderLanguage)
        (level: int)
        (batch: string list)
        =
        let combined = String.concat "\n" batch

        let batchDigest = HostDigest.sha256Hex (String.concat "\n" batch)

        runDistillerPrompt
            runtime
            forkedIds
            batchDigest
            level
            0
            (List.length batch - 1)
            (mergeDistillationsPrompt lang)
            (Some combined)

    let private rippleInsert
        (mergeDistillations: int -> string list -> Task<string>)
        (levels: ResizeArray<ResizeArray<string>>)
        (summary: string)
        =
        task {
            if levels.Count = 0 then
                levels.Add(ResizeArray())

            levels.[0].Add summary
            // DSL-MUTABLE: algorithm-scratch — online reduce level index
            let mutable lvl = 0

            while levels.[lvl].Count >= ReduceFanIn do
                let batch = levels.[lvl] |> Seq.toList
                levels.[lvl].Clear()
                let! reduced = mergeDistillations (lvl + 1) batch

                if levels.Count <= lvl + 1 then
                    levels.Add(ResizeArray())

                levels.[lvl + 1].Add reduced
                lvl <- lvl + 1
        }

    let private foldLevels
        (mergeDistillations: int -> string list -> Task<string>)
        (levels: ResizeArray<ResizeArray<string>>)
        =
        task {
            if levels.Count = 0 then
                return ""
            else
                // DSL-MUTABLE: algorithm-scratch — online reduce carry list
                let mutable carry: string list = []

                for i in 0 .. levels.Count - 1 do
                    let batch = carry @ (levels.[i] |> Seq.toList)

                    match batch with
                    | [] -> ()
                    | [ single ] when i = levels.Count - 1 -> carry <- [ single ]
                    | _ ->
                        let! reduced = mergeDistillations (i + 1) batch
                        carry <- [ reduced ]

                match carry with
                | [ single ] -> return single
                | _ -> return ""
        }

    let reduceOnline (mergeDistillations: int -> string list -> Task<string>) (summaries: string list) : Task<string> =
        task {
            let levels = ResizeArray<ResizeArray<string>>()

            for summary in summaries do
                do! rippleInsert mergeDistillations levels summary

            return! foldLevels mergeDistillations levels
        }

    let private rawTailText (chunks: ResizeArray<int * byte[]>) =
        if chunks.Count = 0 then
            ""
        else
            Encoding.UTF8.GetString(snd chunks.[chunks.Count - 1])

    let private partialWithTail (lang: ProviderLanguage) (account: string) (rawTail: string) =
        if String.IsNullOrWhiteSpace account then
            ProviderProse.render lang Path.CondensationUnavailable (Map [ "raw_tail", rawTail ])
        else
            ProviderProse.render lang Path.CondensationIncomplete (Map [ "account", account; "raw_tail", rawTail ])

    /// Maps bounded spool chunks concurrently (results sorted by chunk index),
    /// then reduces online. Map/reduce failures yield partial account plus the
    /// last 200KB raw tail instead of dropping ProcessResult.
    /// Agent waits go through IDistillationRuntime.AwaitAgentWithPermit (fresh permit each wait).
    let distillSpool (runtime: IDistillationRuntime) (spoolPath: string) (lang: ProviderLanguage) =
        task {
            let forkedIds = ResizeArray<string>()

            let cancelOwned () =
                for id in forkedIds do
                    runtime.CancelAgent id

            let chunks = ResizeArray<int * byte[]>()
            // DSL-MUTABLE: algorithm-scratch — spool chunk index counter
            let mutable index = 0

            do!
                Spool.readChunks spoolPath (fun chunk ->
                    task {
                        chunks.Add(index, Array.copy chunk)
                        index <- index + 1
                    })

            // Start all map tasks first (concurrent), then await in index order.
            let mapTasks =
                [| for (chunkIndex, chunk) in chunks do
                       task {
                           try
                               let! summary = distillFragment runtime forkedIds spoolPath chunk chunkIndex lang
                               return Choice1Of2(chunkIndex, summary)
                           with ex ->
                               return Choice2Of2(chunkIndex, chunk, ex.Message)
                       } |]

            let mapped = Array.zeroCreate mapTasks.Length

            for i in 0 .. mapTasks.Length - 1 do
                let! result = mapTasks.[i]
                mapped.[i] <- result

                match result with
                | Choice2Of2 _ -> cancelOwned ()
                | Choice1Of2 _ -> ()

            let successes =
                mapped
                |> Array.choose (function
                    | Choice1Of2 value -> Some value
                    | Choice2Of2 _ -> None)
                |> Array.sortBy fst

            let failures =
                mapped
                |> Array.choose (function
                    | Choice2Of2 value -> Some value
                    | Choice1Of2 _ -> None)

            let levels = ResizeArray<ResizeArray<string>>()
            let merge = mergeDistillations runtime forkedIds lang

            try
                for _, summary in successes do
                    do! rippleInsert merge levels summary

                let! reduced = foldLevels merge levels
                let rawTail = rawTailText chunks

                if failures.Length = 0 then
                    return reduced
                else
                    cancelOwned ()
                    return partialWithTail lang reduced rawTail
            with ex ->
                cancelOwned ()

                return
                    ProviderProse.render
                        lang
                        Path.CondensationFailed
                        (Map [ "error", ex.Message; "raw_tail", rawTailText chunks ])
        }
