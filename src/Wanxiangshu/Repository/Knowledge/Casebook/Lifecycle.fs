namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore

/// CASE-003/010: process-local Casebook session wiring — draft Q/A turns,
/// observation drain, graceful finalize vs unexpected cleanup. Publication
/// goes through the injected IEventStore; never global state.
module CasebookLifecycle =

    /// Process-local singleton the plugin feeds; lifecycle drains it.
    let collector = CasebookObservationCollector()

    let private stateGate = obj ()
    // DSL-MUTABLE: resource
    let mutable private enabledWorkspace: string option = None

    /// Marker-gated enablement for the shared collector path. `None` or a root
    /// without `.wanxiang/casebook` disables; does not touch the store.
    let setEnabled (workspaceRoot: string option) : unit =
        lock stateGate (fun () ->
            enabledWorkspace <-
                match workspaceRoot with
                | Some root when CasebookFeature.isEnabled root -> Some root
                | _ -> None)

    let isEnabled () : bool =
        lock stateGate (fun () -> enabledWorkspace.IsSome)

    /// Invoke: record Q for the inspector session id (appends a turn).
    let notePrompt (inspectorSessionId: string) (q: string) : unit =
        CasebookDraftStore.setQ inspectorSessionId q

    /// Return: record A for the inspector session id (fills the current turn).
    let noteAnswer (inspectorSessionId: string) (a: string) : unit =
        CasebookDraftStore.setA inspectorSessionId a

    /// Unexpected delete / cancel: clear draft + drop collector buffer; NEVER append events.
    let cleanupInspector (inspectorSessionId: string) : unit =
        CasebookDraftStore.clear inspectorSessionId
        collector.Drain inspectorSessionId |> ignore

    /// Run the durable case-scope probe for `inspectorSessionId`. Aborts on store
    /// error so the dispatch below only sees a verdict-bearing Result.
    let private probeExistingScope
        (store: IEventStore)
        (inspectorSessionId: string)
        : Task<Result<Case option, string>> =
        task {
            match! CasebookWorkflow.fetchCase store 0 inspectorSessionId with
            | Error reason -> return Error(sprintf "cannot confirm case scope %s: %s" inspectorSessionId reason)
            | Ok existing -> return Ok existing
        }

    /// A `Some` case already settled under this scope: refuse the spawn
    /// outright, keeping the original case's evidence. A `None` verdict means
    /// the archive may proceed.
    let private dispositionOfExistingScope
        (inspectorSessionId: string)
        (existing: Result<Case option, string>)
        : InspectorFinalizeSettlement option =
        match existing with
        | Error reason -> Some(InspectorFinalizeSettlement.notCommitted inspectorSessionId reason)
        | Ok(Some case) ->
            Some(
                InspectorFinalizeSettlement.phaseConflict
                    inspectorSessionId
                    (sprintf "case already finalized for scope %s" case.SessionId)
            )
        | Ok None -> None

    /// Refresh the read-model index and settle the settlement. The durable
    /// archive already committed, so an index-rebuild failure must report
    /// Unknown — the evidence is no longer re-ignorable either direction.
    let private refreshIndexThenSettle
        (store: IEventStore)
        (inspectorSessionId: string)
        : Task<InspectorFinalizeSettlement> =
        task {
            try
                let! _ = CasebookIndex.refresh store 256
                return InspectorFinalizeSettlement.finalized inspectorSessionId
            with ex ->
                // The case is archived; only the read-model refresh
                // failed. The finalize itself committed — report it
                // as Unknown rather than NotCommitted so nobody
                // re-archives the same case.
                return InspectorFinalizeSettlement.unknown inspectorSessionId ex.Message
        }

    /// Persist the produced Case record (the durable finalize hop) and rename
    /// its outcome to a typed settlement. The index refresh rides the Ok arm,
    /// never re-archiving `already finalized` or dropping NotCommitted.
    let private archiveCase
        (store: IEventStore)
        (inspectorSessionId: string)
        (case: Case)
        : Task<InspectorFinalizeSettlement> =
        task {
            match! CasebookWorkflow.finalizeCase store case with
            | Error reason when reason.Contains "already finalized" ->
                return InspectorFinalizeSettlement.phaseConflict inspectorSessionId reason
            | Error reason -> return InspectorFinalizeSettlement.notCommitted inspectorSessionId reason
            | Ok() ->
                CasebookIndex.invalidate ()
                return! refreshIndexThenSettle store inspectorSessionId
        }

    /// Spawn the CaseFinalize child, shape its result into the final
    /// settlement. On error the draft is already taken and no durable write
    /// happened, so NotCommitted is honest and the identity is retained.
    let private spawnFinalize
        (inspectorSessionId: string)
        (lastQ: string)
        (a: string)
        (observations: Observation list)
        (transcript: string option)
        (store: IEventStore)
        : Task<InspectorFinalizeSettlement> =
        task {
            let! spawned =
                BookkeeperRuntime.runTransaction
                    BookkeeperRequest.CaseFinalize
                    (SessionId.create inspectorSessionId)
                    lastQ
                    a
                    observations
                    transcript

            match spawned with
            | Error reason -> return InspectorFinalizeSettlement.notCommitted inspectorSessionId reason
            | Ok(q', a') ->
                let case: Case =
                    { SessionId = inspectorSessionId
                      Q = q'
                      A = a'
                      Observations = observations
                      LastAccessOrder = 0L }

                return! archiveCase store inspectorSessionId case
        }

    /// Decision arm of `dispatchFinalize` — probe + spawn wiring, separated
    /// so the outer `try` sees a single named subflow, not two matches.
    let private continueFinalizeDecision
        (store: IEventStore)
        (inspectorSessionId: string)
        (draft: CasebookDraft)
        (a: string)
        : Task<InspectorFinalizeSettlement> =
        task {
            let observations = collector.Drain inspectorSessionId

            let lastQ =
                draft.Turns
                |> List.tryLast
                |> Option.map (fun turn -> turn.Q)
                |> Option.defaultValue ""

            let transcript = CasebookDraftStore.transcript draft.Turns

            let! existing = probeExistingScope store inspectorSessionId

            match dispositionOfExistingScope inspectorSessionId existing with
            | Some settled -> return settled
            | None -> return! spawnFinalize inspectorSessionId lastQ a observations (Some transcript) store
        }

    /// Stage-chain dispatch over a prepared draft: probe existing scope, visit
    /// Bookkeeper if free, or archive the produced Case. Every stage is owned
    /// by its own one-`match` function above; the only `match` here is the
    /// early-exit over `dispositionOfExistingScope`'s optional verdict.
    let private dispatchFinalize
        (store: IEventStore)
        (_workspaceRoot: string)
        (inspectorSessionId: string)
        (draft: CasebookDraft)
        (a: string)
        : Task<InspectorFinalizeSettlement> =
        task {
            try
                let! result = continueFinalizeDecision store inspectorSessionId draft a
                return result
            with ex ->
                collector.Drain inspectorSessionId |> ignore
                return InspectorFinalizeSettlement.unknown inspectorSessionId ex.Message
        }

    let private runFinalize
        (store: IEventStore)
        (workspaceRoot: string)
        (inspectorSessionId: string)
        (draft: CasebookDraft)
        (a: string)
        : Task<InspectorFinalizeSettlement> =
        dispatchFinalize store workspaceRoot inspectorSessionId draft a

    let private finalizeWithDraft
        (store: IEventStore)
        (workspaceRoot: string)
        (inspectorSessionId: string)
        (draft: CasebookDraft)
        (lastAnswer: string option)
        : Task<InspectorFinalizeSettlement> =
        task {
            match lastAnswer with
            | None ->
                collector.Drain inspectorSessionId |> ignore
                return InspectorFinalizeSettlement.nothingToFinalize inspectorSessionId
            | Some a -> return! runFinalize store workspaceRoot inspectorSessionId draft a
        }

    let private finalizeIfDrafted
        (store: IEventStore)
        (workspaceRoot: string)
        (inspectorSessionId: string)
        : Task<InspectorFinalizeSettlement> =
        task {
            match CasebookDraftStore.tryTake inspectorSessionId with
            | None ->
                collector.Drain inspectorSessionId |> ignore
                return InspectorFinalizeSettlement.nothingToFinalize inspectorSessionId
            | Some draft ->
                let lastAnswer = draft.Turns |> List.rev |> List.tryPick (fun turn -> turn.A)
                return! finalizeWithDraft store workspaceRoot inspectorSessionId draft lastAnswer
        }

    /// Graceful owner scope close: if draft has Q+A, drain observations, run
    /// exactly one CaseFinalize child session with the full turn transcript,
    /// then finalizeCase once. Unexpected cleanup never runs Bookkeeper.
    let tryFinalizeInspector
        (workspaceRoot: string)
        (store: IEventStore)
        (inspectorSessionId: string)
        : Task<InspectorFinalizeSettlement> =
        task {
            if CasebookFeature.isEnabled workspaceRoot then
                return! finalizeIfDrafted store workspaceRoot inspectorSessionId
            else
                cleanupInspector inspectorSessionId
                return InspectorFinalizeSettlement.nothingToFinalize inspectorSessionId
        }

    let private refreshWhenTouched (store: IEventStore) (touched: Result<unit, string>) : Task<unit> =
        task {
            match touched with
            | Ok() ->
                CasebookIndex.invalidate ()
                let! _ = CasebookIndex.refresh store 256
                return ()
            | Error _ -> return ()
        }

    let private touchAccessEnabled (store: IEventStore) (sessionId: string) : Task<unit> =
        task {
            try
                let! touched = CasebookWorkflow.touchCaseAccess store sessionId
                do! refreshWhenTouched store touched
            with _ ->
                ()
        }

    /// Fresh fetch side-effect: append InspectorCaseAccessed (ignore errors).
    let touchAccess (workspaceRoot: string) (store: IEventStore) (sessionId: string) : Task<unit> =
        task {
            if CasebookFeature.isEnabled workspaceRoot then
                do! touchAccessEnabled store sessionId
        }
