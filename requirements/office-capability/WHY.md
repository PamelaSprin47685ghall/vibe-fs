# WHY — 为什么 `office-capability` 必须独立存在

## 不可替代的存在理由

系统让一个 participant 把工作托付给另一个 office。托付的依据必须是「对方有资格产生什么后果」，
否则选择面退化成名单/工具白名单，出现两类失败（`docs/why/architecture.md`「Office 认知」备选与被拒）：

- **名单即权威的幻觉**：fork 枚举 calling 名（fast-coder、deep-inspector…）看起来够用，但调用方
  看不到被调用方 Role Law 时，会把 Inspector 当成「另一个能处理 repository 的 agent」，把 Browser
  当成本地调查员，把 Inquiry 当成证人——机器拓扑冒充资格。
- **权限清单冒充能力模型**：把 AGENT-006 权限矩阵抄成「这个 office 能做什么」的口语转写，等于让
  工具的可见性定义 authority。工具可达 ≠ 有权做（「看起来能做」冒充「有权做」）。

本包把 office 的能力钉死在**后果层**：Coder 有资格改变 repository source；Inspector 有资格建立
已存在事实的证据；DevOps 有资格对运行中的世界行动；Browser 有资格带 provenance 建立外部事实；
Inquiry 有资格分辨未决问题的语义。权限矩阵、工具白名单、persona 名都是投影，不是定义。

## RED 是什么样

- office authority 不清、互相重叠，或能产生自己无资格产生的后果。
- 同一 consequence 在不同投影（Manager Role Law / fork description / 各 office Role Law）之间漂移，
  一处说「Inspector 是见证者」另一处说「叫 Inspector 修代码」。
- 把 office 当可互换通用 agent（Coder 只是没 shell 的 Operator；DevOps 是任意难题的逃生口）。
- 同一 office 的 fast/deep calling 名被读成不同 authority。

## 为什么不并进相邻包

- `participant-identity` 答「谁在行动」；本包答「这个 office 有资格产生什么后果」。同一 Role 的
  consequence 跨 Persona/Binding 不变是后果事实，不是身份事实。
- `capability-enforcement` 答「provider 看见的与可执行的 capability 如何同源不扩权」；它消费本包
  的 consequence 模型，但矩阵/gate 同构是 enforcement 的 WHY。
- `delegation` 答「如何按后果把工作转交出去」；它引用 consequence 模型（`entrust-by-consequence`
  锚点已由 delegation 声明），但 consequence 清单本身是本包的。
- `participant-horizon` / `action-affordance` 分别答「什么信息有资格被看见」「调用瞬间的 act
  合同」；consequence 模型是它们引用的唯一 authority。
- 独立变化测试：重画 Inspector 与 DevOps 的 existing-evidence/new-behavior 边界，而 Persona、
  projection、dispatch 完全不动——本包必须能单独承受（boundary card INDEPENDENT CHANGE）。

## 一句话

office 由它有资格产生的后果定义。名字、persona、工具可达性都不能决定 authority。
