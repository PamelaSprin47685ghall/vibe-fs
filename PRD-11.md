# 实施顺序（Flow-first）

## 一、原则

不要七个问题同时大改。每个 batch 内多文件可并行实施，batch 间有依赖。纯静态分析优先验证，编译测试需要 60s 尽量减少无谓测试。

三句取舍原则贯穿全程：
* 宁可少自动继续一次，也不能违反用户 Esc；
* 宁可少发一次 nudge，也不能在 compaction/fallback 后重复抢控制权；
* 宁可因状态缺失阻止 review continuation，也不能让 LLM 在没有原始任务的情况下猜。

## 二、P0：立即阻止错误行为

1. 将唯一生产输入管线改为 `scanCommit`：`step(snapshot)` 先生成 candidate
   State/Events/Effects，单次 append 成功后才发布 `SessionState`/accumulator/effects；
   journal failure 使 session poisoned，并阻断 synthetic prompt。
2. 固定 workspace journal 拓扑：一个 journal path、一个进程内 `JournalWriter`，并以
   interprocess lock 保护 append；投影不得各自写日志。
3. 先引入 `SessionEpoch` 与 `CausalityContext`（包括 session-start 无 owner observation
   options），再引入 `ContinuationLease` 原子 claim：在任何物理 dispatch 前写入
   `DispatchClaimed`，并实现 `Dispatching`、`Dispatched`、`Running`、`Settled` 与
   `DispatchUnknown`/`Reconciling`。迟到 result 必须 stale，不能复活旧 lease。
4. 修复所有 Cancelled gate（`needFallbackContinue`、`terminalObservation`、
   `isSubagentSettledFromObservation`）——第一分支统一 Cancelled/TaskComplete → 终止。
5. 区分一次 Esc 与实际 Abort（OpenCode TUI 双击 Esc 语义）。
6. 引入 per-session event mailbox（取代散落 async handler 直接读写 runtime map）。
7. `session.idle` 不再直接触发 nudge；已 Settled 的 fallback lease 不得再产生 fallback
   nudge。
8. fallback action 发送前增加 lease 校验（cancelGeneration + humanTurnID + lifecycle
   + canonical owner + compaction + TaskComplete）。
9. 接入 compaction Hooks（`experimental.session.compacting`、
   `experimental.compaction.autocontinue`、`session.compacted`），并规定 compaction
   仅递增 contextGeneration，不递增 sessionGeneration。
10. compaction episode 活跃时阻止 nudge；对话开始未取得足够上下文时不得生成 emergency
    todo prompt。
11. Review nudge 携带 `original_task`（不是 `task`），Nudge 显式传
    model/variant/agent。
12. investigator agent 必须注入 CAPS；单工具成功回合只发一个“并行调用工具”提示。
13. Hook 参数改为原地删除，不替换 `output.args`；after hook 移除安全校验职责。
14. `warn`、`warn_tdd`、`warn_reuse` 及 todo 长度遵循软合规：schema 强调但不硬拒绝，
    在工具返回时以一次 stern compliance event 批评模型；仅 malformed business args、
    security/permission denial、parse failure 和控制字段泄漏是 hard gate。
15. 删除 stdout DEBUG。

## 三、P1：消除主要竞态

1. HumanTurnProjection。
2. CancellationProjection。
4. CompactionProjection。
5. ContextBudgetProjection（RebaseRequired，revision/counter 语义）。
6. Host model observation generation。
7. Event ID 幂等与 stale-result immunity。
8. plugin load order 检查。
9. MCP schema 能力审计。
10. prompt provenance 与官方 messageID 绑定。

## 四、P2：消除兼容性启发式

1. 删除零宽字符身份判断。
2. 删除物理时间戳权威判断。
3. 删除 stale injected model 优先级。
4. 删除 compaction 文案判断。
5. 删除最后 assistant 推导 human model。
6. 删除全历史 todo 作为 R。
7. 删除固定大 context limit。
8. 删除 event handler 中直接 prompt。
9. 删除依赖 after hook 的安全策略。

## 五、阶段化实施建议

### 阶段 0：先固定复现和观测

建立端到端测试事件脚本，重放 Esc 与 fallback 竞态、compaction 后 idle、model A/B/C 切换、context threshold、review nudge、warn 字段执行。同时记录结构化事件，不再增加临时 DEBUG。

### 阶段 1：引入 provenance、turn ID、generation，但不引入第二事实源

新增 humanTurnId、continuationId、cancelGeneration、prompt origin、routing context。
所有变更通过一条 domain event 写入 journal，并由 shadow projections 在内存中比较
旧行为；禁止第二次事实写入、禁止 parallel state stores、禁止 `currentProjections`
成为 SSOT。

### 阶段 2：优先修复 Esc 和 nudge owner

最高风险问题。完成 sticky cancellation、invalidation、terminal origin、continuation owner、fallback settle、compaction gate。

此时应先保证"不会擅自继续"，哪怕偶尔少 nudge，也比 Esc 后继续安全。

执行计划必须留在 `step` 的单一 snapshot 事务内；`stateFlow |> map plan` 只用于
诊断/UI 预览。lease claim、dispatch、settle 均只能由已提交事件驱动。

### 阶段 3：修复模型和 review task

切换 nudge 消费者：model 从 HumanTurnProjection 取；task 从 ReviewProjection 取；禁止读取 stale injected model；禁止从旧 assistant 推导 review context。这部分变更相对独立，容易验证。

### 阶段 4：统一控制字段执行网关

先完成能力分类、最终 schema decorator 和启动完整性检查，再切换执行入口。迁移期间可以保留 host-specific hook 做审计，但不能继续作为权威判断。

### 阶段 5：重建 context budget

数学、测量、缓存、compaction 耦合最深，应在事件 owner 和 provenance 稳定后改。缓存
只按 generation、revision/counter、已应用事件数和 canonical byte/prefix equality
断言判断；不得以内容 hash 作为 key 或正确性。否则 context-budget nudge 也会成为
新的抢跑来源。

### 阶段 6：清理旧启发式和 DEBUG

删除或降级 zws 文本识别、timestamp 权威判断、`last non-synthetic assistant` 回退、session 级 stale injected model、各 host 重复 warn 判断、直接 `printfn DEBUG`、post-execute 安全校验。
