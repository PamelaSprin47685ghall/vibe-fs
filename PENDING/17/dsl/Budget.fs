// Meditator DSL — 资源片段。语法化：演算 R1–R3 + 势函数。
// 守恒无法静态证明：类型守形状，属性测试守代数（T4），门禁做文本级启发式并承认限度（P8）。
// R3：consume 超额、负数分量、非正 consume 都是非法状态（Error），不是可修输入——不 clamp。
// 编译顺序：3（无依赖）。
module Meditator.Budget

/// 探索额度：整数预算。唯一的合法分配律是严格下降（R1）。
/// Credit 与概率质量无转换（§9.3）：不同类型，不同守恒对象。
type Budget = { Remaining: int; Spent: int }

module Budget =

    let create (credits: int) : Budget =
        if credits < 0 then
            invalidArg (nameof credits) "initial credit must be non-negative"

        { Remaining = credits; Spent = 0 }

    let canContinue (b: Budget) : bool = b.Remaining > 0

    /// R1 + R3：每次展开至少消耗 1；超额 = 非法状态（Error），不是可修输入。
    let consume (amount: int) (b: Budget) : Result<Budget, string> =
        if amount < 1 then
            Error "R1: every expansion consumes at least one credit"
        elif amount > b.Remaining then
            Error $"R3: consume {amount} exceeds Remaining {b.Remaining}"
        else
            Ok
                { Remaining = b.Remaining - amount
                  Spent = b.Spent + amount }

    /// R2 + R3：子义务额度分配。守恒违规是非法状态（Error），不是可修输入——不 clamp；
    /// 分量必须 ≥ 1——负数会凭空增加 Remaining（评审：allocate 未禁止负数）。
    let allocate (amounts: int list) (b: Budget) : Result<Budget, string> =
        match amounts |> List.tryFind (fun a -> a < 1) with
        | Some bad -> Error $"R3: allocation component must be >= 1 (got {bad})"
        | None ->
            let total = List.sum amounts

            if total > b.Remaining - 1 then
                Error $"R1 violated: Σ children ({total}) exceeds parent − 1 ({b.Remaining - 1})"
            else
                Ok
                    { Remaining = b.Remaining - total
                      Spent = b.Spent + total }

    /// P1-3：从历史恢复预算——Spent 不再归零（Remaining = initial − consumed，Spent = consumed），
    /// 账本与预算值一致；超耗 = 非法输入（fail closed）。
    let restore (initialCredit: int) (consumed: int) : Budget =
        if consumed < 0 || consumed > initialCredit then
            invalidArg (nameof consumed) "consumed must be within [0, initialCredit]"

        { Remaining = initialCredit - consumed
          Spent = consumed }

    /// T4 的势函数：Φ = Σ credit(open obligations)。调度步骤使其严格下降。
    let potential (openCredits: int list) : int = List.sum openCredits
