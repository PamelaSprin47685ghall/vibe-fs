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

    let private runFinalize
        (store: IEventStore)
        (workspaceRoot: string)
        (inspectorSessionId: string)
        (draft: CasebookDraft)
        (a: string)
        : Task<InspectorFinalizeSettlement> =
        task {
            try
                let observations = collector.Drain inspectorSessionId

                let lastQ =
                    draft.Turns
                    |> List.tryLast
                    |> Option.map (fun turn -> turn.Q)
                    |> Option.defaultValue ""

                let transcript = CasebookDraftStore.transcript draft.Turns

                match!
                    task {
                        match! CasebookWorkflow.fetchCase store 0 inspectorSessionId with
                        | Error reason -> return Error(sprintf "cannot confirm case scope %s: %s" inspectorSessionId reason)
                        | Ok existing -> return Ok existing
                    }
                with
                | Error reason ->
                    // The store could not even confirm whether a case for this
                    // scope already exists: the finalize was never attempted
                    // and no durable effect could have happened.
                    return InspectorFinalizeSettlement.notCommitted inspectorSessionId reason
                | Ok(Some case) ->
                    // Already-finalized scope: refuse BEFORE launching a
                    // Bookkeeper child so a duplicate finalize never executes
                    // the completion a second time. The original case stays
                    // intact and the identity is retained for incident
                    // evidence.
                    return InspectorFinalizeSettlement.phaseConflict inspectorSessionId (sprintf "case already finalized for scope %s" case.SessionId)
                | Ok None ->
                match!
                    BookkeeperRuntime.runTransaction
                        BookkeeperRequest.CaseFinalize
                        (SessionId.create inspectorSessionId)
                        lastQ
                        a
                        observations
                        (Some transcript)
                with
                | Error reason ->
                    // The draft was already taken and the failure happened
                    // before any durable write: the finalize was never attempted
                    // against the store. Identity is retained for resume.
                    return InspectorFinalizeSettlement.notCommitted inspectorSessionId reason
                | Ok(q', a') ->
                    let case: Case =
                        { SessionId = inspectorSessionId
                          Q = q'
                          A = a'
                          Observations = observations
                          LastAccessOrder = 0L }

                    // CASE-010 exactly-one: the store already holds this case —
                    // the finalize raced a prior completion. The completion is
                    // neither re-executed nor forgotten; report the conflict and
                    // retain the identity for the incident evidence.
                    match! CasebookWorkflow.finalizeCase store case with
                    | Error reason when reason.Contains "already finalized" ->
                        return InspectorFinalizeSettlement.phaseConflict inspectorSessionId reason
                    | Error reason ->
                        return InspectorFinalizeSettlement.notCommitted inspectorSessionId reason
                    | Ok() ->
                        CasebookIndex.invalidate ()

                        try
                            let! _ = CasebookIndex.refresh store 256
                            return InspectorFinalizeSettlement.finalized inspectorSessionId
                        with ex ->
                            // The case is archived; only the read-model refresh
                            // failed. The finalize itself committed — report it
                            // as Unknown rather than NotCommitted so nobody
                            // re-archives the same case.
                            return InspectorFinalizeSettlement.unknown inspectorSessionId ex.Message
            with ex ->
                collector.Drain inspectorSessionId |> ignore
                return InspectorFinalizeSettlement.unknown inspectorSessionId ex.Message
        }

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
