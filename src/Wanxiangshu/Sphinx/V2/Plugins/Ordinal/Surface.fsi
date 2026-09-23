namespace Wanxiangshu.Sphinx.V2.Plugins

module Surface =
    val stringSetOf: string list -> Set<string>
    val stringFloatMapOf: (string * float) list -> Map<string, float>
    val listOfItems: string list -> string list
    val listCount: string list -> int
    val isOk: Result<'value, 'error> -> bool
    val isError: Result<'value, 'error> -> bool

    val judgmentOf: string -> Judgment
    val isDirectional: Judgment -> bool
    val isTie: Judgment -> bool
    val isAbstention: Judgment -> bool
    val isConditional: Judgment -> bool
    val labelsWithin: Set<string> -> PairwiseResponse -> Set<string> -> Result<unit, string>

    val logSigmoid: float -> float
    val logSumExp: float -> float -> float
    val compositePairs: string list -> (string * string) list
    val decodeMaxDiff: obj -> Result<MaxDiffObservation, DecodeError>
