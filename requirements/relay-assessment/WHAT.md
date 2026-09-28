# relay-assessment — WHAT

## [001] 八维评级

`review` 必填 `language_algorithms`、`simplicity`、`structure`、`granularity`、`tests_evidence`、`logic_reliability_boundaries`、`caller_ergonomics`、`completeness`，值仅为 `PERFECT | REVISE | N/A`。不得缺项、增加未知字段、使用数值或总评分。可选 `note` 仅为不返回、不参与裁决的字符串。

## [002] 同任一次评估

每个 IncumbencyId 至多一个 semantic AssessmentId。身份、binding、snapshot、authority、scores 全部相同的重放幂等返回原结果；同任第二次不同评估返回 AssessmentAlreadySubmitted，同 ToolCallId 异载荷返回 AssessmentReplayConflict。不得覆盖首次结果或跨任期重放。

## [003] 精确证据绑定

Accepted assessment 绑定 Road、Incumbency、WorkspaceSnapshot 与 AuthorityRevision，以及 PhysicalUserMessage、ProviderRun、ToolCall 和同一 assistant message 中调用前公开评审文本的摘要。隐藏 reasoning 与调用后文本不属于评审证据。

## [004] REVISE 即修复义务

任一 REVISE 即不通过；每个 REVISE 维度直接定义一项修复义务，PERFECT/N/A 不产生义务。义务从唯一 ScoreVector 查询，不另存第二份状态。AssessmentCommitted 同时确立 WorkOwned；后续账目推进属于执行行为。

## [005] 证书与降权

无 REVISE 时生成证书，精确绑定 assessment、snapshot、authority、root request digest、requirement set digest、narrative digest、evidence frontier 和 target horizon。本任立即失去工作区修改能力，仅可读、清理和 suicide。Accepted 退休后，证书有效期间禁止新任；显式失效后由普通新任重新独立评估。

## [006] 禁止同任自证

Assessment 一旦 accepted，本任永久失去 review 能力。修复质量由下一任独立评估，不得通过第二 review、reverify 或 challenge 自证。

## [007] 拒绝不占名额

Schema、范围、narrative 或精确 binding 校验失败不得写入 assessment 或消耗本任名额。提交取当前最新工作区快照；同 idempotency key 的异载荷须拒绝。

## [008] 信息时域隔离

评审前只提供当前独立只读评估要求，不泄露低分接责、满分退场或后继循环。评审后恰好选择一条指令：含 REVISE 为修复，无 REVISE 为关闭退场。`manager-assess`/review 描述与 `manager-work`/`manager-finish` 的可见时机必须遵循此边界，循环机制不进入 provider 文本。

## [009] 独立调查

Manager 可亲自使用评审专用只读工具，也可委派只读 Engineer 建立事实；评审期不得修改评估对象。评分由当前评估者基于当前快照独立作出，实现者或 DevOps 的报告只作待核查线索，不代替调查或决定评分。

## [010] 改动使旧证据过期

DevOps 验证可包含其角色授权内的非架构源码修复，不得假定其只读。快照一旦改变，旧测试结果、评估和证书即不能证明新快照；须由下一独立任期在最新快照重新评估。
