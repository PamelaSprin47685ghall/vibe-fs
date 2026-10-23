namespace Wanxiangshu.Context.Scope

open Wanxiangshu.Participant.Provider.Projection.ProviderProjection

module Protocol =
    [<Literal>]
    val ResetScopeTool: string = "resetScope"

    [<Literal>]
    val SuffixSeparator: string = "\u0000\uFEFF"

    [<Literal>]
    val UserReminder: string =
        "[System: Estimate the turnBudget for the first fine-grained task block (state closed-loop), and call resetScope immediately before doing anything else.]"

    [<Literal>]
    val BudgetExhaustedSteer: string =
        "[System: Weak, optional reminder — the scope turnBudget is exhausted (0 remaining). If you are not busy and at a natural boundary, consider calling resetScope with a fresh budget. Do not interrupt, pause, or abandon your current work for this; keep going and reset when convenient.]"

    val toObjArray: messages: obj -> obj array
    val budgetSuffix: budget: int -> string
    val appendText: content: obj -> addition: string -> obj
    val appendUserReminder: content: obj -> obj
    val appendBudgetReminder: content: obj -> budget: int -> obj
    val stripContradictoryBudgetSuffixes: messages: obj -> obj array
    val addBudgetExhaustedSteer: messages: obj -> obj array
    val endsWithSideRequest: messages: obj -> bool
    val isScopePrompt: message: obj -> bool
    val promptKey: message: obj -> string
    val lastPromptIndex: messages: obj -> int
    val hasResetSinceLastPrompt: messages: obj -> bool
    val filterScopeMessages: messages: obj -> obj array
    val addUserReminders: messages: obj -> obj array
    val mapArrayIfChanged: arr: obj array -> mapper: (obj -> obj) -> obj array
    val isUnchanged: arr: obj array -> other: obj array -> bool
    val filterWireMessages: messages: WireMessage list -> WireMessage list
