# Host — 证明

行为：`what/host.md`。边界：`shape/host.md`。程序：`how/host.md`。

## 事件

| 证明 | 条款 |
|------|------|
| 业务层无碎片事件处理 | HOST-001、HOST-002、ARCH-002 |
| Domain 仅 typed HostSignal | HOST-003 |
| chat.message 不拼装 terminal；外部用户消息只 signal 当前 JoinAttempt，零 future wake | HOST-002、EXEC-017 |
| `MessageAbortedError` / `AbortError` → typed `AttemptAborted` → revoke → `AbortWake`；零 ProviderFailure、零 repair/裸 `#` | `tests/unit/codec/signals.test.mjs`、`tests/unit/domain/reconcile-program.test.mjs`、`tests/unit/host/session-quiescence-gate.test.mjs`、`tests/e2e/cases/temporal-ownership-unhappy-path.test.mjs`（HOST-002、HOST-004） |
| provider `TurnAborted` 保留到消费边界；无 Armed 不产生 Agent completion | HOST-004、LOOP-006、EXEC-020 |
| Reconciler 无新信号时不产生 setTimeout/GetMessages（仅 ≤3 次因果重读） | HOST-004、A 类有界 |
| 重复 snapshot 稳定只证明观测稳定；`TurnUnknown` 为私有 `SnapshotObservation`（非 `TurnOutcome`）；无 `IdleWake` 不产生 idle-derived continuation | HOST-004 |
| 发送瞬间 fresh permit：stale permit → zero physical prompt + zero `PluginPromptClaimed`；`TryConsume` 与 dispatcher send 间无 await | HOST-004 |

## Compaction

| 证明 | 期望 | 条款 |
|------|------|------|
| 预防四项关闭 + 首轮探测 | 失败 → HostContractUnsupported | HOST-006 |
| 任意 pseudo-run | ContextReanchored；PrefixCoverage 归零；RecordCoverage 保留 | HOST-006、PERSIST-010 |

## 绑定与身份

| 证明 | 条款 |
|------|------|
| Transform 绑定 0/≥2 → 不写 seal | HOST-010 |
| journal 代理等式 canary | HOST-010 |
| Tool 身份仅 ToolContext 双半边 | HOST-011 |
| 跨实例共享表 vs 每实例 Journal；共享表操作不跨 await | HOST-012 |
| 永久 auto-injected pair | 用户 canonical multi-tool 序列逐字成立（`Req1 Req2 FakeReq1 Resp1 Resp2 FakeResp1` → `… Req3 FakeReq2 Resp3 FakeResp2`）；历史 pair 不随 current placement 搬家；same placement 重入不新增 pair（journal append 数 = 1、wire bytes exactly equal）；restart replay byte-identical；anchor 不在当前真实 view 时 pair 不重放（不重定位）；XWire DropLeading continue 不 AbortSession；N 轮 property：同 epoch `isAppendOnlyPrefix(wire[n], wire[n+1])` 恒 true | HOST-013 |
| Companion / Blogger 不注入 auto-injected | durable `isCompanion=true` 的 session transform 后 `markerCount=0`、消息字节与注入前相等；不为该 session append `PairProgrammingGuidelineAnchored` | HOST-013 |
| `WANXIANGSHU_SKIP_AUTO_INJECTED=1` 或 provider=`cursor` | 空 transcript / cursor transcript 不追加 pair；已有 durable history 仍 replay、新 placement 不 append | HOST-013 |
| 空 Content 预防 | assistant/user 消息空 content 兜底；reasoning 填充或非空 text | HOST-016 |

代表：`tests/unit/host/pair-thought-transform.test.mjs`、`tests/integration/plugin/manager-tool-contract.test.mjs`（`HOST_013_*`）、`tests/unit/enforcer/latest-tip-nudge.test.mjs`、HOST-013 replay property / restart / fail-closed 单测、Quiescence gate 单测（`tests/unit/host/`）。

## Session 关联

Work↔Companion 深度 1；关联非 Role（HOST-008、COMPANION-001/002）。

代表：`tests/unit/plugin/host-hooks.test.mjs`、host-compaction unit、e2e compaction/reanchor 路径。

## Satellite 与 Student/Teacher

| 证明 | 期望 | 条款 |
|------|------|------|
| kind 投影 | Work/Companion/Teacher 双向 O(1)；Satellite 无子 Satellite | HOST-008 |
| Host children 恢复 | journal id 匹配复用、id 丢失 Replacement、无关联新建不收养、冲突/查询失败 fail closed；物理 parent 恒为 family root | HOST-008、HOST-014、HOST-015 |
| Teacher 三轮调用 | 同一 Teacher SessionId；普通正文不完成父工具 | HOST-014、AGENT-020 |
| Teacher return | 文本只成为父 `teacher` 结果；固定 terminal 正常完成，无 abort/interrupted，Session 可继续 | HOST-014 |
| Student final return | QA 删除先于最终 Assistant completion；message 成为最终回复 | HOST-014、EXEC-027 |
| 非 Student 回归 | provider schema、hooks、Companion 行为字节/语义不变 | HOST-014 |
