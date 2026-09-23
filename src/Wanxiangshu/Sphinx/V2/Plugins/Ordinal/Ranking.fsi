namespace Wanxiangshu.Sphinx.V2.Plugins

module Ranking =
    /// Joint best/worst likelihood. Returns None when theta is incomplete for the set.
    val maxDiffLogProbability: Map<string, float> -> string list -> string -> string -> float option

    /// Strict complete ranking under Plackett–Luce. None when the order is partial.
    val plackettLuceLogProbability: Map<string, float> -> string list -> float option

    /// Pairs from one ranked ballot. The cluster must travel with them.
    val compositePairs: string list -> (string * string) list

    /// Descriptive baseline only. Never a likelihood, never an uncertainty.
    val bordaCounts: string list list -> string list -> Map<string, float>
