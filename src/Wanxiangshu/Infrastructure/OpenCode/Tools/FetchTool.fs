namespace Wanxiangshu.Infrastructure

open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Infrastructure.Persist
open Wanxiangshu.OpenCode

/// CASE-004/009: the conditional `fetch` tool — registered only when the
/// Casebook marker exists (schema + execution both gated). Fetch replays the
/// stored observations against the current worktree: no-delta returns the
/// exact old A (freshness hint, never a proof); delta returns the old A
/// marked stale with a refresh intent. Fetch is cheap and never writes the
/// subject worktree.
module FetchTool =

    /// CASE-011: same-worktree fetch single-flight — concurrent fetches for the
    /// same session_id share one in-flight Task; a second caller awaits the
    /// same work instead of replaying the worktree twice.
    let private fetchGate = obj ()

    let private fetchInFlight =
        System.Collections.Generic.Dictionary<string, System.Threading.Tasks.Task<string>>()

    /// Emit a fresh hit (best-effort access touch).
    let private emitFresh (workspaceRoot: string) (sessionId: string) (answer: string) : string =
        CasebookLifecycle.touchAccess workspaceRoot sessionId

        ToolHostCodec.tomlObject
            [ "status", ToolHostCodec.TString "fresh"
              "session_id", ToolHostCodec.TString sessionId
              "a", ToolHostCodec.TString answer ]

    /// Stale path after Bookkeeper synthesis failed or still-stale post-refresh.
    let private emitStale (sessionId: string) (answer: string) : string =
        ToolHostCodec.tomlObject
            [ "status", ToolHostCodec.TString "stale"
              "session_id", ToolHostCodec.TString sessionId
              "a", ToolHostCodec.TString answer
              "refresh", ToolHostCodec.TString "required" ]

    let private runFetch
        (workspaceRoot: string)
        (store: IEventStore)
        (raw: IGitRawStore)
        (sessionId: string)
        : System.Threading.Tasks.Task<string> =
        task {
            match CasebookWorkflow.fetchCase store raw 256 sessionId with
            | Error err ->
                return
                    ToolHostCodec.tomlObject [ "error", ToolHostCodec.TString(sprintf "casebook fetch failed: %s" err) ]
            | Ok None ->
                return
                    ToolHostCodec.tomlObject
                        [ "status", ToolHostCodec.TString "no-case"
                          "session_id", ToolHostCodec.TString sessionId ]
            | Ok(Some case) ->
                let replayed = CasebookReplay.replayAll workspaceRoot case.Observations

                match CasebookWorkflow.checkFreshness case replayed with
                | ReplayResult.Fresh -> return emitFresh workspaceRoot sessionId case.A
                | ReplayResult.Stale ->
                    // CASE-006: one Bookkeeper child session (edit-qa against staged
                    // Q/A + stability verify). Maintenance failure keeps the old Case
                    // and still returns stale — never a fetch error.
                    match! CasebookBookkeeper.refreshStale store raw workspaceRoot sessionId with
                    | Ok true ->
                        match CasebookWorkflow.fetchCase store raw 256 sessionId with
                        | Error _ -> return emitStale sessionId case.A
                        | Ok None -> return emitStale sessionId case.A
                        | Ok(Some updated) ->
                            let again = CasebookReplay.replayAll workspaceRoot updated.Observations

                            match CasebookWorkflow.checkFreshness updated again with
                            | ReplayResult.Fresh -> return emitFresh workspaceRoot sessionId updated.A
                            | ReplayResult.Stale -> return emitStale sessionId updated.A
                    | Ok false
                    | Error _ -> return emitStale sessionId case.A
        }

    let spec (factory: HostToolFactory) (workspaceRoot: string) (store: IEventStore) (raw: IGitRawStore) : ToolSpec =
        let tString = ToolHostCodec.TString

        { Name = "fetch"
          Description =
            "Fetch a previous Inspector answer from the Casebook by session_id. "
            + "Replays the stored evidence against the current worktree; 'fresh' means the old answer still matches the evidence, "
            + "'stale' means the evidence changed and a refresh is required. Never modifies the worktree."
          Arguments = [ "session_id", ToolHostCodec.stringSchema factory ]
          Execute =
            fun args _ ->
                task {
                    let sessionId = args.Text "session_id"

                    if System.String.IsNullOrWhiteSpace sessionId then
                        return ToolHostCodec.tomlObject [ "error", tString "missing session_id" ]
                    else
                        // CASE-011: same-worktree single-flight — a concurrent
                        // fetch for the same session_id awaits the in-flight
                        // Task instead of replaying the worktree twice.
                        let! result =
                            lock fetchGate (fun () ->
                                match fetchInFlight.TryGetValue sessionId with
                                | true, existing -> existing
                                | false, _ ->
                                    let work =
                                        task {
                                            try
                                                return! runFetch workspaceRoot store raw sessionId
                                            finally
                                                lock fetchGate (fun () -> fetchInFlight.Remove sessionId |> ignore)
                                        }

                                    fetchInFlight.[sessionId] <- work
                                    work)

                        return result
                } }
