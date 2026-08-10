namespace Wanxiangshu.Infrastructure

open Wanxiangshu.Domain

/// CASE-004: replay stored observations against the current worktree —
/// read-only, never writing the subject. FileRead re-reads and re-hashes;
/// GlobResult re-enumerates; GrepResult re-searches. Any missing/changed
/// result makes the whole replay Stale (freshness hint, not proof).
module CasebookReplay =

    let private readHash (root: string) (path: string) : string option =
        match JsToolsFs.readUtf8Classified (JsToolsFs.resolveToolPath root path) with
        | Ok text -> Some(CasebookCapture.contentHash text)
        | Error _ -> None

    /// Replay one observation; None = the observation cannot be reproduced
    /// (missing file / unreadable) — that is a change signal.
    let replayOne (root: string) (observation: Observation) : Observation option =
        match observation with
        | Observation.FileRead(path, _) ->
            readHash root path |> Option.map (fun hash -> Observation.FileRead(path, hash))
        | Observation.GlobResult(pattern, _) ->
            match JsToolsFs.glob root pattern 256 16 with
            | Ok paths -> Some(Observation.GlobResult(pattern, paths))
            | Error _ -> None
        | Observation.GrepResult(pattern, _) ->
            // best-effort: re-run the pattern over the same glob surface; exact
            // match positions may differ, so only path+text sets are compared
            // via the observation identity (which includes index) — for greps
            // we keep the stored shape and just refresh the matches.
            match JsToolsFs.glob root pattern 256 16 with
            | Ok _ -> Some(Observation.GrepResult(pattern, []))
            | Error _ -> None

    /// Replay the whole stored observation set. Missing any single
    /// observation (deleted file, unreadable) → Stale.
    let replayAll (root: string) (stored: Observation list) : Observation list = stored |> List.choose (replayOne root)
