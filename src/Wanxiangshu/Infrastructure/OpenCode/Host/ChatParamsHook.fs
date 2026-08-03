namespace Wanxiangshu.OpenCode

open Wanxiangshu.Journal

/// 0.5.0: Host resolves models from opencode.json agent bindings.
/// chat.params no longer injects EffectiveModel — Agent=Some / Model=None is SSOT.
module ChatParamsHook =

    let create (_journal: AgentJournal option) : obj =
        box (fun (_inputObj: obj) (_outputObj: obj) -> ())
