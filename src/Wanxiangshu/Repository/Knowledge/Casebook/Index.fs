namespace Wanxiangshu.Repository.Knowledge.Casebook

open Wanxiangshu.Change
open Wanxiangshu.Change.Host
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Delegation.Handle.OpenCode
open Wanxiangshu.Execution.Delegation.OpenCode
open Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Execution.Session.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Finality.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Resources
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence

open System
open System.Threading.Tasks
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
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.OpenCode

/// Process-local Casebook index frozen for the current provider epoch.
/// Provider entries expose only a shelfmark plus canonical Q; durable session
/// identity remains an internal lookup key and never crosses the Casebook wire.
module CasebookIndex =

    type Entry = { Shelfmark: string; Question: string }

    type Snapshot = { Epoch: int64; Cases: Entry list }

    type private ResolvedEntry = { Public: Entry; Case: Case }

    let private gate = obj ()
    // DSL-MUTABLE: resource
    let mutable private frozen: Snapshot option = None
    // DSL-MUTABLE: resource
    let mutable private dirty = true

    let tryGet () : Snapshot option = lock gate (fun () -> frozen)

    /// Force the next successful refresh to advance epoch (Captured/Refreshed/Evicted).
    let invalidate () : unit = lock gate (fun () -> dirty <- true)

    let private compactTitle (question: string) =
        let firstLine =
            if String.IsNullOrWhiteSpace question then
                "Untitled case"
            else
                question.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n')
                |> Array.tryFind (String.IsNullOrWhiteSpace >> not)
                |> Option.defaultValue "Untitled case"

        let cleaned = firstLine.Trim().TrimStart([| '#'; '-'; '*'; ' '; '\t' |]).Trim()

        let words =
            cleaned.Split([| ' '; '\t' |], StringSplitOptions.RemoveEmptyEntries)
            |> String.concat " "

        let title =
            if String.IsNullOrWhiteSpace words then
                "Untitled case"
            else
                words

        if title.Length <= 72 then
            title
        else
            title.Substring(0, 71).TrimEnd() + "…"

    /// Stable public locator. The suffix is a one-way catalog discriminator,
    /// never the durable session identity itself.
    let shelfmarkFor (sessionId: string) (canonicalQuestion: string) : string =
        let digest = ToolHostCodec.digest sessionId

        let discriminator =
            let colon = digest.IndexOf(':')

            if colon >= 0 && colon + 1 < digest.Length then
                digest.Substring(colon + 1)
            else
                digest

        sprintf "%s · %s" (compactTitle canonicalQuestion) discriminator

    let private resolvedEntries (cases: Map<string, Case>) : ResolvedEntry list =
        cases
        |> Map.toList
        |> List.map (fun (sessionId, case) ->
            { Public =
                { Shelfmark = shelfmarkFor sessionId case.Q
                  Question = case.Q }
              Case = case })
        |> List.sortBy (fun entry -> entry.Public.Shelfmark)

    let private publicEntries (cases: Map<string, Case>) =
        resolvedEntries cases |> List.map (fun entry -> entry.Public)

    let private project (store: IEventStore) (capacity: int) : Result<Map<string, Case>, string> =
        match store.TryCurrent "Casebook" with
        | None -> Ok Map.empty
        | Some current ->
            let state = unbox<CasebookProjection.State> current
            let cases = CasebookProjection.evict capacity state.Cases |> fst
            Ok cases

    /// Resolve a public shelfmark to its internal Case without exposing the
    /// durable session key. The generated shelfmark is collision-free for the
    /// process identity inputs because it carries the full 32-bit discriminator.
    let resolve (store: IEventStore) (capacity: int) (shelfmark: string) : Task<Result<Case option, string>> =
        task {
            match project store capacity with
            | Error error -> return Error error
            | Ok cases ->
                return
                    Ok(
                        resolvedEntries cases
                        |> List.tryFind (fun entry -> entry.Public.Shelfmark = shelfmark)
                        |> Option.map (fun entry -> entry.Case)
                    )
        }

    /// Rebuild from the unified EventStore projection. Epoch advances when the
    /// provider-visible index changes or an explicit invalidation occurred.
    let refresh (store: IEventStore) (capacity: int) : Task<Snapshot> =
        task {
            let projected = project store capacity

            return
                lock gate (fun () ->
                    match projected with
                    | Error _ ->
                        match frozen with
                        | Some snapshot -> snapshot
                        | None -> { Epoch = 0L; Cases = [] }
                    | Ok cases ->
                        let entries = publicEntries cases

                        let previousEpoch =
                            frozen |> Option.map (fun snapshot -> snapshot.Epoch) |> Option.defaultValue -1L

                        let visibleChanged =
                            match frozen with
                            | None -> true
                            | Some snapshot -> snapshot.Cases <> entries

                        let epoch =
                            if dirty || visibleChanged then
                                previousEpoch + 1L
                            else
                                previousEpoch

                        dirty <- false

                        let snapshot = { Epoch = epoch; Cases = entries }
                        frozen <- Some snapshot
                        snapshot)
        }
