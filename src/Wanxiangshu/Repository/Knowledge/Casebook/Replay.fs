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

    let private replayGlob root pattern : System.Threading.Tasks.Task<Observation option> =
        task {
            let! globRes = JsGlobFs.glob root pattern

            match globRes with
            | Ok listing -> return Some(Observation.GlobResult(pattern, listing.Paths))
            | Error _ -> return None
        }

    let private replayGrep root pattern : System.Threading.Tasks.Task<Observation option> =
        task {
            let! grepRes = JsAnchorFs.grep root (AnchorSpec.Regex pattern) "**/*"

            match grepRes with
            | Ok listing ->
                let matches = listing.Matches |> List.map (fun hit -> hit.Path, hit.Line, hit.Text)
                return Some(Observation.GrepResult(pattern, matches))
            | Error _ -> return None
        }

    /// Replay one observation; None = the observation cannot be reproduced
    /// (missing file / unreadable) — that is a change signal.
    let replayOne (root: string) (observation: Observation) : System.Threading.Tasks.Task<Observation option> =
        task {
            match observation with
            | Observation.FileRead(path, _) ->
                return readHash root path |> Option.map (fun hash -> Observation.FileRead(path, hash))
            | Observation.GlobResult(pattern, _) -> return! replayGlob root pattern
            | Observation.GrepResult(pattern, _) -> return! replayGrep root pattern
        }

    /// Replay the whole stored observation set. Missing any single
    /// observation (deleted file, unreadable) → Stale.
    let replayAll (root: string) (stored: Observation list) : System.Threading.Tasks.Task<Observation list> =
        task {
            let results = ResizeArray<Observation>()

            for obs in stored do
                let! replayed = replayOne root obs
                replayed |> Option.iter results.Add

            return Seq.toList results
        }
