namespace Wanxiangshu.OpenCode

module HookPolicySurface =
    val rows: unit -> obj array
    val acceptsPolicy: criticality: string -> disposition: string -> bool
    val runOptionalCasebookEffect: criticalResult: obj -> effect: (unit -> unit) -> obj
