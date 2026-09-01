namespace Wanxiangshu.Context.Prefix

[<RequireQualifiedAccess>]
module PrefixSurface =
    val empty: obj
    val snapshot: value: obj -> obj
    val requestKindLabels: string array
    val requestKindMayCarryProbe: kind: string -> bool
    val requestKindLabel: kind: string -> string
    val requestKind: obj
    val select: value: obj -> obj
    val applyRebase: request: obj -> state: obj -> obj
    val applyReanchor: request: obj -> state: obj -> obj
    val epochOf: state: obj -> int64
    val hasSnapshot: state: obj -> bool
    val reanchoredRuns: state: obj -> string array
    val isReanchored: run: string -> state: obj -> bool
    val forSnapshot: snapshot: obj -> memoryPreamble: string -> memoryBody: string -> obj
    val forChoice: choice: obj -> committed: obj -> memoryPreamble: string -> memoryBody: string -> obj
    val requiredBlob: choice: obj -> committed: obj -> obj
    val retainTodoWriteRounds: messages: obj array -> bool array
