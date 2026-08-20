# behavior-diagnosis — WHAT

## BD-001: live Rulebook 是唯一规则真相；built-in 与 institutional rule 共享同一身份空间

每条规则的唯一语义身份恒为 `TipName = RuleId = FieldName`。live Rulebook 仅由两种规范来源合成：shipped built-in 目录（basename 即 TipName）与经本包准入后进入统一 durable event substrate 的 `InstitutionalRuleBorn`。两者合流为单一 `EnforcerCatalog`，共享全局唯一的 TipName 命名空间、同一 Blogger system prompt 与 Main 处置手册索引。跨来源 TipName 冲突必须 fail closed。禁止设立 `catalog.json`、shadow learned catalog、运行时专用规则库或任何平行身份清单。

## BD-002: 规则装载与准入校验 fail-fast，零 fallback

启动时装载 built-in 规则失败（目录缺失、叶子缺失、文本为空、Domain 校验失败）必须立即进程级 fail-fast，不跳过、不警告降级、无代码内置 fallback 规则、无双副本备份。institutional candidate 在 durable append 前必须通过同一结构、身份与双语正文校验，非法 candidate 严禁写入事件。institutional 准入预检必须绑定精确的预期 `RulebookRevision`，原子提交前若 live revision 已漂移则判定为 `KnownNotCommitted`，整笔学习事务零提交并由上层重新评估。

## BD-003: live union Domain 校验合同

`EnforcerCatalog.validate` 对 built-in 与 institutional 的 live union 合流要求：`schemaVersion = 1`；至少存在一条规则；Name / RuleId / FieldName 各自唯一且三者两两恒等；LexicalOrder 连续 `1..N`；EnforcerText / MainText（及双语对应正文）经 trim 后非空。LexicalOrder 是合流集合的确定性派生序，非事件自带的可竞争序号。

## BD-004: 检测语料全量、确定性进入 Blogger system prompt

有效的 Blogger effective system prompt 由基础 Blogger prompt、`# Enforcer Rulebook` 标题、live Rulebook 全部 `## <TipName>` 及其对应当前语言的 EnforcerText 全文按 LexicalOrder 确定性拼接而成。同一 live union 集合合成完全一致的字节。合成结果为内存派生产物，严禁写回文件系统或作为第三份规则真源。

## BD-005: 多语言本地化叶子完整性合同

built-in 与 institutional rule 的本地化语言叶子遵循同一合同进入 live Rulebook：正文非空且 TipName / RuleId / FieldName 恒等。每条 institutional BIRTH 必须同时提供完整双语（English 与 zh-CN）的 EnforcerText 与 MainText；缺失任一语言则准入失败。投影层按 provider 语言选择对应正文，无跨语言 fallback。

## BD-006: chronicle 工具参数合同与 NoLiveCycle 协议结果

`chronicle` 工具调用中 `entry`（trim 后非空文本）与 `tip`（可归一到 TipName）为语义必需；缺少 tip、空 tip 或非 string tip 必须稳定返回错误面。若物理工具调用到达时不存在存活的 Blogger cycle，宿主层必须产生封闭的 `NoLiveCycle` 协议结果并终止过时 session，仅在最外层工具适配器编码为宿主异常。

## BD-007: tip 确定性最近映射与无未知分支

`tip` 参数查找前先执行 trim。精确命中已装载 rulebook 的 TipName 时直接映射；否则对全部 TipName 计算 Levenshtein 编辑距离并选取距离最小者。最小距离并列时选取 LexicalOrder 最小者。任何非空 string tip 都必须解析为一条确定规则，不存在 `UnknownTip` 或规则逃逸分支；RuleId = FieldName = TipName 恒成立。

## BD-008: 彻底剔除数值评分路径与桥接字段

解码与运行时表面不暴露任何 `Scores`、`parseScore` 或数值严重度字段；输入中的额外数值属性被安全忽略且不得复活评分路径；`ScoreWhen`、`Nudge`、`Family`、`CatalogOrdinal` 等桥接字段在装载后均不存在。诊断语义是离散的单条 tip 选定，而非数值积分向量。

## BD-009: 每个 provider run 必须且仅能调用一次 chronicle

一个 Blogger provider run 只有在原始 assistant step 中恰好包含一次 `chronicle` 调用时，方有资格形成有效 cycle。包含 0 次或 2 次及以上调用的情形均属协议违约，严禁内部合并、严禁提交 `BlogObservationCommitted`、严禁推进 coverage。terminal 状态前只等待宿主收敛工具调用，terminal 后统一进入有界协议修复。

## BD-010: Cycle provider-run 身份 fail-closed 门禁

通过单次调用基数校验后，Cycle 仍须提供可验证的 `ProviderRunIdentity`（非空 messageId）：缺少 messageId 立即触发 `enforcer-cycle-failed` 致命失败。缺少或非法 ToolCallId 导致无法形成规范调用时，按协议违约进入有界修复；身份证据不足的 provider run 严禁提交。

## BD-011: fail-closed 内容硬界约束

单 cycle 内容硬界约束（违背则 fail-closed 并报 `enforcer-cycle-failed`）：规范化日志文本不得超过 512 KiB UTF-8 字节；证据字段不得超过 128 KiB UTF-8 字节。硬界属于安全防线，严禁演化为业务层面的启发式评分参数。

## BD-012: BlogObservationCommitted 是唯一原子 cycle 事实

每次正常 cycle 的提交由唯一的 `BlogObservationCommitted` 事件原子承载：包含工作日志 frame 追加、RecordCoverage 推进、单一 TipRuleId/FieldName、provider/tool 身份及大文本 blob 引用。不存在独立的 `EnforcementCycleCommitted` 事实；监督状态由 BlogEntry 纯函数派生，保证日志与覆盖范围推进同生共死。

## BD-013: Coverage 严格单调推进出生门与提交前校验

Coverage 出生门：若待覆盖区间的 Next ≤ Prev 或 NextCursor 无法映射，必须拒绝生成 context chunk，严禁启动零推进的 Blogger 窗口。提交前执行持久化一致性预检：若 staged cursor/cutoff/epoch 与当前投影不一致，返回 `KnownNotCommitted` 并可恢复弃置，严禁先写事件再被 fold 拒绝。

## BD-014: 每 cycle 恰好派生一个 RecentTip，容量有界且重放幂等

每个已提交 cycle 恰好派生一个 RecentTip（包含 RuleId、FieldName 与 CycleId）。RecentTips 集合容量有界（最大 8 项），按 oldest → newest 严格排序。同一 ProviderRun 重复提交时必须幂等拒绝，不产生重复收据。

## BD-015: tip 与 frame 是不可拆分的配对观察视图

Observation 历史是诊断 tip 与 Blog frame 的不可拆心配对视图：前向 zip（tipᵢ ↔ frameᵢ），剩余侧 unpaired 追加；tip 锚定视图丢弃无 tip 的 frame，严禁发明假 tip，严禁维护平行独立的 tips 与 frames 数组作为权威历史。

## BD-016: 历史压缩协同裁剪，不创造新 occurrence

执行 squash 压缩（`BlogObservationsSquashed`）时，将最老 K 个 frame 折叠为一个 Squash frame，并同步按 1:1 比例共同裁剪最老 `min(K, tips)` 条 RecentTips。squash frame 本身不新增 tip、不触发新的 Main 交付，仅作为历史表示形式的变换，严禁借历史压缩伪造新的诊断事件。

## BD-017: 无效 cycle 的有界协议修复与 AABB 状态恢复

未形成有效 cycle 的 terminal（0 次调用、多调用、缺 tip、空 entry）进入有界协议修复流程：
1. 首发 Nudge 必须且仅能由处于完全静止的 idle terminal 发起，禁止在 transform 阶段发送。
2. 每个 `BloggerRequestId` 至多获得一次 Nudge 修复机会；同一 terminal run 重放保持幂等。
3. 纯文本 terminal 不得依赖后续 transform 唤醒，由 `SessionIdle` 触发专用 nudge。
4. Nudge 后的再次无效 terminal 记录 confirmed failure 并无条件执行首发 AABB；AABB 阶段严格保留 BloggerRequestId 与目标 terminal 身份。
5. 恢复重判仅从包含 `ToolName=chronicle` 的 completed 工具执行状态派生，禁止猜测或别名兼容。

## BD-018: RulebookRevision 按 Blogger life 冻结与生效边界

每个 Blogger participant life 在创建时绑定确定的 `RulebookRevision`，同时决定 system prompt 字节、`chronicle.tip` 枚举、解码映射表与 Main 处置索引。Blogger life 存活期间四者保持冻结，新规则的诞生不打断当前 in-flight cycle，仅在下一 cycle 创建 fresh life 时绑定最新 revision，确保 byte stability 与工具定义不分叉。
