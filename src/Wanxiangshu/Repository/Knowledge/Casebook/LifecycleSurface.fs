namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Persistence.Journal

/// JS-native lifecycle boundary for the Casebook draft and observation flow.
/// Draft storage, collector state, and Bookkeeper/Journal capabilities remain
/// private to the lifecycle owner.
module CasebookLifecycleSurface =

    let private settlementToJs (settled: CaseFinalizeSettlement) : obj =
        match settled.Commitment with
        | CaseFinalizeCommitment.Finalized
        | CaseFinalizeCommitment.NothingToFinalize ->
            box
                {| ok = true
                   releasesIdentity = CaseFinalizeSettlement.releasesIdentity settled |}
        | CaseFinalizeCommitment.NotCommitted reason
        | CaseFinalizeCommitment.Unknown reason
        | CaseFinalizeCommitment.PhaseConflict reason ->
            box
                {| ok = false
                   error = reason
                   releasesIdentity = false |}
        | CaseFinalizeCommitment.PersistenceFailed failure ->
            let result = CasebookAppendSurface.finalizeFailureToJs failure
            result?releasesIdentity <- CaseFinalizeSettlement.releasesIdentity settled
            result

    let enable (workspaceRoot: string) : unit =
        CasebookLifecycle.setEnabled (Some workspaceRoot)

    let disable () : unit = CasebookLifecycle.setEnabled None

    let isEnabled () : bool = CasebookLifecycle.isEnabled ()

    let notePrompt (sessionId: string) (question: string) : unit =
        CasebookLifecycle.notePrompt sessionId question

    let noteAnswer (sessionId: string) (answer: string) : unit =
        CasebookLifecycle.noteAnswer sessionId answer

    let collect (sessionId: string) (toolName: string) (args: obj) (output: string) : unit =
        CasebookLifecycle.collector.Collect(sessionId, toolName, args, output)

    let observationCount (sessionId: string) : int =
        CasebookLifecycle.collector.Count sessionId

    let cleanup (sessionId: string) : unit =
        CasebookLifecycle.cleanupDraft sessionId

    let private acquireStore (workspaceRoot: string) : IEventStore =
        Wanxiangshu.OpenCode.WorkspaceEventStore.acquire (RuntimePath.gitCommonDir workspaceRoot)

    let tryFinalize (workspaceRoot: string) (sessionId: string) : Task<obj> =
        let store = acquireStore workspaceRoot

        task {
            let! settled = CasebookLifecycle.tryFinalizeDraft workspaceRoot store sessionId

            return settlementToJs settled
        }

    let finalizeEngineerCase
        (store: obj)
        (identity: string)
        (trace: string)
        (question: string)
        (answer: string)
        (relatedPaths: string array)
        (baseline: string)
        : Task<obj> =
        task {
            let! settled =
                CasebookLifecycle.finalizeEngineerCase
                    (unbox<EventStoreHandle> store).Store
                    identity
                    trace
                    question
                    answer
                    (Array.toList relatedPaths)
                    baseline

            return settlementToJs settled
        }

    let touchAccess (workspaceRoot: string) (sessionId: string) : Task<unit> =
        CasebookLifecycle.touchAccess workspaceRoot (acquireStore workspaceRoot) sessionId
