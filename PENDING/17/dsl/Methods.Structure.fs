// ⑦ 结构设计（设计目录）：产出合法性证书，对象是问题域模型——不是运行时流程的一步。
// 编译顺序：16（依赖 Boundary、Meditation）。
module Meditator.Methods.Structure

open Meditator.Boundary
open Meditator.Meditation

/// state_machine_reasoning：穷尽性证书。非法组合清单是证书的核心内容。
type ExhaustivenessCertificate =
    { MachineName: string
      States: string list
      Transitions: (string * string) list
      IllegalCompositions: string list
      MissingTransitions: (string * string) list }

/// auditMachine：枚举合法状态/转移/非法组合。
/// 纯函数形态：模型即输入，证书即输出；oracle 仅在模型提取期参与（外壳）。
/// 穷尽性只针对 mustCover（必须覆盖的转换）——状态机是稀疏图，
/// 未声明的任意状态间转换不构成缺失（评审：任意两状态间未声明都算 missing 是假穷尽性）。
let auditMachine
    (machineName: string)
    (states: string list)
    (mustCover: (string * string) list)
    (transitions: (string * string) list)
    : Meditation<ExhaustivenessCertificate> =
    meditation {
        let declared = Set.ofList transitions

        let missing = mustCover |> List.filter (fun t -> not (declared.Contains t))

        return
            { MachineName = machineName
              States = states
              Transitions = transitions
              IllegalCompositions = [] // 由调用方以领域规则填充；证书只登记已声明事实
              MissingTransitions = missing }
    }

/// type_driven_design：把非法状态编码为不可表示的代数模型。
// val encodeIllegalStates : DomainSlice -> IllegalState list -> Meditation<AlgebraicModel>
//     产出：DU/record + smart constructor 设计；编译器守新增分支（§13.2⑦）

/// event_sourcing：命令/事实/fold/重放/幂等的合法性审计。
// val auditEventFold : Command list -> Event list -> fold:('s -> 'e -> 's) -> Meditation<ReplayCertificate>
//     产出：命令事件混淆、覆盖写、非幂等 fold 的违例清单
