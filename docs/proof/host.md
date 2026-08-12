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
| `WANXIANGSHU_SKIP_AUTO_INJECTED=1` | 不追加新 occurrence；已有 durable history 仍按当前 provider renderer replay | HOST-013 |
| Cursor Pair Hint | Cursor 新 placement 仍 append同一 durable occurrence；provider wire 无 `auto-injected` fake tool，只有 `ResultGap` 的一条 stable-id assistant text；普通→Cursor→普通 replay 可逆；Assistant/User/System controlled encoder 三者正文 byte-identical，strict-validator/authority canary 后生产固定 Assistant | HOST-013、PROMPT-018 |
| Pair parallel-wave Hint | canonical MarkerText 同时含 bounded current-wave、独立 read-only 默认并行、真实依赖才分 wave、不得猜参数/制造调用；三 renderer 同字节 | HOST-013 |
| NEEDHELP reasoning sensor | fragmented reasoning delta 精确命中；visible text/tool output 不命中；同 ProviderRun exactly-once arm；control sentinel exact-strip 后不进 XTrace evidence → `tests/unit/host/needhelp-sensor.test.mjs` | HOST-027、AGENT-031、PROMPT-018 |
| NEEDHELP reconcile / binding | fast→deep 同 Session 且 fallback 不动；请求 binding 从 exact Host `SessionMessage.Agent` 取；fast-root 下 later deep request 正确转 consultation；deep→单个 `deep-inquiry` child，parent/child canonical LWR、duplicate/recovery exactly-once、owner cancel no resurrection、finite guard → `tests/unit/host/assistance-host.test.mjs` | HOST-027、AGENT-031、PROMPT-018 |
| 空 Content 预防 | assistant/user 消息空 content 兜底；reasoning 填充或非空 text | HOST-016 |
| G2 PREFIX LAW ModelId bind：`ChatParamsHook` 留 `Model=None`；Inspector Q1–Q3 `SendPrompt` 必须带同一 `OpencodeModel`（`SyncDelegateRuntime` optional `promptModel`；G6 不得当己有字段删除）。**G2 Product Exit DONE** | HOST-008；PREFIX LAW 权威：`why/host.md` §13 / HOST-013 `ProviderProjection.isAppendOnlyPrefix` → tests/unit/session/g2-inspector-provider-wire-prefix.test.mjs（`G2_inspector_Q1_Q2_Q3_provider_wire_append_only_prefix`）；Host：tests/e2e/support/long-stroke-oracles.mjs `assertG2InspectorPrefixLaw`（same model；经 `tests/e2e/entry.test.mjs`）。2026-08-12 Amendment：mock LLM + 本机 OpenCode = 正确 Host |

## SessionProviderLanguage 证明（HOST-026；EN/zh-CN）

| 证明 | 期望 | 条款 |
|------|------|------|
| 创建绑定 | session 创建瞬间读全局偏好 → 不可变 `SessionProviderLanguage`；禁 ambient 再读 | HOST-026、PROMPT-017 |
| child 继承 | Companion / SyncDelegate / Bookkeeper / StrengthReplica / Attached 继承 owner\|commissioner；不得各自绑全局 | HOST-026 |
| 事件不改语 | Fallback / Strength / restart / reanchor / BlindPlan T1 **不**改写已绑语言 | HOST-026、ARCH-016 D |
| 全局后改 | 只影响此后新建 session；已开 Life Opening / Library / consequence / HOST-013 marker 字节连续 | HOST-026 |
| 翻译边界 | localizable = system / Role Law / Common Law / Library / tool prose / WorkRecord headings；invariant = tool 名 / argument / wire / enum / path / command / `exit_code` | HOST-026、ARCH-016 C |
| EN/ZH 资源 | 每份 provider semantic resource EN + zh-CN 成对；缺语言 fail（Gate C） | HOST-026、ARCH-016 C |

Gate C 跨域门禁见 `proof/architecture.md`；Prompt 侧冻结见 `proof/prompt.md` Gate D / PROMPT-017。

代表：`tests/unit/host/pair-thought-transform.test.mjs`、`tests/integration/plugin/manager-tool-contract.test.mjs`（`HOST_013_*`）、`tests/unit/enforcer/latest-tip-nudge.test.mjs`、HOST-013 replay property / restart / fail-closed 单测、Quiescence gate 单测（`tests/unit/host/`）。

## Session 关联

Work↔Companion 深度逻辑 1（InternalLeaf）；Dedicated SyncInspector/SyncCoder = Work+Attached 且 MAY
再挂 Companion；关联非 Role（HOST-008、COMPANION-001/002）。物理 parent 恒 family root（HOST-015）。

代表：`tests/unit/plugin/host-hooks.test.mjs`、host-compaction unit、e2e compaction/reanchor 路径。

## Attached 所有权

G2 PREFIX LAW / `promptModel` 证据见下表与上节「绑定与身份」。**G2 Product Exit DONE**（2026-08-12 Amendment：mock LLM + 本机 OpenCode = 正确 Host）。

| 证明 | 期望 | 条款 |
|------|------|------|
| 正交投影 | ExecutionClass×Ownership 可 O(1) 分辨 Work/InternalLeaf 与 Root/Attached；AttachmentKind 含 Companion/SyncInspector/SyncCoder/Bookkeeper/StrengthReplica | HOST-008 |
| Sync* 分类 | Dedicated SyncInspector/SyncCoder = Work+Attached，可走 Companion 能力路径；不得实现成 InternalLeaf | HOST-008 |
| InternalLeaf | Companion / Bookkeeper / StrengthReplica = InternalLeaf+Attached；不持有 Companion、不递归挂叶 | HOST-008、STRENGTH-004/014 |
| StrengthReplica 分类 | owner 最多一个 active StrengthReplica；非 SatelliteKind；retire 后不跨 decision 复用 | HOST-008、STRENGTH-004/014 |
| Host children 恢复 | journal id 匹配复用、id 丢失 Replacement、无关联新建不收养、冲突/查询失败 fail closed；物理 parent 恒为 family root | HOST-008、HOST-015 |
| G3 absence | `scripts/checks/student-teacher-absence.mjs` 与 capability/store ratchet 证明生产 Role、Agent、request kind、tool、QA storage、Satellite kind 均无 Student/Teacher 复活 | HOST-014、AGENT-020、PROMPT-012 |
| SyncDelegate 非兼容壳 | Inspector/Coder 使用 Work+Attached 与 Returned→Completion；不存在 Student/Teacher fallthrough、alias 或 legacy recovery | HOST-008、EXEC-026/028、HOST-014 |
| G2 Inspector PREFIX LAW | Q1→Q2→Q3 same SessionId + same model + `ProviderProjection.isAppendOnlyPrefix` / `wireOf`/`sealHolds`；unit + 唯一 Long Stroke（mock LLM + 本机 OpenCode）。**G2 Product Exit DONE** | HOST-008、HOST-013 → tests/unit/session/g2-inspector-provider-wire-prefix.test.mjs（`G2_inspector_Q1_Q2_Q3_provider_wire_append_only_prefix`）；Host：tests/e2e/support/long-stroke-oracles.mjs `assertG2InspectorPrefixLaw`（经 `tests/e2e/entry.test.mjs`） |
| Strength leaf 隔离 | StrengthReplica 只走 InternalLeaf+Attached，Session deletion / attempt abort 级联取消；不借 Student/Teacher 身份或 Satellite kind | HOST-008、STRENGTH-004/011/014 |

## Magic Todo V1 membrane canaries（Phase 0 blocking）

行为：`what/host.md` HOST-017..025。语义交叉：`what/todo.md` TODO-002/003/004/005/006/007/008/009/011/012/013。  
**任一 blocking canary 未证明 → 禁止写 production membrane；禁止改 Host core 绕过。**

| ID | 证明 | 期望 | 条款 | 级别 |
|----|------|------|------|------|
| A | before 原地 mutation 达 executor | durable pre-before `ToolPart.input` **仍为** provider V2 原字节；executor 见 V1 compatibility list | HOST-019 | **blocking** |
| B | 同时替换 parameters + jsonSchema | provider 见 V2；原 executor 仍跑 V1 decoder | HOST-018 | blocking |
| C | before 剥 kind/id | 原 decoder 接受 unknown 扩展字段剥离后的 V1 list | HOST-020 | blocking |
| D | `status="reviewing"` 经 TodoTable → todo.updated → API → TUI | 全容忍 → passthrough；否则冻结 sink→`in_progress` | HOST-023、TODO-003 | blocking（策略冻结） |
| E | after 改写 `output.output` | 本次模型可见 ∧ 下一 provider history **同字节** | HOST-021、TODO-005/013 | blocking |
| F | execute throw | 记录 after 是否运行；协议不依赖其运行 | HOST-021 | 冻结观测 |
| G | after 运行瞬间 | 冻结 ToolPart 是否已 durable completed；Accepted 仍走双路径 | HOST-022、TODO-004 | blocking（防误绑） |
| H | 仅 sessionID+callID | 完整 SDK snapshot **唯一**定位 ToolPart / assistant / run / ordinal / XTrace range | HOST-025、HOST-011 | **blocking** |
| I | 第五态消费者回归 | 承接 D；UI 不稳则强制 compatibility `in_progress` | HOST-023 | blocking if D flaky |
| J | live Accepted | executor 成功→after → `TodoWriteAccepted` 与 Prepared digest 对齐 | HOST-022 | blocking |
| K | recovery Accepted | 无 after 时 snapshot completed ToolPart → 同一 digests Accepted | HOST-022、TODO-012 | blocking |
| L | Prepared+失败 | 不 Accepted；sink 乐观 Pk 不构成 checkpoint；下次 before Journal 覆盖 sink | HOST-022、TODO-007 | blocking |
| M | REVISE 消费后 reconcile | Host TodoTable == settled current；**零**新 checkpoint/review facts | HOST-023、TODO-005/007 | blocking |
| N | V2 runner | 无 hook parity 时 MagicTodo Manager Attempt **construction fail closed**；零裸 `SessionTodo.update` | HOST-024、TODO-004 | **blocking** |
| O | 无 Host core / 同名覆盖 | builtin executor 仍为 sink；无 OpenCode 源码修改；无 plugin 同名 tool 夺权 | HOST-017 | 静态/集成 |
| P | bridge 非真相 | crash 后忽略 Map；只从 Journal 恢复；failure cleanup 无残留 key | HOST-021、TODO-012 | blocking |
| Q | description 面 | 含 tagged/reviewing/lag/multi-reject；**不含** reviewer/session/barrier/witness/2N | HOST-018、TODO-013 | 静态 |
| R | multi-todowrite | 同 assistant message 两个不同 callID → 全部拒绝、无 winner | HOST-020、TODO-004 | blocking |

代表落点（实现后）：`tests/unit/host/magic-todo-membrane-canary*.test.mjs`、integration plugin hook 契约、e2e Manager todowrite unhappy-path。未落地前以本表为 release gate 清单（对齐 `changes/active/magic-todo.md` §47 Host canary 门禁）。

### 反例（必须红）

```text
before mutation 改写 historical ToolPart.input          → A 红 → 停
after 回写“修复”被污染的历史 input                      → 仍停（不可补救）
V2 settle 静默写 TodoTable                              → N 红
REVISE settlement 后 sink 永久留否决 Pk                 → M 红
bridge / Host TodoTable 当 canonical 恢复源             → P/L 红
plugin tool 名 todowrite 覆盖 builtin                   → O 红
```
