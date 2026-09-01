namespace Wanxiangshu.Strength

[<RequireQualifiedAccess>]
type StrengthBudget =
    | K0
    | K1
    | K2

module StrengthBudget =
    val parse: string -> StrengthBudget option
    val wire: StrengthBudget -> string
    val requestLimit: StrengthBudget -> int
