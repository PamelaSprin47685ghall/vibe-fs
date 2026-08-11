namespace Wanxiangshu.Infrastructure

open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Journal
open Wanxiangshu.OpenCode

/// CASE-003/010: process-local Casebook session wiring — draft Q/A turns,
/// observation drain, graceful finalize vs unexpected cleanup. Publication
/// goes only through WorkspaceEventStore (unified store); never AgentJournal.
module CasebookLifecycle =

    /// Process-local singleton the plugin feeds; lifecycle drains it.
    let collector = ObservationCollector()

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

    /// Graceful owner scope close: if draft has Q+A, drain observations, run
    /// exactly one CaseFinalize child session with the full turn transcript,
    /// then finalizeCase once. Unexpected cleanup never runs Bookkeeper.
    let tryFinalizeInspector (workspaceRoot: string) (inspectorSessionId: string) : Task<Result<unit, string>> =
        task {
            if not (CasebookFeature.isEnabled workspaceRoot) then
                cleanupInspector inspectorSessionId
                return Ok()
            else
                match CasebookDraftStore.tryTake inspectorSessionId with
                | None ->
                    collector.Drain inspectorSessionId |> ignore
                    return Ok()
                | Some draft ->
                    let lastAnswer = draft.Turns |> List.rev |> List.tryPick (fun turn -> turn.A)

                    match lastAnswer with
                    | None ->
                        collector.Drain inspectorSessionId |> ignore
                        return Ok()
                    | Some a ->
                        try
                            let commonDir = RuntimePath.gitCommonDir workspaceRoot
                            let raw, store = WorkspaceEventStore.acquire commonDir
                            let observations = collector.Drain inspectorSessionId

                            let lastQ =
                                draft.Turns
                                |> List.tryLast
                                |> Option.map (fun turn -> turn.Q)
                                |> Option.defaultValue ""

                            let transcript = CasebookDraftStore.transcript draft.Turns

                            match!
                                BookkeeperRuntime.runTransaction
                                    BookkeeperRequest.CaseFinalize
                                    inspectorSessionId
                                    lastQ
                                    a
                                    observations
                                    (Some transcript)
                            with
                            | Error err -> return Error err
                            | Ok(q', a') ->
                                let case: Case =
                                    { SessionId = inspectorSessionId
                                      Q = q'
                                      A = a'
                                      Observations = observations
                                      LastAccessOrder = 0L }

                                match CasebookWorkflow.finalizeCase store raw case with
                                | Ok() ->
                                    CasebookIndex.invalidate ()
                                    CasebookIndex.refresh store raw 256 |> ignore
                                    return Ok()
                                | Error err -> return Error err
                        with ex ->
                            collector.Drain inspectorSessionId |> ignore
                            return Error ex.Message
        }

    /// Fresh fetch side-effect: append InspectorCaseAccessed (ignore errors).
    let touchAccess (workspaceRoot: string) (sessionId: string) : unit =
        if not (CasebookFeature.isEnabled workspaceRoot) then
            ()
        else
            try
                let commonDir = RuntimePath.gitCommonDir workspaceRoot
                let raw, store = WorkspaceEventStore.acquire commonDir

                match CasebookWorkflow.touchCaseAccess store raw sessionId with
                | Ok() ->
                    CasebookIndex.invalidate ()
                    CasebookIndex.refresh store raw 256 |> ignore
                | Error _ -> ()
            with _ ->
                ()
