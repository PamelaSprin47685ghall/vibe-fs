// ⑤ 表示变换（设计目录）：组合子。守恒义务词法化——缺一段前提即规则不可用（演算 H）。
// 变换只能改写问题，不能产出答案；无法证明守恒性质时只能停在候选（§13.2⑤）。
// 编译顺序：14（依赖 Boundary、Meditation）。
module Meditator.Methods.Transforms

open Meditator.Boundary
open Meditator.Meditation

/// 守恒验证的回执：非空 witness 列表 = 通过；空 = 守恒性质未证明。
type PreservationCheck = Meditation<VerifierWitness list>

/// H-relax（§14.4 词法范例）：relaxation。
/// 四段前提：松弛、求解、投影回来、硬约束验证。缺一即编译不过——
/// 不存在运行时"记一条以后要 project back 的 obligation"。
/// 确定性复核 witness 由程序集内权柄签发（P0-1：不接收外部权柄）。
let relax
    (relaxProblem: 'h -> Meditation<'r>)
    (solveRelaxed: 'r -> Meditation<'a>)
    (projectBack: 'a -> Meditation<'h>)
    (validateHard: 'h -> Meditation<VerifierWitness list>)
    (source: 'h)
    : Meditation<Result<Validated<'h>, 'h>> =
    meditation {
        let! relaxed = relaxProblem source
        let! answer = solveRelaxed relaxed
        let! projected = projectBack answer
        let! witnesses = validateHard projected

        match Validated.create Verifiers.deterministicCheck witnesses projected with
        | Ok validated -> return Ok validated
        | Error _ -> return Error projected // 投影回硬约束失败：只能停在候选
    }

/// H-simplify：simplification。SafelySimplified 构造私有（Boundary）——
/// 无法证明 preserved + 完成 loss audit，就造不出安全简化结果。
/// 禁止 closeUnknown（§14.3）：返回类型没有 UnknownCoverage 通道。
/// 损失审计有否决权：losses 非空即停在候选（Error），witness 不豁免（评审：损失审计无否决权）。
let simplify
    (reduce: 'a -> Meditation<'b>)
    (provePreserved: 'a -> 'b -> Meditation<VerifierWitness list>)
    (auditLoss: 'a -> 'b -> Meditation<string list>)
    (source: 'a)
    : Meditation<Result<Validated<'b>, string list>> =
    meditation {
        let! reduced = reduce source
        let! witnesses = provePreserved source reduced
        let! losses = auditLoss source reduced

        if not (List.isEmpty losses) then
            return Error losses
        else
            match Validated.create Verifiers.deterministicCheck witnesses reduced with
            | Ok validated -> return Ok validated
            | Error _ -> return Error losses
    }

/// H-dual：duality。影子问题求解 + 回拉；gap 必须显式（§13.2⑤ correspondence map + duality gap）。
let viaDual
    (toDual: 'a -> 'b)
    (solveDual: 'b -> Meditation<'r>)
    (pullBack: 'r -> Meditation<'a>)
    (estimateGap: 'a -> 'r -> Meditation<string option>)
    (source: 'a)
    : Meditation<'a * string option> =
    meditation {
        let dual = toDual source
        let! dualAnswer = solveDual dual
        let! pulled = pullBack dualAnswer
        let! gap = estimateGap source dualAnswer
        return (pulled, gap)
    }

// 其余七个签名（设计目录 ⑤；形态相同：守恒义务词法化为函数参数）：
//
// val transformEquivalent :
//     transform:('a -> Meditation<'b>) -> provePreserved:('a -> 'b -> PreservationCheck)
//     -> source:'a -> Meditation<Result<Validated<'b>, 'b>>
//     守恒义务 = preserved observables
//
// val withAuxiliary :
//     construct:Meditation<'aux> -> use:('aux -> Meditation<'r>) -> discharge:('r -> PreservationCheck)
//     -> Meditation<Result<'r, 'aux>>
//     守恒义务 = discharge 辅助对象（auxiliary_construction）
//
// val decompose :
//     split:('a -> Meditation<'parts>) -> recombine:('parts -> Meditation<'b>)
//     -> contract:('parts -> PreservationCheck) -> source:'a -> Meditation<Result<'b, 'parts>>
//     守恒义务 = 接口契约（decomposition_recombination）
//
// val reduceDimension :
//     project:('a -> 'b) -> reasonInSlice:('b -> Meditation<'r>) -> liftBack:('r -> Meditation<'a>)
//     -> auditLift:('a -> Meditation<string list>) -> source:'a -> Meditation<'a * string list>
//     守恒义务 = dropped dimensions + lift risks（dimensional_reduction）
//
// val viaQuotient :
//     equiv:('a -> 'a -> bool) -> solveOnClass:('a -> Meditation<'r>)
//     -> liftMap:('a -> 'r -> Meditation<'a list>) -> source:'a -> Meditation<'a list>
//     守恒义务 = equivalence relation（quotient_space）
//
// val mapStructure :
//     objectMap:('a -> 'b) -> morphismMap:(('a -> 'a) -> ('b -> 'b)) -> proveCommutes:PreservationCheck
//     -> source:'a -> Meditation<Result<'b, 'a>>
//     守恒义务 = 图交换性（category_mapping）
//
// val coarseGrain :
//     microMap:('m -> 'M) -> relevant:string list -> macroReason:('M -> Meditation<'r>)
//     -> source:'m -> Meditation<'r * string list>
//     守恒义务 = scale-relevant variables（renormalization）
