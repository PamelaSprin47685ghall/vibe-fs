namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore

/// CASE-003/010 / KR-010: process-local Casebook session wiring.
module CasebookLifecycle =

    let collector = CasebookObservationCollector()

    let private stateGate = obj ()
    // DSL-MUTABLE: resource
    let mutable private enabledWorkspace: string option = None

    let setEnabled (workspaceRoot: string option) : unit =
        lock stateGate (fun () ->
            enabledWorkspace <-
                match workspaceRoot with
                | Some root when CasebookFeature.isEnabled root -> Some root
                | _ -> None)

    let isEnabled () : bool =
        lock stateGate (fun () -> enabledWorkspace.IsSome)

    let notePrompt (inspectorSessionId: string) (q: string) : unit =
        CasebookDraftStore.setQ inspectorSessionId q

    let noteAnswer (inspectorSessionId: string) (a: string) : unit =
        CasebookDraftStore.setA inspectorSessionId a

    let cleanupInspector (inspectorSessionId: string) : unit =
        CasebookDraftStore.clear inspectorSessionId
        collector.Drain inspectorSessionId |> ignore

    let private probeExistingScope
        (store: IEventStore)
        (inspectorSessionId: string)
        : Task<Result<Case option, string>> =
        task {
            match! CasebookWorkflow.fetchCase store 0 inspectorSessionId with
            | Error reason -> return Error(sprintf "cannot confirm case scope %s: %s" inspectorSessionId reason)
            | Ok existing -> return Ok existing
        }

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
                    (sprintf "case already finalized for scope %s" case.Identity)
            )
        | Ok None -> None

    let private refreshIndexThenSettle
        (store: IEventStore)
        (inspectorSessionId: string)
        : Task<InspectorFinalizeSettlement> =
        task {
            try
                let! _ = CasebookIndex.refresh store 256
                return InspectorFinalizeSettlement.finalized inspectorSessionId
            with ex ->
                return InspectorFinalizeSettlement.unknown inspectorSessionId ex.Message
        }

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
                let related =
                    observations
                    |> List.choose (function
                        | Observation.FileRead(p, _) -> Some p
                        | _ -> None)
                    |> List.distinct
                    |> List.sort

                let case: Case =
                    { Identity = inspectorSessionId
                      SourceTrace = inspectorSessionId
                      Q = q'
                      A = a'
                      RelatedPaths = related
                      CompletionFileState = "state-initial"
                      MaintenanceFileState = "state-initial"
                      AccessOrder = 0L
                      Observations = observations }

                return! archiveCase store inspectorSessionId case
        }

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

    let finalizeEngineerCase
        (store: IEventStore)
        (identity: string)
        (sourceTrace: string)
        (q: string)
        (a: string)
        (relatedPaths: string list)
        (completionStateRef: string)
        : Task<InspectorFinalizeSettlement> =
        task {
            let case: Case =
                { Identity = identity
                  SourceTrace = sourceTrace
                  Q = q
                  A = a
                  RelatedPaths = relatedPaths
                  CompletionFileState = completionStateRef
                  MaintenanceFileState = completionStateRef
                  AccessOrder = 0L
                  Observations = [] }

            return! archiveCase store identity case
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

    let touchAccess (workspaceRoot: string) (store: IEventStore) (sessionId: string) : Task<unit> =
        task {
            if CasebookFeature.isEnabled workspaceRoot then
                do! touchAccessEnabled store sessionId
        }
