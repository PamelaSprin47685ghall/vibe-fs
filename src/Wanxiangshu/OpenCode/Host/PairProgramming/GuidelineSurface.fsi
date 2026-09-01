namespace Wanxiangshu.OpenCode.Host.PairProgramming

[<RequireQualifiedAccess>]
module GuidelineSurface =
    val empty: obj
    val nextOrdinal: state: obj -> int64
    val pairs: state: obj -> obj array
    val visiblePairs: state: obj -> obj array
    val applyReanchor: state: obj -> obj
    val apply: request: obj -> state: obj -> obj
