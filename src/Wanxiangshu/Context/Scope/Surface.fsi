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
    val bindDelegation: recipient: string -> delegator: string -> unit
    val askNegotiation: recipient: string -> questions: string -> obj
    val prepareNegotiationReply: receipt: string -> answer: string -> obj
    val completeNegotiationReply: receipt: string -> bool
    val confirmNegotiationSteer: receipt: string -> recipient: string -> bool
    val negotiationRequired: recipient: string -> bool
    val planFilter: messages: obj -> obj array
    val appendTailOnce: content: obj -> suffix: string -> obj
    val writeUserHint: message: obj -> texts: string array -> obj
    val writeToolResultHint: message: obj -> texts: string array -> obj
    val stripKnownHints: message: obj -> texts: string array -> obj
    val userCanCarryHint: message: obj -> bool
    val toolCanCarryHint: message: obj -> bool
    val rememberSystem: sessionId: string -> system: string array -> unit
    val rememberMessages: sessionId: string -> messages: obj array -> unit
    val rememberModel: sessionId: string -> model: obj -> unit
    val modelFor: sessionId: string -> obj
    val startTemporary: tempId: string -> delegatorId: string -> obj
    val isTemporary: sessionId: string -> bool
    val finishTemporary: tempId: string -> unit
    val recordToolViolation: tempId: string -> unit
    val toolViolationCount: tempId: string -> int
    val forgetSession: sessionId: string -> unit
    val projectSystem: tempId: string -> string array
    val projectMessages: tempId: string -> question: obj -> obj array
