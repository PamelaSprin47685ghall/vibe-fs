# behavior-diagnosis — WHAT（唯一 normative 合同）

> 命题 = 当前世界必须同时成立的事实。编号 `BEHAVIOR-DIAGNOSIS-NNN`（下文简称
> `BD-NNN`）。每条末尾的证据指针 → `HOW.md` 行号。
> 边界：诊断如何/何时展示给 Main、feedback dedupe/coverage 归 `guidance-delivery`；
> `chronicle` 工具名/权限归 `capability-enforcement`；score vector 是已弃权历史。

## A. 检测边界：规则实例 SSOT

### BD-001 目录即唯一规则真相

规则实例的唯一真相是 `resources/enforcer/<TipName>/` 目录：目录 basename =
TipName = provider tip enum 值 = durable RuleId = FieldName，四者恒等。不存在
`catalog.json`、manifest 或任何并行元数据清单作为装载输入或第二身份。

- 含义：一个名字一套真相（Rulebook §11）。目录集合同时决定 Blogger system 的规则
  集合、`tip` 枚举、Main guide 查找命名空间与校验清单。
- 边界：目录的物理格式/命名规则本身不归本包（是资源实现细节）；本包消费「目录 =
  身份」这个不变量。
- 证据：`catalog.test.mjs` `ENFORCER_170_*`；`HOW.md` 行 10。

### BD-002 装载 fail-fast，零 fallback

启动装载失败（目录缺失、叶子缺失、文本为空、Domain 校验失败）→ 进程 fail fast，
不 skip、不 warn-and-continue、无代码内 fallback catalog、无 dist 双副本。

- 含义：坏包必须当场暴露，不能静默成功（历史 why/enforcer 三连拒之一）。
- 证据：`catalog.test.mjs`、`catalog-validation.test.mjs`；打包路径由
  `requirements/behavior-diagnosis/tests/integration/resources/enforcer-rulebook.test.mjs`（REUSE）覆盖；`HOW.md` 行 11。

### BD-003 Domain 校验合同

`EnforcerCatalog.validate` 要求：`schemaVersion = 1`；至少一条规则；Name /
RuleId / FieldName 各自唯一且三者两两相等；LexicalOrder 连续 `1..N`；
EnforcerText / MainText（及装载派生字段）trim 后非空。N 不硬编码（当前仓库恰为
120，由测试锁定，不写进 Domain）。

- 含义：身份唯一 + 顺序连续 + 正文非空是检测语料可用的最低门槛。
- 证据：`catalog-validation.test.mjs` `ENFORCER_170_validate_*`（11 条）；`HOW.md` 行 12。

### BD-004 检测语料全量、确定性进入 Blogger system

有效 effective system = base Blogger prompt + `# Enforcer Rulebook` 头 + 全部
`## <TipName>` + 对应 enforcer.md 全文（按 LexicalOrder 拼接）。同一输入合成同一
字节；合成产物是 derived artifact，不写回仓库、不是第三份规则数据。

- 含义：Blogger 在开始判断前已看到全部检测规则，不需要 lookup/search 工具
  （Rulebook §8/§10）；检测语料不因检索机制而残缺。
- 证据：`rulebook-system-composition.test.mjs` `SYSTEM_001/002/004`；`HOW.md` 行 13。

### BD-005 本地化叶子同样完整

zh-CN 叶子（`enforcer.zh-CN.md` + `main.zh-CN.md`）按同一合同装载：120 条、非空、
TipName/RuleId/FieldName 恒等。装载按语言定位叶子，无跨语言 fallback。

- 含义：检测边界不因语言而塌缩；语言绑定是 `provider-language` 的领地，本包只
  保证每个语言世界都有完整检测语料。
- 证据：`rulebook-system-composition.test.mjs` `SYSTEM_003`；`HOW.md` 行 13–14。

## B. tip 身份与枚举（codec）

### BD-006 chronicle 参数合同

`chronicle` 调用按 codec 解析：`entry`（trim 后非空）与 `tip`（目录 TipName 枚举
精确命中）为语义必需；缺失 tip / 空 tip / 非 string tip → 失败，错误面稳定
（`missing required argument: tip`）；`entry` 缺失或空 → 无有效文本。

- 含义：诊断必须有「观察了什么 + 选中哪条规则」两个要素，缺一不可成立
  （ENFORCER-022/023/061）。
- 边界：`chronicle` 工具名与权限归 `capability-enforcement`；这里只锁参数语义。
- 证据：`codec.test.mjs` `ENFORCER_023_*`、`ENFORCER_022_*`；`HOW.md` 行 15–17。

### BD-007 tip 精确映射，无 fuzzy

`tip` 必须精确命中已装载 rulebook 的 TipName 枚举，映射到 RuleId 且
RuleId = FieldName = TipName；未知 tip / 拼写近似 / 词形变体一律失败，不做
fuzzy / Damerau–Levenshtein / 默认 tip 修复。查找前 trim。

- 含义：诊断不能在「最接近的规则」上成立（历史 why/enforcer 1.4）；
  未知输入不得被强行解释成某条工程规则。
- 证据：`codec.test.mjs` `ENFORCER_021_*`、`ENFORCER_024_fuzzy_or_misspelled_*`；
  `HOW.md` 行 15–17。

### BD-008 无 score path

decode 面无 `Scores` / `parseScore` / 数值严重度 surface；额外 numeric property
不得复活 score path；`ScoreWhen` / `Nudge` / `Family` / `CatalogOrdinal` bridge
字段在装载后不存在。

- 含义：诊断是「一条 tip」，不是评分向量（ENFORCER-024/072/073 clean break）。
- 证据：`codec.test.mjs` `ENFORCER_024_extra_numeric_properties_are_ignored`、
  `catalog.test.mjs` `ENFORCER_170_no_bridge_fields_on_rule`；`HOW.md` 行 15–17。

## C. Cycle 归并与有效性

### BD-009 每个 provider run 恰好一次 `chronicle`

一个 Blogger provider run 只有在 raw assistant step 中**恰好出现一次** `chronicle`
调用时才有资格形成 cycle。0 次或 2+ 次都属于协议违约，不得合并、不提交
`BlogObservationCommitted`、不推进 coverage。assistant 尚未 terminal 时只等待 Host 把 tool parts
收敛，禁止看到第二个 pending call 就抢先 nudge；terminal 后统一进入 BD-017 的
InteractionRepair → AABB 有界修复。

- 含义：`chronicle` 是一次观察的原子提交口，不是可 map/reduce 的批量接口。多调用若被防御性
  merge，会把「模型没遵守 exactly-once 协议」伪装成成功，并让后续 nudge/AABB 时序失去唯一失败点。
- 证据：`enforcer-cycle-protocol.test.mjs` `ENFORCER_042_multi_call_*`；`HOW.md` 行 18。

### BD-010 Cycle 身份 fail-closed

通过 BD-009 cardinality gate 后，Cycle 仍要求可证明的 ProviderRunIdentity（非空 messageId）：
空/缺失 messageId → `enforcer-cycle-failed` fatal。缺失/非法 ToolCallId 使该唯一 raw invocation 无法形成
canonical call，按 BD-017 作为无效 cycle 修复；身份不足的 provider run 不得进入提交。多调用已由
BD-009 在 commit 前转入协议修复，不再以 deterministic merge 兜底。

- 含义：cardinality violation 是可修复的模型协议失败；身份缺失是无法证明事实归属的存储边界失败，
  两者不得混成同一种 fatal/merge 行为。
- 证据：`identity-fail-closed.test.mjs` `ENFORCER_043_*`；
  `enforcer-cycle-protocol.test.mjs` `ENFORCER_042_multi_call_*`；`HOW.md` 行 19。

### BD-011 fail-closed 硬界

通过 exact-one cardinality gate 的单 cycle 内容硬界（fail closed，`enforcer-cycle-failed` fatal）：
canonical text > 512 KiB UTF-8；evidence > 128 KiB UTF-8。tool call 数不再有第二个“≤32 可 merge”
区间：任何 2+ 已由 BD-009 进入有界协议修复。这些是实现安全界，不重新演化成业务
score/severity 参数。

- 含义：防拒绝服务与不可控提交（ENFORCER-042 §13.2）；界是硬墙不是启发式。
- 证据：`bounds.test.mjs`（4 条，驱动真实 `handleContinuation`）；`HOW.md` 行 20。

## D. 原子 occurrence

### BD-012 `BlogObservationCommitted` 是唯一原子 cycle 事实

一次 normal cycle 提交 = 一个 `BlogObservationCommitted` 事实，原子携带：frame
append + RecordCoverage advance + 单一 TipRuleId/FieldName + provider/tool 身份 +
blob 引用。不存在独立 `EnforcementCycleCommitted` 事实；Enforcement 半边由
BlogEntry 派生。

- 含义：诊断 occurrence 与工作日志、覆盖推进同生共死（ENFORCER-045）；「frame 有
  coverage 没动」或其反面都不可能出现。
- 证据：MOVE `observation-projection.test.mjs` `OBS_PROJ_002`；REUSE
  `requirements/behavior-diagnosis/tests/blogger-cycle-atomic-fact.test.mjs`
  `C0_no_EnforcementCycleCommitted_fact`、`enforcer-cycle-protocol.test.mjs`
  `ENFORCER_host_completed_blog_with_live_request_commits_and_advances_coverage`；
  `HOW.md` 行 21。

### BD-013 Coverage 严格推进门

coverage 出生门：Next ≤ Prev 或 NextCursor 不可映射 → 拒生（`mainContextFromChunk`
返回 None），绝不启动一个零推进的 Blogger 窗口；未知突破仍在 commit 路径
`Diagnostic.fatal`（已知拒生，未知仍杀）。commit 前 PERSIST-010 precheck：staged
cursor/cutoff/epoch 与投影不一致 → `KnownNotCommitted`（可恢复弃置），绝不先写
事实再被 fold 拒绝。

- 含义：诊断只建立在覆盖推进的事实上；因果前置不能靠重新从 XTrace head 推导
  （ENFORCER-045/154）。
- 证据：`coverage-birth-gate.test.mjs` `ENFORCER_045_*`；REUSE
  `enforcer-cycle-commit-branches.test.mjs` `ENFORCER_precheck_*`；`HOW.md` 行 22。

### BD-014 每 cycle 恰好一个 tip occurrence

每个已提交 cycle 恰好派生一个 RecentTip（RuleId + FieldName + CycleId）；RecentTips
有界 8、oldest → newest；同一 ProviderRun 重复提交被拒绝（replay 幂等，不产生
第二条 receipt）。

- 含义：occurrence 有独立身份、有界可回溯（ENFORCER-070/154）；重放不重数。
- 证据：REUSE `requirements/behavior-diagnosis/tests/tip-v2-contract.test.mjs`
  `ENFORCER_TIP_08/09/10/11`；`HOW.md` 行 23。

## E. Observation 配对

### BD-015 tip 与 frame 是同一个不可拆观察

Observation 历史 = tip 与 Blog frame 的**配对**视图（domain `ObservationUnit` /
`WorkLogObservation`）：前向 zip（tipᵢ ↔ frameᵢ），剩余侧 unpaired 追加；
tip-anchored 视图丢弃无 tip 的 frame（不发明 tip）。禁止 tips∥frames 两路平行流
当权威历史。

- 含义：Blogger 回看历史时能直接看到「当时我看到这些事实 → 所以我选了这条 tip」，
  不需要把两个数组重新 join（Rulebook §2）。
- 证据：MOVE `observation-pair.test.mjs` `RULEBOOK_OBS_001..008`、
  `observation-projection.test.mjs` `OBS_PROJ_001/002/004`；`HOW.md` 行 24。

### BD-016 历史压缩不创造新 occurrence

squash（`BlogObservationsSquashed`）把最老 K 个 frame 折叠为一个 Squash frame，
同时按 1:1 co-truncate 最老 `min(count, tips)` 条 RecentTips；squash frame 本身不
新增 tip、不触发新的 Main 交付。压缩是历史表示变换（K→1、保留代表 TipIdentity），
不是新观察、不是新 violation 发现。

- 含义：记忆重写不得伪造新的世界教训（Rulebook §5/§6；ENFORCER-070）。
- 边界：squash 的压缩调度/触发语义归 `context-compression`；本包只锁「不造新
  occurrence」这一半。
- 证据：MOVE `observation-projection.test.mjs` `OBS_PROJ_003`；REUSE
  `requirements/behavior-diagnosis/tests/tip-v2-contract.test.mjs` `ENFORCER_TIP_12`、
  `paired-history-eval.test.mjs` `A42_PAIRED_HISTORY_*`；`HOW.md` 行 25。

## F. 无效 cycle 的协议修复（cycle 生命周期一部分）

### BD-017 无有效 cycle → 有界协议修复，不另造预算

无有效 cycle（`chronicle` 0 次 / 2+ 次、纯散文、缺 tip、空 entry）最终可进入
InteractionRepair/nudge 路径，但**第一次物理 Nudge 只能由真正 quiescent 的 idle terminal 拥有，transform
不得发送 session nudge**。transform 正处在 Host provider/tool-loop 内，在那里发送 nudge 会与 Host 的自然
continuation 竞争并形成 queued user message。尤其“恰好一次 chronicle，但参数/schema/tool execution 因错误
tip/hint 失败”仍属于 Host tool-loop：先把 tool error 原样交回 Blogger，让它在下一 provider step 自己改正；
这一失败本身不 claim repair、不记 confirmed failure、不消费 AABB。只有后续真正无 tool-loop 可继续的
invalid terminal 才由 idle 立即发送 repair nudge。每个 exact `BloggerRequestId` 至多一次 Nudge；同一 terminal run 重放幂等（同一观察
重放，不发送、不推进）；新 terminal 再次无效才证明 nudge repair 失败 → 统一 Fallback/AABB；abort 清理
残留只注入一次 repair、不推进主 cursor、不消耗 AABB 预算。**0-call/pure-prose terminal 不得依赖
“下一次 provider transform”才能开始修复**：它没有 tool loop，自然也没有下一次 transform；Host
`SessionIdle` 的 reconciled turn 是该失败后的可靠唤醒点，必须直接驱动 Blogger 专用 nudge，而不是
ordinary `MissingClosingReport`（后者会错误要求 Blogger 继续输出自然语言）。若 nudge 后新的 0-call
terminal 再次 idle，则该 idle 机会记录一次通用 Fallback confirmed failure，并**无条件保留本 BloggerRequest 已赢得的一次首发 AABB 发送权**：即使该记账恰好使通用 provider fallback cursor 达到 exhaustion，也必须先实际发送这一发 AABB，不能在 AABB 尚未进入 Host 前打印 `blogger protocol repair exhausted`。AABB 阶段必须保留**BloggerRequestId + 它所针对的 terminal ProviderRunIdentity**：同一 terminal 的 transform/idle 重放幂等，旧 BloggerRequest 的 nudge/AABB claim 对新 request 完全不可见；首发 AABB 之后，每个**新的**无效 terminal 都是新的 confirmed failure：fallback projection 尚可继续则获得下一次 request-scoped AABB occasion，只有 projection 已真实 `FallbackExhausted` 时才允许 fatal。换言之，`AabbRepairIssued` 只证明“这一发已发送”，绝不等价于“整个 AABB budget 已耗尽”。durable `blogger-aabb` InteractionRepair claim 与 provider-visible synthetic repair evidence 都必须恢复该 request+terminal identity，而不是退化成 LogicalRun 级 `AABB consumed` 布尔；仅有历史 ClaimSequence 不证明 AABB 已执行，已 `Abandoned` 的 dispatch 不得恢复成 `AabbRepairIssued`。没有 exact live `BloggerRequest` 的历史 idle 也不得消费 protocol budget。恢复重判必须从 durable claim lifecycle + provider-visible `SessionToolPart` 证据派生，且
只有 terminal 中**恰好一个 completed `chronicle`** 才能证明 repair 后协议恢复；不得从丢失 tool name
的 `MessagePart.ToolResult` 猜测，也不得保留旧工具名 `blog` alias。`BloggerToolRecovery` 不退化成整数计数器。

- 含义：诊断机制自己不变成第二控制循环；修复机会有界（ENFORCER-060/061/062/063/
  065/066/067/068/153）。
- 边界：FallbackController 本身归 `provider-attempt-recovery`/`crash-reconciliation`；
  本包只锁「无效 cycle 的修复入口与有界性」。
- 证据：REUSE `requirements/behavior-diagnosis/tests/enforcer-cycle-protocol.test.mjs`
  `ENFORCER_060_*`、`ENFORCER_061_*`、`ENFORCER_068_*`、`LOOP_006_*`；
  `HOW.md` 行 26。
