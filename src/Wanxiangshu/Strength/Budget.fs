namespace Wanxiangshu.Strength

/// STRENGTH-003: the delegation budget counts provider requests, never tool
/// calls. It is a whole non-negative integer chosen by the owner model as the
/// maximum across one parallel tool batch; there are no tier levels.
[<Struct>]
type ReadonlyRoundBudget = private ReadonlyRoundBudget of int

module ReadonlyRoundBudget =

    let tryCreate value =
        if value < 0 then
            Error "negative-readonly-round-budget"
        else
            Ok(ReadonlyRoundBudget value)

    let value (ReadonlyRoundBudget value) = value

    /// STRENGTH-003: one batch of already-validated budgets collapses to its
    /// maximum. None means the tool set grants no authorization opportunity at
    /// all; Some 0 means do not start a Replica, not a mode of its own.
    let maxOf (budgets: ReadonlyRoundBudget list) : ReadonlyRoundBudget option =
        match budgets with
        | [] -> None
        | _ -> Some(List.maxBy value budgets)
