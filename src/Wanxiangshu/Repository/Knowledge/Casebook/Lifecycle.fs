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
open Wanxiangshu.Repository.Programming.Js.OpenCode
open Wanxiangshu.Resources
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence

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
open Wanxiangshu.Persistence.Journal
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

    let private runFinalize
        (workspaceRoot: string)
        (inspectorSessionId: string)
        (draft: CasebookDraft)
        (a: string)
        : Task<Result<unit, string>> =
        taskResult {
            try
                let commonDir = RuntimePath.gitCommonDir workspaceRoot
                let store = WorkspaceEventStore.acquire commonDir
                let observations = collector.Drain inspectorSessionId

                let lastQ =
                    draft.Turns
                    |> List.tryLast
                    |> Option.map (fun turn -> turn.Q)
                    |> Option.defaultValue ""

                let transcript = CasebookDraftStore.transcript draft.Turns

                let! q', a' =
                    BookkeeperRuntime.runTransaction
                        BookkeeperRequest.CaseFinalize
                        inspectorSessionId
                        lastQ
                        a
                        observations
                        (Some transcript)

                let case: Case =
                    { SessionId = inspectorSessionId
                      Q = q'
                      A = a'
                      Observations = observations
                      LastAccessOrder = 0L }

                do! CasebookWorkflow.finalizeCase store case
                CasebookIndex.invalidate ()
                let! _ = CasebookIndex.refresh store 256 |> TaskResultCE.ofTask
                return ()
            with ex ->
                collector.Drain inspectorSessionId |> ignore
                return! Error ex.Message
        }

    let private finalizeWithDraft
        (workspaceRoot: string)
        (inspectorSessionId: string)
        (draft: CasebookDraft)
        (lastAnswer: string option)
        : Task<Result<unit, string>> =
        task {
            match lastAnswer with
            | None ->
                collector.Drain inspectorSessionId |> ignore
                return Ok()
            | Some a -> return! runFinalize workspaceRoot inspectorSessionId draft a
        }

    let private finalizeIfDrafted (workspaceRoot: string) (inspectorSessionId: string) : Task<Result<unit, string>> =
        task {
            match CasebookDraftStore.tryTake inspectorSessionId with
            | None ->
                collector.Drain inspectorSessionId |> ignore
                return Ok()
            | Some draft ->
                let lastAnswer = draft.Turns |> List.rev |> List.tryPick (fun turn -> turn.A)
                return! finalizeWithDraft workspaceRoot inspectorSessionId draft lastAnswer
        }

    /// Graceful owner scope close: if draft has Q+A, drain observations, run
    /// exactly one CaseFinalize child session with the full turn transcript,
    /// then finalizeCase once. Unexpected cleanup never runs Bookkeeper.
    let tryFinalizeInspector (workspaceRoot: string) (inspectorSessionId: string) : Task<Result<unit, string>> =
        task {
            if CasebookFeature.isEnabled workspaceRoot then
                return! finalizeIfDrafted workspaceRoot inspectorSessionId
            else
                cleanupInspector inspectorSessionId
                return Ok()
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

    let private touchAccessEnabled (workspaceRoot: string) (sessionId: string) : Task<unit> =
        task {
            try
                let commonDir = RuntimePath.gitCommonDir workspaceRoot
                let store = WorkspaceEventStore.acquire commonDir
                let! touched = CasebookWorkflow.touchCaseAccess store sessionId
                do! refreshWhenTouched store touched
            with _ ->
                ()
        }

    /// Fresh fetch side-effect: append InspectorCaseAccessed (ignore errors).
    let touchAccess (workspaceRoot: string) (sessionId: string) : Task<unit> =
        task {
            if CasebookFeature.isEnabled workspaceRoot then
                do! touchAccessEnabled workspaceRoot sessionId
        }
