namespace Wanxiangshu.Infrastructure

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
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

    [<Fable.Core.Emit("$0 === undefined || $0 === null")>]
    let private isUndefined (value: obj) : bool = jsNative

    // ponytail: global lock, per-session lock if throughput matters
    let private fetchGate = obj ()

    let private fetchInFlight =
        System.Collections.Generic.Dictionary<string, System.Threading.Tasks.Task<string>>()

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
                    // dynamic access, consistent with JsToolSpec: plain args
                    // objects (tests) and HostToolArguments both work
                    let sessionRaw = args?session_id
                    let sessionId = if isUndefined sessionRaw then "" else string sessionRaw

                    if System.String.IsNullOrWhiteSpace sessionId then
                        return ToolHostCodec.tomlObject [ "error", tString "missing session_id" ]
                    else
                        match CasebookWorkflow.fetchCase store raw 256 sessionId with
                        | Error err ->
                            return
                                ToolHostCodec.tomlObject [ "error", tString (sprintf "casebook fetch failed: %s" err) ]
                        | Ok None ->
                            return
                                ToolHostCodec.tomlObject
                                    [ "status", tString "no-case"; "session_id", tString sessionId ]
                        | Ok(Some case) ->
                            let replayed = CasebookReplay.replayAll workspaceRoot case.Observations

                            match CasebookWorkflow.checkFreshness case replayed with
                            | ReplayResult.Fresh ->
                                return
                                    ToolHostCodec.tomlObject
                                        [ "status", tString "fresh"
                                          "session_id", tString sessionId
                                          "a", tString case.A ]
                            | ReplayResult.Stale ->
                                return
                                    ToolHostCodec.tomlObject
                                        [ "status", tString "stale"
                                          "session_id", tString sessionId
                                          "a", tString case.A
                                          "refresh", tString "required" ]
                } }
