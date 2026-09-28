# institutional-learning — WHAT

## [001] 原始经验

`celebrate(experience)` 与 `regret(experience)` 分别接收一段非空自然语言成功经验与代价经验，允许主观、局部和口语化，不要求规则模板、故障分类或 TipName。成功不折算为评分，后悔不等于已确认违规。

## [002] 唯一结论与有界评估

每个成功完成的 `LearningOccurrenceId` 恰好提交一个结论。每次评估只调用一次私有 Enhancer，结论只能是：

- `ABSORB`：既有规则已涵盖该机制，不变更规则；
- `BIRTH`：发现全新、可复用且未来可识别的机制，请求创建规则；
- `DISCARD`：局部偶然、个人偏好、单次事实或不能泛化，不入库。

不设第四种结论、评分阈值或递归增强。BIRTH 提交前若预期 `RulebookRevision` 已变化，可基于最新 live Rulebook 重评一次；再次冲突则明确失败，不提交中间状态。

## [003] 机制提炼与输入边界

Enhancer 从经验中提炼超出单次经历的机制，输入仅为该经验与当前 canonical live Rulebook。不得另行开展网络或仓库调查，也不得把未经抽象的命令流水、文件路径或时间戳升格为永久规则。

## [004] 规则准入

BIRTH candidate 必须包含唯一 TipName、中英双语 EnforcerText 与 MainText，并通过 `behavior-diagnosis` 的准入后才能持久化、生效。ABSORB、DISCARD 不变更规则，不得另设运行时规则库。

## [005] 新规则的价值

BIRTH 必须同时具备可泛化的机制、未来可识别的 trigger、至少一个防误诊的 negative 或 distinction，且不与既有规则重复，长期价值足以抵扣注意力成本。否则选择 ABSORB 或 DISCARD。

## [006] 成功与代价同等对待

不得因未发生事故而忽视成功机制。成功可形成正面准则或可检测的反向病理，不得一律改写成惩罚性禁令；规则库无法如实表达时选择 DISCARD。

## [007] 学习闭合后的暂缓工作

`celebrate` 按接收经验、提炼结论、BIRTH 准入、冻结学习结果的顺序完成学习，再由 `attention-regulation` 提取尚未弹出的 DeferredWork，统一放在返回值尾部。`regret` 不弹出。弹出项只作提示，不自动执行或形成阻塞义务。

## [008] 原子提交与重放

Enhancer 无合法结论或 BIRTH 准入失败时，返回值须明确说明经验未制度化。提交前先完成内存 staging，再原子提交 `LearningDispositionCommitted`，以及必要的 `InstitutionalRuleBorn`（BIRTH）与 `DeferredWorkResurfaced`（celebrate 有暂缓项）；预检失败或版本冲突时零提交。同一 `LearningOccurrenceId` 重放只返回冻结结果，不重评、不再次弹出暂缓项。
