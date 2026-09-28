namespace Wanxiangshu.Strength

[<Struct>]
type ReadonlyRoundBudget = private ReadonlyRoundBudget of int

module ReadonlyRoundBudget =
    val tryCreate: value: int -> Result<ReadonlyRoundBudget, string>
    val value: budget: ReadonlyRoundBudget -> int

    val maxOf: budgets: ReadonlyRoundBudget list -> ReadonlyRoundBudget option
