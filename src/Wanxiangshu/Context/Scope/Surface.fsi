namespace Wanxiangshu.Context.Scope

open Wanxiangshu.Foundation.Identity

module Surface =
    val toObjArray: messages: obj -> obj array
    val filterScopeMessages: messages: obj -> obj array
    val appendUserReminder: content: obj -> obj
    val appendBudgetReminder: content: obj -> budget: int -> obj
    val budgetSuffix: budget: int -> string
    val addBudgetExhaustedSteer: messages: obj -> obj array
    val stripContradictoryBudgetSuffixes: messages: obj -> obj array
    val isScopePrompt: message: obj -> bool
    val hasResetSinceLastPrompt: messages: obj -> bool
    val lastPromptIndex: messages: obj -> int
    val promptKey: message: obj -> string
    val endsWithSideRequest: messages: obj -> bool
    val mapArrayIfChanged: arr: obj array -> mapper: (obj -> obj) -> obj array
    val isUnchanged: arr: obj array -> other: obj array -> bool
    val addUserReminders: messages: obj -> obj array

    val turnBudget: sessionId: string -> int
    val isScopeInitialized: sessionId: string -> bool
    val onTurnStart: sessionId: string -> unit
    val onNewPrompt: sessionId: string -> promptKey: string -> timestamp: float option -> hasReset: bool -> unit
    val markScopeInitialized: sessionId: string -> initialized: bool -> unit
    val resetScopeState: sessionId: string -> budget: int -> unit
    val clearSessionState: sessionId: string -> unit
