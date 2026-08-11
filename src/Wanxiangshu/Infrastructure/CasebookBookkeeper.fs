namespace Wanxiangshu.Infrastructure

open Wanxiangshu.Domain
open Wanxiangshu.Infrastructure.Persist

/// CASE-006 minimal Host Bookkeeper — mechanical CaseRefresh without LLM.
/// When stored observations are stale vs the current worktree, replay and
/// publish InspectorCaseRefreshed with the same Q/A and the replayed
/// observation set. No edit-qa synthesis; maintenance failure keeps the old
/// Case (never a fetch failure).
module CasebookBookkeeper =

    /// Returns Ok true when a Refreshed event was published; Ok false when
    /// Fresh / no-case (nothing to do). Error on store failure only.
    let refreshStale
        (store: IEventStore)
        (raw: IGitRawStore)
        (root: string)
        (sessionId: string)
        : Result<bool, string> =
        match CasebookWorkflow.needsRefresh store raw 256 sessionId root with
        | Error err -> Error err
        | Ok false -> Ok false
        | Ok true ->
            match CasebookWorkflow.fetchCase store raw 256 sessionId with
            | Error err -> Error err
            | Ok None -> Ok false
            | Ok(Some case) ->
                let replayed = CasebookReplay.replayAll root case.Observations

                match CasebookWorkflow.refreshCase store raw sessionId case.Q case.A replayed with
                | Ok() ->
                    CasebookIndex.invalidate ()
                    CasebookIndex.refresh store raw 256 |> ignore
                    Ok true
                | Error err -> Error err
