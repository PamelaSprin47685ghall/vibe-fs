namespace Wanxiangshu.Host.Contract

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host

/// JS-native boundary for HOST-006 compaction prevention + containment policy.
///
/// The policy module (`HostCompactionPolicy`) is pure F# with typed unions
/// (`CompactionGateVerdict`, `CompactionSetting`) and `ProviderRunIdentity`
/// parameters. This surface translates those into JSON-shaped objects so
/// semantic tests observe the real policy without touching Fable representation.
module CompactionPolicySurface =

    /// Required prevention settings as JSON: `{ path, required, clause, reason }`.
    let requiredSettings () : obj =
        HostCompactionPolicy.requiredSettings
        |> List.map (fun setting ->
            box
                {| path = setting.Path |> String.concat "."
                   required = setting.Required
                   clause = setting.Clause
                   reason = setting.Reason |})
        |> List.toArray
        |> box

    /// `experimental.compaction.autocontinue` answer (always false).
    let autoContinueEnabled () : bool =
        HostCompactionPolicy.autoContinueEnabled

    /// HOST-006 containment: is this message a Host compaction pseudo-run.
    let isContainableCompaction (isCompaction: bool) : bool =
        HostCompactionPolicy.isContainableCompaction isCompaction

    /// HOST-006 containment: newest unhandled compaction run, or null.
    ///
    /// `observed` is an array of raw run-id strings (oldest first).
    /// `isReanchored` is a JS function `(runId: string) => boolean`.
    let nextReanchor (observed: string array) (isReanchored: obj) : obj =
        let predicate (runId: ProviderRunIdentity) =
            (isReanchored :?> (string -> bool)) (ProviderRunIdentity.value runId)

        let typed = observed |> Array.map ProviderRunIdentity.create |> Array.toList

        match HostCompactionPolicy.nextReanchor typed predicate with
        | Some runId -> box (ProviderRunIdentity.value runId)
        | None -> null

    /// HOST-006 startup probe verdict as JSON:
    ///   `{ kind: "Satisfied" }`
    ///   `{ kind: "SettingUnavailable", path, required, clause, reason }`
    ///   `{ kind: "CompactedDespiteSettings", session, runs }`
    let judgeFirstTurn (session: string) (pseudoRunsOnFirstTurn: int) : obj =
        HostCompactionPolicy.judgeFirstTurn None (SessionId.create session) pseudoRunsOnFirstTurn
        |> function
            | CompactionGateVerdict.Satisfied -> box {| kind = "Satisfied" |}
            | CompactionGateVerdict.SettingUnavailable setting ->
                box
                    {| kind = "SettingUnavailable"
                       path = setting.Path |> String.concat "."
                       required = setting.Required
                       clause = setting.Clause
                       reason = setting.Reason |}
            | CompactionGateVerdict.CompactedDespiteSettings(session, runs) ->
                box
                    {| kind = "CompactedDespiteSettings"
                       session = SessionId.value session
                       runs = runs |}
