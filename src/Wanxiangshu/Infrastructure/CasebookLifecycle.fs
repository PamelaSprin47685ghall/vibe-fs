namespace Wanxiangshu.Infrastructure

open Wanxiangshu.Domain
open Wanxiangshu.Journal
open Wanxiangshu.OpenCode

/// CASE-003/010: process-local Casebook session wiring — draft Q/A, observation
/// drain, graceful finalize vs unexpected cleanup. Publication goes only through
/// WorkspaceEventStore (unified store); never AgentJournal.
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

    /// Invoke: record Q for the inspector session id.
    let notePrompt (inspectorSessionId: string) (q: string) : unit =
        CasebookDraftStore.setQ inspectorSessionId q

    /// Return: record A for the inspector session id.
    let noteAnswer (inspectorSessionId: string) (a: string) : unit =
        CasebookDraftStore.setA inspectorSessionId a

    /// Unexpected delete / cancel: clear draft + drop collector buffer; NEVER append events.
    let cleanupInspector (inspectorSessionId: string) : unit =
        CasebookDraftStore.clear inspectorSessionId
        collector.Drain inspectorSessionId |> ignore

    /// Graceful owner scope close: if draft has Q+A, drain+finalizeCase once; then clear.
    /// Best-effort Result — missing draft/A is Ok no-op; store failures surface as Error.
    let tryFinalizeInspector (workspaceRoot: string) (inspectorSessionId: string) : Result<unit, string> =
        if not (CasebookFeature.isEnabled workspaceRoot) then
            cleanupInspector inspectorSessionId
            Ok()
        else
            match CasebookDraftStore.tryTake inspectorSessionId with
            | None ->
                collector.Drain inspectorSessionId |> ignore
                Ok()
            | Some draft ->
                match draft.A with
                | None ->
                    collector.Drain inspectorSessionId |> ignore
                    Ok()
                | Some a ->
                    try
                        let commonDir = RuntimePath.gitCommonDir workspaceRoot
                        let raw, store = WorkspaceEventStore.acquire commonDir
                        let observations = collector.Drain inspectorSessionId

                        let case: Case =
                            { SessionId = inspectorSessionId
                              Q = draft.Q
                              A = a
                              Observations = observations
                              LastAccessOrder = 0L }

                        match CasebookWorkflow.finalizeCase store raw case with
                        | Ok() ->
                            CasebookIndex.invalidate ()
                            CasebookIndex.refresh store raw 256 |> ignore
                            Ok()
                        | Error err -> Error err
                    with ex ->
                        collector.Drain inspectorSessionId |> ignore
                        Error ex.Message

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
