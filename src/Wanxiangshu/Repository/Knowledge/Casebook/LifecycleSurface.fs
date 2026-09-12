namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Threading.Tasks
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Persistence.Journal

/// JS-native lifecycle boundary for the Casebook draft and observation flow.
/// Draft storage, collector state, and Bookkeeper/Journal capabilities remain
/// private to the lifecycle owner.
module CasebookLifecycleSurface =

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
        CasebookLifecycle.cleanupInspector sessionId

    let private acquireStore (workspaceRoot: string) : IEventStore =
        Wanxiangshu.OpenCode.WorkspaceEventStore.acquire (RuntimePath.gitCommonDir workspaceRoot)

    let tryFinalize (workspaceRoot: string) (sessionId: string) : Task<obj> =
        let store = acquireStore workspaceRoot

        task {
            match! CasebookLifecycle.tryFinalizeInspector workspaceRoot store sessionId with
            | Ok() -> return box {| ok = true |}
            | Error message -> return box {| ok = false; error = message |}
        }

    let touchAccess (workspaceRoot: string) (sessionId: string) : Task<unit> =
        CasebookLifecycle.touchAccess workspaceRoot (acquireStore workspaceRoot) sessionId
