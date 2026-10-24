namespace Wanxiangshu.Context.Scope

module Surface =
    [<Literal>]
    val ResetScopeTool: string = "resetScope"
    [<Literal>]
    val SuffixSeparator: string = "\u0000\uFEFF"

    val toObjArray: messages: obj -> obj array
    val isUnchanged: first: obj array -> second: obj array -> bool
    val budgetSuffix: budget: int -> string
    val turnBudget: sessionId: string -> int
    val isScopeInitialized: sessionId: string -> bool
    val onTurnStart: sessionId: string -> unit
    val resetScopeState: sessionId: string -> budget: int -> unit
    val clearSessionState: sessionId: string -> unit
    val planRequest: sessionId: string -> messages: obj -> obj
    val planFilter: messages: obj -> obj array
    val appendTailOnce: content: obj -> suffix: string -> obj
    val writeUserHint: message: obj -> texts: string array -> obj
    val writeToolResultHint: message: obj -> texts: string array -> obj
    val stripKnownHints: message: obj -> texts: string array -> obj
    val userCanCarryHint: message: obj -> bool
    val toolCanCarryHint: message: obj -> bool
