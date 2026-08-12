namespace Wanxiangshu.Infrastructure

open Wanxiangshu.Domain
open Wanxiangshu.Infrastructure.Persist
open Wanxiangshu.OpenCode

/// Conditional Casebook read. Provider identity is a public shelfmark; durable
/// session identity, freshness state and maintenance machinery remain internal.
module FetchTool =

    let private fetchGate = obj ()

    let private fetchInFlight =
        System.Collections.Generic.Dictionary<string, System.Threading.Tasks.Task<string>>()

    let private answerResult (consequence: string) (answer: string) =
        ToolHostCodec.tomlObjectWithInstructions [ consequence ] [ "answer", ToolHostCodec.TString answer ]

    let private fresh workspaceRoot sessionId answer =
        CasebookLifecycle.touchAccess workspaceRoot sessionId
        answerResult "No change was found in the evidence this answer depended on." answer

    let private refreshed workspaceRoot sessionId answer =
        CasebookLifecycle.touchAccess workspaceRoot sessionId

        answerResult
            "The evidence this case depended on had changed. The case was revised against the current evidence."
            answer

    let private stale answer =
        answerResult
            "The Casebook could not reconcile the answer with the new evidence. Treat what follows as an older account."
            answer

    let private noCase () =
        ToolHostCodec.tomlObjectWithInstructions [ "The Casebook contains no entry under that shelfmark." ] []

    let private unavailable () =
        ToolHostCodec.tomlObjectWithInstructions [ "The Casebook could not be read from this execution context." ] []

    let private runFetch
        (workspaceRoot: string)
        (store: IEventStore)
        (raw: IGitRawStore)
        (shelfmark: string)
        : System.Threading.Tasks.Task<string> =
        task {
            match CasebookIndex.resolve store raw 256 shelfmark with
            | Error _ -> return unavailable ()
            | Ok None -> return noCase ()
            | Ok(Some case) ->
                let sessionId = case.SessionId
                let replayed = CasebookReplay.replayAll workspaceRoot case.Observations

                match CasebookWorkflow.checkFreshness case replayed with
                | ReplayResult.Fresh -> return fresh workspaceRoot sessionId case.A
                | ReplayResult.Stale ->
                    match! CasebookBookkeeper.refreshStale store raw workspaceRoot sessionId with
                    | Ok true ->
                        match CasebookWorkflow.fetchCase store raw 256 sessionId with
                        | Error _
                        | Ok None -> return stale case.A
                        | Ok(Some updated) ->
                            let again = CasebookReplay.replayAll workspaceRoot updated.Observations

                            match CasebookWorkflow.checkFreshness updated again with
                            | ReplayResult.Fresh -> return refreshed workspaceRoot sessionId updated.A
                            | ReplayResult.Stale -> return stale updated.A
                    | Ok false
                    | Error _ -> return stale case.A
        }

    let spec (factory: HostToolFactory) (workspaceRoot: string) (store: IEventStore) (raw: IGitRawStore) : ToolSpec =
        { Name = "fetch"
          Description =
            "Fetch a completed Casebook account by shelfmark. Returns the exact canonical answer with a freshness consequence; never investigates or modifies the repository."
          Arguments = [ "shelfmark", ToolHostCodec.stringSchema factory ]
          Execute =
            fun args _ ->
                task {
                    let shelfmark = args.Text "shelfmark"

                    if System.String.IsNullOrWhiteSpace shelfmark then
                        return ToolHostCodec.tomlObjectWithInstructions [ "A Casebook shelfmark is required." ] []
                    else
                        let! result =
                            lock fetchGate (fun () ->
                                match fetchInFlight.TryGetValue shelfmark with
                                | true, existing -> existing
                                | false, _ ->
                                    let work =
                                        task {
                                            try
                                                return! runFetch workspaceRoot store raw shelfmark
                                            finally
                                                lock fetchGate (fun () -> fetchInFlight.Remove shelfmark |> ignore)
                                        }

                                    fetchInFlight.[shelfmark] <- work
                                    work)

                        return result
                } }
