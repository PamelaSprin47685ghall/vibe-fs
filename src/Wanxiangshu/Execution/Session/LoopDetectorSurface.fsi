namespace Wanxiangshu.Execution.Session

[<RequireQualifiedAccess>]
module LoopDetectorSurface =
    val vocabularySize: int
    val halfLife: float
    val lambda: float
    val normalWeightedDistinctCount: float
    val centralProbability: float
    val lowerQuantileProbability: float
    val upperQuantileProbability: float
    val minimumWeightedDistinctCount: float
    val maximumWeightedDistinctCount: float
    val constants: unit -> obj
    val classify: weightedDistinctTokens: float -> string
    val create: unit -> obj
    val trackedTokenCount: handle: obj -> int
    val evaluate: handle: obj -> obj
    val pushText: handle: obj -> text: string -> obj
    val isLoopTextDelta: raw: obj -> bool
    val tryDecodeTextDelta: raw: obj -> obj
