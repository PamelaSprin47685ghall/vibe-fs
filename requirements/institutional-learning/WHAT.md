# institutional-learning — WHAT

## INSTITUTIONAL-LEARNING-001: `celebrate` 与 `regret` 记录原始经验而非结构化模板

`celebrate(experience)` 与 `regret(experience)` 接口各接收一段非空自然语言描述。输入允许主观、局部与口语化，不要求调用方预先将其提炼为规则格式、分类故障族或推测 TipName。两者分别代表正向经验（提炼可复制的成功机制）与负向经验（提炼应避免的代价机制），严禁将成功直接量化为数值评分，严禁将后悔等同于既有违规事实。

## INSTITUTIONAL-LEARNING-002: 每个经验仅产出单一 Committed Disposition 且评估有界

每次成功完成的 `LearningOccurrenceId` 最终恰好提交一个 Enhancer disposition。单次 evaluation 仅调用一次私有 Enhancer，且在同一经验上严格输出以下三种互斥结果之一：
- `ABSORB`：既有规则已涵盖该通用机制，不新增、不改写规则；
- `BIRTH`：存在全新的、可复用且未来可再次识别的机制，请求创建新规则；
- `DISCARD`：属于局部偶然、个人偏好、单次事实或无法泛化，不进入制度库。

系统不存在第四种状态、评分阈值或递归增强循环。若 BIRTH 在原子提交前因预期 `RulebookRevision` 漂移而失效，允许在最新 live Rulebook 上至多重新评估一次；再次冲突则显式失败，严禁提交不一致的中间状态。

## INSTITUTIONAL-LEARNING-003: Enhancer 旨在提炼通用机制而非改写单次经历

Enhancer 必须聚焦回答「本次经历揭示了何种比单次经历更一般的机制」。提炼过程仅以当前传入的经验文本与当前 canonical live Rulebook 为输入，严禁为了制定规则而扩大发起新的外部网络或仓库调查，严禁将局部命令流水、具体文件路径或单次时间戳未经抽象直接升格为永久规则。

## INSTITUTIONAL-LEARNING-004: 仅 BIRTH 允许提炼新规则且必须通过准入校验

BIRTH 必须生成满足 `behavior-diagnosis` 合同的新规则 candidate（包含唯一 TipName 与中英双语的 EnforcerText 与 MainText），并提交给 `behavior-diagnosis` 的准入边界。只有准入校验通过方可实现制度化；ABSORB 与 DISCARD 均产生零规则变更。严禁维护运行时专用的临时规则库，新规则的持久化与生效统一由 `behavior-diagnosis` 裁决。

## INSTITUTIONAL-LEARNING-005: 新规则必须压缩注意力税而非累积瘢痕

BIRTH 成立必须同时满足：机制超出单次经历且未来具备明确的可识别 trigger；能明确定义至少一个 negative 或 distinction 避免误诊；与现有规则库不重复；未来预防或复制该机制的价值足以抵扣长期的注意力税。不满足上述任一条件时必须降级为 ABSORB 或 DISCARD。

## INSTITUTIONAL-LEARNING-006: 正向成功机制与负向代价机制同等合法

`celebrate` 所代表的成功经验不得因「未发生事故」而被忽视；Enhancer 可将成功经验提炼为正面行为准则，或在符合规范的前提下表达为未来可检测的反向病理。严禁将所有经验强行扭曲为惩罚性禁令；若现有规则库表达形式无法诚实承载，应选择 DISCARD。

## INSTITUTIONAL-LEARNING-007: `celebrate` 在学习闭合后尾部弹出 Deferred Work

`celebrate` 调用的执行时序严格固定为：接收经验 → 提炼 disposition → 规则准入校验（仅 BIRTH）→ 冻结学习结果 → 由 `attention-regulation` 提取当前未弹出的 DeferredWork → 在工具返回值尾部统一呈现暂缓工作项。`regret` 调用不触发暂缓工作的弹出。`celebrate` 弹出的暂缓工作项仅供提示，严禁自动转为执行任务或产生新的阻塞性义务。

## INSTITUTIONAL-LEARNING-008: 学习失败诚实暴露与原子提交事务

若 Enhancer 无法提炼出合法结果或 BIRTH 准入失败，工具必须在返回值中明确告知经验未被制度化，严禁隐瞒失败。每个学习事件在持久化提交前先完成纯内存 staging；随后通过单笔原子提交写入 `LearningDispositionCommitted` 事实，并按需同批提交 `InstitutionalRuleBorn`（仅 BIRTH）与 `DeferredWorkResurfaced`（仅 celebrate 有暂缓项时）。若预检失败或版本冲突，整笔事务零提交。同一 `LearningOccurrenceId` 重放时仅幂等返回已冻结的结果，不再重复调用 Enhancer 或二次弹出暂缓项。
