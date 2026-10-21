namespace Wanxiangshu.Host.Contract

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host

/// JS-native boundary for HOST-006 compaction prevention + containment policy.
///
/// The policy module (`HostCompactionPolicy`) is pure F# with typed unions
/// (`CompactionGateVerdict`, `CompactionSetting`) and `ProviderRunIdentity`
/// parameters. This surface translates those into JSON-shaped objects so
/// semantic tests observe the real policy without touching Fable representation.
module CompactionPolicySurface =

    [<Import("Prefix", "wanxiangli/prefix")>]
    let private prefixOwnerConstructor: obj = jsNative

    let private prefixOwner: obj = createNew prefixOwnerConstructor ()

    let private settingPath (setting: CompactionSetting) = setting.Path |> String.concat "."

    let private verdictToJs (verdict: CompactionGateVerdict) : obj =
        let message = HostCompactionPolicy.describeVerdict verdict

        match verdict with
        | CompactionGateVerdict.Satisfied ->
            box
                {| kind = "Satisfied"
                   message = message |}
        | CompactionGateVerdict.SettingUnavailable setting ->
            box
                {| kind = "SettingUnavailable"
                   path = settingPath setting
                   required = setting.Required
                   clause = setting.Clause
                   reason = setting.Reason
                   message = message |}
        | CompactionGateVerdict.CompactedDespiteSettings(session, runs) ->
            box
                {| kind = "CompactedDespiteSettings"
                   session = SessionId.value session
                   runs = runs
                   message = message |}

    /// Required prevention settings as JSON: `{ path, required, clause, reason }`.
    let requiredSettings () : obj =
        HostCompactionPolicy.requiredSettings
        |> List.map (fun setting ->
            box
                {| path = settingPath setting
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

        let handled =
            typed
            |> List.filter predicate
            |> List.map ProviderRunIdentity.value
            |> List.toArray

        let state =
            createObj
                [ "epoch" ==> "0"
                  "compactionRuns" ==> handled
                  "tenures" ==> Array.empty<string> ]

        let candidate: obj = prefixOwner?proposeCompaction (state, observed)
        if isNull candidate then null else candidate?evidence?run

    /// HOST-006 startup probe: compaction pseudo-runs inside the first-turn window.
    ///
    /// `messages` is an array of `{ completedAssistant, compaction }` booleans,
    /// oldest first. Returns the count, or null while the first turn is open.
    let firstTurnCompactionRuns (messages: obj array) : obj =
        let window =
            messages
            |> Array.toList
            |> List.map (fun raw ->
                { CompletedAssistant = unbox<bool> raw?completedAssistant
                  Compaction = unbox<bool> raw?compaction })

        match HostCompactionPolicy.firstTurnCompactionRuns window with
        | Some count -> box count
        | None -> null

    /// HOST-006 startup probe verdict as JSON:
    ///   `{ kind: "Satisfied", message }`
    ///   `{ kind: "SettingUnavailable", path, required, clause, reason, message }`
    ///   `{ kind: "CompactedDespiteSettings", session, runs, message }`
    let judgeFirstTurn (session: string) (pseudoRunsOnFirstTurn: int) : obj =
        HostCompactionPolicy.judgeFirstTurn None (SessionId.create session) pseudoRunsOnFirstTurn
        |> verdictToJs

    /// HOST-006 startup verdict when one required setting could not be established.
    let judgeFirstTurnWithUnavailable (unavailablePath: string) (session: string) (pseudoRunsOnFirstTurn: int) : obj =
        let unavailable =
            HostCompactionPolicy.requiredSettings
            |> List.find (settingPath >> (=) unavailablePath)

        HostCompactionPolicy.judgeFirstTurn (Some unavailable) (SessionId.create session) pseudoRunsOnFirstTurn
        |> verdictToJs
