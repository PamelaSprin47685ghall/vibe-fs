namespace Wanxiangshu.Repository.Knowledge.Casebook

open Wanxiangshu.Repository.Programming.Js

/// CASE-004: replay stored observations against the current worktree —
/// read-only, never writing the subject. FileRead re-reads and re-hashes;
/// GlobResult re-enumerates; GrepResult re-searches. Any missing/changed
/// result makes the whole replay Stale (freshness hint, not proof).
module CasebookReplay =

    let private readHash (root: string) (path: string) : string option =
        match JsUtf8Fs.readUtf8Classified (JsMutationFs.resolveToolPath root path) with
        | Ok text -> Some(CasebookCapture.contentHash text)
        | Error _ -> None

    let private replayGlob root pattern =
        match JsGlobFs.glob root pattern with
        | Ok listing -> Some(Observation.GlobResult(pattern, listing.Paths))
        | Error _ -> None

    let private replayGrep root pattern =
        match JsAnchorFs.grep root (AnchorSpec.Regex pattern) "**/*" with
        | Ok listing ->
            let matches = listing.Matches |> List.map (fun hit -> hit.Path, hit.Line, hit.Text)
            Some(Observation.GrepResult(pattern, matches))
        | Error _ -> None

    /// Replay one observation; None = the observation cannot be reproduced
    /// (missing file / unreadable) — that is a change signal.
    let replayOne (root: string) (observation: Observation) : Observation option =
        match observation with
        | Observation.FileRead(path, _) ->
            readHash root path |> Option.map (fun hash -> Observation.FileRead(path, hash))
        | Observation.GlobResult(pattern, _) -> replayGlob root pattern
        | Observation.GrepResult(pattern, _) -> replayGrep root pattern

    /// Replay the whole stored observation set. Missing any single
    /// observation (deleted file, unreadable) → Stale.
    let replayAll (root: string) (stored: Observation list) : Observation list = stored |> List.choose (replayOne root)
