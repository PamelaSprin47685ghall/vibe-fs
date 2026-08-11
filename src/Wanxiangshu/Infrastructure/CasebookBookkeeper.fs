namespace Wanxiangshu.Infrastructure

open Wanxiangshu.Domain
open Wanxiangshu.Infrastructure.Persist

/// CASE-006 Host Bookkeeper — Q/A synthesis transaction on stale evidence.
///
/// When stored observations are stale vs the current worktree, freeze the
/// replayed observation set, run exactly one `QaSynthesize` provider transaction
/// (one edit-qa turn), then stability-verify by replaying again. A stable freeze
/// publishes InspectorCaseRefreshed with synthesized Q/A + replayed observations.
/// Synthesizer or stability failure keeps the old Case (never a fetch failure).
///
/// Contract: the default synthesizer is deterministic and must revise A (not
/// identity) by appending a single-line evidence digest. No LLM.
module CasebookBookkeeper =

    /// Provider transaction: (q, a, replayed observations) → revised (q, a).
    type QaSynthesize = string -> string -> Observation list -> Result<string * string, string>

    let private evidenceDigest (observations: Observation list) : string =
        let token (observation: Observation) =
            match observation with
            | Observation.FileRead(path, hash) -> "read:" + path + ":" + hash
            | Observation.GlobResult(pattern, paths) ->
                "glob:" + pattern + ":" + (paths |> List.sort |> String.concat ",")
            | Observation.GrepResult(pattern, matches) ->
                let flat =
                    matches
                    |> List.map (fun (path, index, text) -> path + "@" + string index + ":" + text)
                    |> List.sort
                    |> String.concat "|"

                "grep:" + pattern + ":" + flat

        let body =
            observations |> Observations.normalize |> List.map token |> String.concat ";"

        "evidence:" + body

    /// Deterministic default: keep Q, revise A with one evidence line (1 edit-qa turn).
    let defaultSynthesize: QaSynthesize =
        fun q a observations -> Ok(q, a + "\n" + evidenceDigest observations)

    let private synthGate = obj ()
    // DSL-MUTABLE: resource
    let mutable private synthesizer: QaSynthesize = defaultSynthesize

    let setSynthesizer (next: QaSynthesize) : unit =
        lock synthGate (fun () -> synthesizer <- next)

    let resetSynthesizer () : unit = setSynthesizer defaultSynthesize

    /// Run the current synthesizer once. Shared by refresh and CaseFinalize.
    let synthesize (q: string) (a: string) (observations: Observation list) : Result<string * string, string> =
        let fn = lock synthGate (fun () -> synthesizer)
        fn q a observations

    /// Returns Ok true when a Refreshed event was published; Ok false when
    /// Fresh / no-case (nothing to do). Error on store, synthesizer, or
    /// stability-verify failure — the old Case is left intact.
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
                let freeze = CasebookReplay.replayAll root case.Observations

                match synthesize case.Q case.A freeze with
                | Error err -> Error err
                | Ok(q', a') ->
                    let verify = CasebookReplay.replayAll root case.Observations

                    match Observations.classifyReplay freeze verify with
                    | ReplayResult.Stale ->
                        Error "casebook synthesis unstable: worktree changed during provider transaction"
                    | ReplayResult.Fresh ->
                        match CasebookWorkflow.refreshCase store raw sessionId q' a' freeze with
                        | Ok() ->
                            CasebookIndex.invalidate ()
                            CasebookIndex.refresh store raw 256 |> ignore
                            Ok true
                        | Error err -> Error err
