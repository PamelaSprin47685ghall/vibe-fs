# obligation-ledger — WHAT

## OBLIGATION-LEDGER-001: 当前义务是当前仍欠的工作而非 workflow status

`CurrentObligations` 始终描述在当前 relation 下仍欠什么。Pre-T1（首次 accepted `planComplete=true` 之前）时，它诚实记录把计划做完仍欠的调查、分析、分解与决策；Post-T1（承诺建立后）时，它只描述为了满足请求仍需成为真的 mission 结果与证据。禁止携带 `kind`、`id`、`status`、`priority` 之类 provider-visible 冷状态。每项 obligation 可携带相对当前执行前沿的 `horizon` 分辨率；该字段只表达展开粒度，不是生命周期状态。

## OBLIGATION-LEDGER-002: obligations wire 与 CurrentObligations 定义

协议 wire 格式固定为：
```text
todowrite(planComplete: bool, workingOn: string, obligations: [{ name: string, horizon: near|mid|far, work: string }])
CurrentObligations = last accepted obligations list
effectivePlanComplete(k) = OR(planComplete of Accepted T1..Tk)
```
`name` 在同一 obligation 存续期间保持稳定；`work` 描述该义务所欠内容；`horizon` 表达规划分辨率（`near` 可直接着手闭环，`mid` 保留下一层结果与依赖，`far` 粗粒度覆盖剩余债务）。

`workingOn` 是当前实际工作焦点与透视原点。非空 account 中，规范化后的 `workingOn` 必须精确命名一个 obligation，但 horizon 不参与 admission。若输入拼写未 exact 命中，按 Levenshtein 编辑距离在全部 obligations 中归一到最近 `name`，并列时取原顺序首个；空 account 归一为空字符串。`workingOn` 仅决定当前焦点与 Host 兼容投影，不是 obligation status，也不进入 `CurrentObligations` 元素本身。

义务当欠则留，凭工作挣得后移除；禁止仅为缩短列表删除仍欠义务，亦禁止为保留历史留存已履行业务。粗粒度义务被细化时，直接由新暴露的 obligations 替代。

## OBLIGATION-LEDGER-003: 禁止用 status 枚举伪装进度

禁止把任务进度伪装为 provider-visible 的 status 枚举机。真实进度由工作闭环与下一轮 truthful account 判断。Host UI 或兼容层中的 status 字段仅为单向投影（`workingOn` 命中的行投影为 `in_progress`，其余为 `pending`），不得反推 canonical obligation truth。

## OBLIGATION-LEDGER-004: planning work 只在 commitment 前合法

当 effective `planComplete=false` 时，调查仓库、分析请求、列出工作与设计方案等 planning work 是合法 obligation；当 effective `planComplete=true` 后，同类条目若仅增加 Manager 自身理解或清单，不再构成 mission obligation，必须改写为对外部世界或交付物仍欠的结果与证据。Host 判定仅依据 durable commitment latch，不得使用自然语言关键词分类器。

## OBLIGATION-LEDGER-005: obligation 必须可闭环

无论处于 Pre-T1 还是 Post-T1，每项 obligation 必须包含足以判断其完成的具体 owed work。Pre-T1 闭环于规划产出（如明确事实或完成分解），Post-T1 闭环于交付结果与关闭证据。`placeholder: planning`、`TBD` 或裸阶段名等无实际 work 的槽位条目均不构成合法 obligation。

## OBLIGATION-LEDGER-006: obligation identity 与连续性

同一 proposed account 内出现重复 `name` 或空白 `name` 均触发调用语法拒绝，作为当前 tool 红字返回。禁止通过 `work` 文本相似度猜测 obligation identity；Host 内部若需稳定标识，不得穿透至 provider 视野。

## OBLIGATION-LEDGER-007: 同 message 多已 materialize todowrite 顺序执行与单 inflight

同一 assistant message 出现多个 `ToolCallId` 的 `todowrite` 时，支持同回合多次调用并按顺序执行语义串行生效：第 $k$ 次调用 accepted 的账目作为第 $k+1$ 次调用的 base，后一次调用自然更新前一次调用；流式构造期间无输入的 pending stub 不计为第二调用。T1 commitment 仅在首个 accepted `planComplete=true` 时揭示一次。同一 Manager Life 同时最多存在一个进行中的 checkpoint admission 事务。

## OBLIGATION-LEDGER-008: 同 ToolCallId replay 幂等

相同 `ToolCallId` 的重放必须幂等收敛至同一 checkpoint identity 与同一 obligation account。其 input digest、Life、BaseObligations 及 ordinal 契约必须与既有 Prepared 一致；若发生冲突，属于基础设施不变量破坏，必须立即 fatal，不得降格为工具红字。

## OBLIGATION-LEDGER-009: 失败分型（语法红字与系统故障）

系统严格区分两类失败：
1. **调用语法与形态失败**：只拒绝当前 `todowrite`，向 provider 返回工具红字。
2. **基础设施故障**：包括 Snapshot、Journal、digest 校验或 Projection 不一致，必须立即输出诊断并终止进程，绝不向模型返回工具错误。

## OBLIGATION-LEDGER-010: Accepted 立即 supersede CurrentObligations

`TodoWritePrepared` 仅冻结调用发生前的 `BaseObligations` 与本次提交的 `Submitted`，不改变 Current。`TodoWriteAccepted` 一旦 durable，提交的 account 立即 supersede 原 `CurrentObligations`。不存在「已 accepted 但尚未生效」的中间态，崩溃恢复仅重放 Accepted 事实链。

## OBLIGATION-LEDGER-011: 账本变更完全由 Accepted 事实驱动

`CurrentObligations` 的演进完全由 `TodoWriteAccepted` 事实链驱动，不存在未决过程评审的回滚或自动 merge。Manager 在推进工作后，通过后续 `todowrite` 提交新的完整 account，由新的 Accepted 自然 supersede 当前账目。

## OBLIGATION-LEDGER-012: checkpoint 的 SSOT 为 TodoWriteAccepted

`TodoWriteAccepted` 是 checkpoint 事实的唯一真相源。每个 Accepted checkpoint 拥有独立的 `TodoWriteId` 并记录当前账目快照。

## OBLIGATION-LEDGER-013: 无过程评审阻塞与保留 todo lag-1 折叠

移除过程性评审，$T_k$ 的 Accepted 立即生效，不派生过程评审义务，亦不阻塞后续 $T_{k+1}$ 的提交与执行。同时严格保留基于 committed Accepted 链的 desired lag-1 cutoff 与 prefix rebase 折叠行为。

## OBLIGATION-LEDGER-014: 移除中间过程评审，终结资格直达 Finality

各 checkpoint 之间无过程性评审门禁，Manager 可无缝推进工作。质量验收与终结裁决完全由终局评审（Finality Review）在 mission 结束时统一执行。

## OBLIGATION-LEDGER-015: canonical 单真相源 vs Host compatibility sink

Journal facts 与 `MagicTodoProjection` 是账本的唯一语义真相源，Host TodoTable 仅为兼容 UI 投影。禁止用 Host 表反推或恢复 canonical obligations。REVISE 不回滚 canonical 账本，亦不得将 Host sink 刷回旧账；若发生状态漂移，仅允许执行无副作用的纯投影修复。

## OBLIGATION-LEDGER-016: T1 commitment 与 Opening 关闭

每次 `todowrite` 显式提交 `planComplete: bool`。当前 Life 首次 accepted `planComplete=true` 构成不可逆的 T1 commitment；在此之前任意数量的 accepted `false` 均为合法 Planning Table checkpoint。T1 确立后，effective planComplete 永久为 true，后续传入 false 仍按 true 解释并要求 mission-debt 账本。T1 的交托确认仅通过 conversation tool result 传递，不切换 system prompt 或角色规范。

## OBLIGATION-LEDGER-017: Manager BlindPlan Opening（无生产 Activation）

Manager 采用 BlindPlan 开启策略。Pre-T1 处于 Planning Table：为后续执行制定计划，允许调查但不得直接执行所规划路径。当权威输入证明当前无待办任务时，`planComplete=true, workingOn="", obligations=[]` 是合法的零债务 T1，随后按 Finality 协议接受终审。生产路径不存在单独的 Activation 阶段机或 prompt 切换。

## OBLIGATION-LEDGER-018: 恢复只从 durable facts

系统状态恢复仅依赖 durable facts 经 Boot Fold 重建增量 projection facts，随后重入普通 workflow。禁止在业务热路径全表扫描或重放完整 Journal。`planComplete` 单调性均通过增量 locator 在 $O(1)$ 复杂度内查询。升级解码旧版本缺失的 `PlanCompleteDeclared` 字段时，严格解码为 `true` 以保持历史承诺。

## OBLIGATION-LEDGER-019: 新 Life 账本为空

新开启的 Life 其 `CurrentObligations` 初始严格为空，绝不从 Host TodoTable 自动继承上一 Life 的遗留条目。升级瞬间的历史开放 Life 仅允许执行一次受控的 legacy seed，且必须在首次 provider request 之前完成。

## OBLIGATION-LEDGER-020: 质量验收统一归于 Finality Review

每个 checkpoint 不再创建或注册专职过程 Reviewer，全生命周期的质量保障与双重 PERFECT 裁决统一收口于 Finality Review。

## OBLIGATION-LEDGER-021: desired lag-1 cutoff 仅由 committed Accepted 子链推导

Pre-T1 的 `planComplete=false` checkpoints 属于开放的 Opening，不派生 Prefix rebase。只有 commitment 之后的 Accepted 子链（$E_k=true$）才使 desired lag-1 cutoff 可推导。首个 committed checkpoint（T1）无 prior cutoff，后续 committed checkpoint 使用上一 committed checkpoint 的调用前位置。

## OBLIGATION-LEDGER-022: checkpoint 无过程评审阻塞，tail 直接进入 Finality

checkpoint 提交不产生未决过程评审负担，Manager 工作就绪后可直接调用 `suicide` 进入 Finality Review，无需等待或抽干中间过程评审。

## OBLIGATION-LEDGER-023: MagicTodoManagerGuideline 的 Manager-only 语义

账本指引属于 Manager 专有规范，涵盖义务增删规则（当欠则留、挣得则除）、checkpoint 连续性、Pre-T1 planning 维护、T1 不可逆承诺以及渐进细化纪律。该指引独立于全局配对文案，且不得向模型泄露隐藏评审编排机制。

## OBLIGATION-LEDGER-024: tool.definition 唯一广告点

`tool.definition` 是 V2 schema 的唯一 Host 广告点，必须同步更新 parameters、jsonSchema 与 description。描述需完整表达 Manager 可见规则与透视纪律，禁止提及 dedicated reviewer、barrier、witness 等隐藏机制。

## OBLIGATION-LEDGER-025: before 合同（materialization 与 Prepared 冻结）

`tool.execute.before` 同步阶段仅执行 provider 参数解码与内存兼容投影，并发起延迟 prepare，不等待 I/O 或启动评审。延迟 prepare 在完整 Snapshot 中唯一定位当前调用，将调用前的 transcript 前缀同步至 XTrace，固化 exact `ReviewFrontier` 并 durable `TodoWritePrepared`。

## OBLIGATION-LEDGER-026: after 合同（Accepted 与富化 T1 result）

`after` 仅在物理执行成功返回后触发：幂等写入 `TodoWriteAccepted`，推导 desired lag-1 cutoff。首次 T1 确认时在富化结果中揭示交托确认。

## OBLIGATION-LEDGER-027: 透视粒度（complete coverage 而非 uniform decomposition）

账本是非空 account 沿当前 `workingOn` 向未来的透视视图：
1. `near`：执行级粒度，可直接着手且有独立闭环证据。
2. `mid`：结果级粒度，明确下一层产出与依赖，暂不展开内部步骤。
3. `far`：覆盖级粒度，粗粒度覆盖剩余已知债务。
随执行前沿推进，粗粒度债务逐步由细粒度 obligations 替换。`planComplete=true` 仅要求道路完整覆盖且近处可执行，禁止要求将所有远期工作均拆解为 `near` 粒度。

## OBLIGATION-LEDGER-028: ledger fatal绑定checkpoint settlement与注入fuse

只有typed materialization/acceptance invariant incident可以请求fatal；Prepared、physical success与Accepted checkpoint的exact durable状态必须先settle。MagicTodo membrane/Host codec只接受composition注入的mandatory fatal capability，不得直接引用physical adapter、optional/default/global fallback。同一incident只允许一次report与kill；普通输入错误、unknown physical success与业务拒绝保持typed nonfatal。
