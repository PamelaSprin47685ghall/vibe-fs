# STATUS/plan-CTX-006 — CTX-006 生产接线计划

本文件是在 `STATUS/blocker-CTX-006.md` 基础上的执行计划：把 Y 侧恢复槽内 squash 的完整生产链路接好，使 `next/Session/CompanionHostBlogger.fs:155` 的 `SHOCK-UNMIGRATED[CTX-006]` 标记可以清零。

## 目标

让一次真实的 Work Session 失败后，下一个 armed 的 Companion Session 能：

1. 识别 armed + primed + 有 frame 材料的恢复槽；
2. 以 `ProviderRequestKind.BloggerSquash` 发起 squash 请求；
3. 把 `BlogSquashCommitted` 作为唯一 durable fact 写入；
4. squash 成功后在同一槽继续 BloggerMain，失败则只推进 fallback cursor 不发主请求；
5. 与 X 侧 probe 的 reconcile 路径互不污染，不新增第二个 writer。

## 当前已就位（不做改动，只调用）

| 构件 | 位置 | 状态 |
|------|------|------|
| `SlotArming` / `RecoverySlot` | `Domain/RecoverySlot.fs` | 纯函数完成，第 1 层测试覆盖 |
| `AttemptExecutionProfile` / `AttemptPlanner` | `Domain/AttemptPlanner.fs` `Domain/PromptAuthority.fs` | 构造函数唯一，probe 绑定已表达 |
| `XProjectionChoice` / `PrefixProbe` | `Domain/XPrefixProjection.fs` 等 | X 侧已接入 `XWire.applyTransform` |
| `CompanionProjectionBuilder` | `Domain/CompanionProjectionBuilder.fs` | Normal / Squash 两种投影已纯函数实现 |
| `BlogProjection.applySquash` / fold | `Journal/BlogProjection.fs` `Journal/Fold.fs` | `BlogSquashCommitted` 的 fold 与投影已就位 |
| `AgentFact.BlogSquashCommitted` | `Kernel/Fact.fs:355` | 事实类型已定义 |
| `HostSignalBootstrap.onTurn` arming | `OpenCode/HostSignalBootstrap.fs:69-71` | 失败/中止时调用 `scope.ArmRecovery` |
| `CompanionHostBlogger.squash` | `Session/CompanionHostBlogger.fs:122` | 发送 `BloggerSquash` 的壳已存在，但 `Completed+valid` 分支 failwith |

## 缺失且必须新建/接线的环节

### A. Y 的 transform 入口能拿到 armed 槽

当前 `OpenCode/CompanionTransform.fs:90-97` 只判断 `isCompanionSession`。Work Session 走 `companion.TransformRaw` 只做 COMPANION-005 累积；Y Session（即 Companion 自身）原样返回，没有任何 squash 决策。`TransformRaw` 看不到 attempt 结局是正确的（CTX-002），但它也看不到当前槽是否 armed/primed/hasMaterial——这个信息需要在 `handleCompanionTransform` 里从 `scope.TryRecoveryArming` 和 `journal` 投影读取。

A1. 在 `handleCompanionTransform` 中，对 Y Session（`isCompanionSession = true`）增加一条路径：

- 查询 `scope.TryRecoveryArming`（Y 工作会话的 arming 由 Work Session 失败后通过某条路径写入，或者由 Y 自己上一次失败写入）；
- 查询 `AgentProjection` 的 `Blog` / `PrefixEpoch` / `Fallback`；
- 计算 `RecoverySlot.mayRecover`；
- 若 `mayRecover` 为真且 `hasMaterial` 来自 frame，决定进入 squash。

关键未定：Work Session 失败后，Y Session 的 arming 从何而来？

`HostSignalBootstrap.onTurn:71` 的 `scope.ArmRecovery turn.SessionId` 只 arm 失败的那个 session。Work Session 失败后需要把 arming 传播到其 Companion Session，或者让 Y 在发现父 Work Session 有 armed 槽时自我 arm。两种方案：

- 方案 A1a（推荐）：Work Session 的 `XWire.applyTransform` 在 armed 槽且 `mayRecover` 为真时，把 `RequestKind` 标记为 `WorkMain` 并携带 probe；若该 session 的 `Blog` 有 frame 材料，同时向其 Companion Session 的 `scope.RecoveryArming` 写入 `ArmedByAdvance`。这样 `handleCompanionTransform` 里 Y 的 arming 就是本 session 自己 arm 的，与 Work Session 的失败一一对应。
- 方案 A1b：在 `HostSignalBootstrap.onTurn` 里，失败时除 `scope.ArmRecovery turn.SessionId` 外，还查找其 Companion 并 arm。这会把一个失败同时 arm 两个 session，容易让下一次 Y 请求误判——且 `onTurn` 不区分失败来自 X 还是 Y。

决定：方案 A1a。 因为 Y 的 recovery 是对 Y 自己失败（或 Work Session 失败后转嫁给 Y）的响应。当前 Work Session 失败已经会触发 X probe；如果同一 armed 槽也满足 `hasMaterial` 来自 `Companion.Blog`，则该 Work Session 的 `AttemptExecutionProfile` 需要同时把 Y 的 squash 作为子请求安排。这与 `CTX-006` 的表格一致：Work Session 做 X prefix probe，Companion Session 做 Y frame squash，两者是同一恢复槽的两个动作。

但 X 与 Y 是不同 session，一个 transform hook 不能同时改两个 session 的消息。因此需要把"Y 需要 squash"作为一条 durable 或可观察的状态：

- 在 Work Session 的 `XWire.applyTransform` 中，若 `mayRecover` 且 `Blog.hasCoverage` 为真，向 Y Session 的 `scope.RecoveryArming` arm（非 durable，跨 session 的内存字典）。
- 该 arm 在 Y 的下一次 `experimental.chat.messages.transform` 中被 `handleCompanionTransform` 读取。

这是跨 session 的内存协调，但不写入 journal，因为 arming 是序列内控制流事实（`RecoverySlot` 注释第 3-6 行）。

### B. Companion 子请求安排：从 `TransformRaw` 到 `squash` 调用

当前 `CompanionHost.TransformRaw` 直接返回 `messages`，不做任何请求。`SubmitProjection` 是异步博客入口，由 Work Session 的 `CompanionTransform` 在 transform 时调用。Y 的 squash 不能由 `SubmitProjection` 触发，因为 `SubmitProjection` 接收的是 provider 投影，不携带 armed 信息。

B1. 新增 `CompanionHost.SquashRequest`（或类似）入口：

- 接收 `frameCount`；
- 调用 `CompanionHostBlogger.squash`；
- 返回 `Task<BloggerCompletion>`；
- 在 `handleCompanionTransform` 中调用。

B2. `handleCompanionTransform` 对 Y Session 的处理：

- 若 `isCompanionSession = true` 且该 Y 正在 transform 自己的下一轮（即 Y 收到用户消息或系统消息），读取 arming；
- 若 armed+primed+有 frame，先调用 `SquashRequest`，等待其完成；
- 根据 `BloggerCompletion` 或异常决定：
  - squash 成功 → 继续 BloggerMain；
  - squash 失败/中止 → 不发起 BloggerMain，把失败返回给 Host 以推进 fallback cursor；
  - squash 结果无效 → 按 CTX-007 `CompletedInvalid` 处理为 `MainWithoutSquash`（不 repair，因为 compression 不值得 repair）。

关键未定：Y Session 的 transform hook 是同步的，无法等待 `Task`。

`experimental.chat.messages.transform` 是同步 hook（返回 `messages`）。`companion.TransformRaw` 当前是同步的。如果 squash 需要额外 LLM 调用，它不能在该 hook 内完成。

需要两种可能：

- 方案 B2a：把 squash 改在 `HostSessionNudge` 或某个异步发送点完成。但这要求 Prompt 发送前有时间窗口做子请求。
- 方案 B2b：使用 OpenCode 的 `experimental.chat.messages.transform` 无法完成异步 squash，这是 Host 能力问题，可能触发 SSOT 例外。

需要先做源码判定：读 `../opencode/packages/opencode/src/session/prompt.ts:1255` 及 transform hook 的调用上下文，确认 transform 是否允许返回 Promise/异步。如果 transform 是纯同步且不能阻塞等待，那么 CTX-006 的 Y squash 不能在该 hook 内发生，需要另一个 hook（如 `experimental.chat.message` 异步处理）或 SSOT 例外。

### C. `PromptDispatcher` 对 `BloggerSquash` 的发送

当前 `CompanionHostBlogger.sendBloggerPrompt` 调用 `PromptDispatcher.forJournal` 的 `SendAgentOwnerRoot`，传 `deps.EffectiveAgent` 与 prompt 文本。`RequestKind` 已改为 `BloggerSquash`，但 `SendAgentOwnerRoot` 把 origin 写死为 `AuthorityRoot AgentOwnerRoot`。

C1. `PromptDispatcher` 需要支持 `BloggerSquash` 作为 `ContinuationKind` 或新的 origin，使得：

- `PluginPromptClaimed` 的 `ContinuationKind` 字段可识别为 squash；
- `PromptKey` 派生包含 squash 的 `ProviderRun` 与 frame epoch，保证同一槽主请求与 squash 不重键；
- `RequestKind` 已存在，只需让 `AttemptExecutionProfile` 能携带 `BloggerSquash` 而非 `BloggerMain`。

或者更简单地：`BloggerSquash` 仍走 `SendAgentOwnerRoot`，因为它是 Blogger 子会话的一次 Authority Root（Blogger 自己是一个 session，每次 squash 都是它的 authority root）。此时 `RequestKind` 在 `AttemptExecutionProfile` 中标记，而不改变 PromptDispatcher 的 send 路径。

决定：后者更合 SSOT。 Prompt 发送仍是 `SendAgentOwnerRoot` 唯一入口；`RequestKind` 通过 `AttemptExecutionProfile` 注入 Prompt metadata。`BloggerHost` 当前使用 `bloggerEffectiveAgent` 作为 agent，`RequestKind` 已 mutable 写入 `deps.RequestKind`。

### D. `XWire.reconcileAttempt` 处理 BloggerSquash

当前 `XWire.reconcileAttempt` 只在 `AttemptPlanner.promotableProbe` 非空时追加 `PrefixRebaseCommitted`。对 Y 的 `BloggerSquash` 结局没有处理。

D1. 在 `XWire.reconcileAttempt` 中：

- 拿到 `AttemptPlan`；
- 若 `plan.Profile.RequestKind = ProviderRequestKind.BloggerSquash`：
  - `Completed` + `TerminalValidity.check` 通过 → 唯一 `durable.Writer.BlobWriter.Write` 写 frame 文本，再 `AgentJournal.appendAgent` `BlogSquashCommitted`；
  - `CompletedInvalid` → 不提交，不推进 cursor，清理 attempt plan；
  - `Failed` / `Aborted` → 推进 fallback cursor（`FallbackController.recordFailure`），清理 attempt plan，不发 BloggerMain。
- 若 `BloggerSquash` 成功 → 清理 plan，不重试同一槽（squash 只发一次）。

D2. `BloggerSquash` 成功后如何继续 BloggerMain：

- 同一 armed 槽的 squash 成功后，应当立即继续 BloggerMain；
- `XWire.reconcileAttempt` 不发起新 prompt，它只写 journal；
- BloggerMain 的继续由 `Host` 的下一轮驱动，或者由 `HostSessionNudge` 在 squash 成功后 nudge。

关键未定：squash 成功后如何不丢失同一槽的 BloggerMain。

如果 squash 是独立的 prompt，Host 收到 squash 的 `TurnCompleted` 后，该 turn 结束。要继续 BloggerMain，需要让 Host 把 squash 与 main 视为同一个逻辑 user turn 的两个请求，或者在 squash 完成后自动再发 main。当前 `Host` 没有"子请求"概念。

可能方案：

- 方案 D2a：squash 与 main 合成一个 prompt：先请求 squash，收到结果后把结果作为内部上下文，再请求 main。这需要两次 provider 调用在同一 turn 内完成，而 `experimental.chat.messages.transform` 是同步的，无法做到。
- 方案 D2b：把 squash 与 main 分开发。squash 成功后，由插件再次调用 `HostSessionNudge.sendContinuation` 发送 main。但这需要 Host 支持在一个 `TurnCompleted` 后插件立即发新 prompt，且不等待用户输入。
- 方案 D2c：让 squash 的结果作为一条新的 "user" 消息追加到 Companion Session，然后让 Host 继续正常的 main 请求。这与 `Blogger` 的 `SubmitProjection` 把 delta 追加为最后一条 user 消息类似。也就是说：squash 请求本身是一次 `Blogger` 的 authority root，成功后把返回的压缩文本作为 frame 追加到 journal，同时触发下一次正常的 `SubmitProjection`（如果还有 delta 要提交）或把 main 当作下一次 natural turn。

当前理解：最可行的实现是 D2c。 squash 是一次独立的 Blogger authority root，其结果（`TurnFormalText`）被 `BlogSquashCommitted` 记录为新的 frame；后续的 main 请求在需要时自然发生。因此 `BloggerMain` 与 `BloggerSquash` 不是"同一槽必须连续"，而是：一个 armed 槽内，先做 squash（压缩历史），再做 main（提交当前 delta）。如果 squash 失败，main 不做；如果成功，main 仍然可以做。

但如何"仍然可以做"？如果 `handleCompanionTransform` 是同步的，它只能决定 transform 后的消息；不能发起第二个 prompt。所以 squash 必须发生在另一个异步边界。

这再次指向 B2 的问题：需要确认 Host 能力。

## 工作包顺序

```
P1  源码判定：Y 的 squash 能否在现有 Host hook 内异步完成
    输入：`../opencode/packages/opencode/src/session/prompt.ts` transform 调用点
    输出：判定 D2c 可行，或需要 SSOT 例外 / 改 Host（后者被 ARCH-003 禁止）

P2  将 Y arming 接入 CompanionTransform（方案 A1a 或根据 P1 调整）
    输入：XWire.applyTransform 的 armed 槽信息、Blog.hasCoverage
    输出：Y Session 的 handleCompanionTransform 能识别需要 squash

P3  完成 CompanionHostBlogger.squash 的 Completed 分支
    输入：P2 的调用、BloggerSquash 的 ProviderRun、frameCount
    输出：调用 durable blob writer + AgentJournal.appendAgent BlogSquashCommitted
    约束：该函数是 `BlogSquashCommitted` 的唯一 writer

P4  XWire.reconcileAttempt 增加 BloggerSquash 分派
    输入：AttemptPlan（RequestKind = BloggerSquash）与 ReconciledTurn outcome
    输出：Completed 时写 blob 与 BlogSquashCommitted；Failed/Aborted 时推进 cursor

P5  恢复槽失败后的下一槽行为
    输入：RecoverySlot.onSquashOutcome、onMainOutcome
    输出：squash 失败只推进 cursor 不发 main；main 失败后 cursor 也推进

P6  删除 SHOCK-UNMIGRATED 标记，跑编译 + test:mjs
    输入：P1-P5 完成
    输出：`shock-audit` 0 未迁移、`dotnet build` 绿、`test:mjs` 绿
```

## 验证阶梯

按 `VERIFY-001`：

| 层级 | 命令 | 通过条件 |
|------|------|---------|
| 0 | `npm run gate:static` | 包括 `ssot-lint` / `architecture-gate` / `shock-audit` |
| 1 | `npm run test:mjs` | 新增 / 更新 `tests-mjs/Context/` 的 CTX-006 纯函数测试 |
| 2 | `npm run build` | Fable 产物能 `import` 无错 |
| 3 | `npm run test:harness` | 森林自检通过 |
| 4 | `npm run test:e2e:companion` 或单 canary | K8f 的 X-A–X-D 至少有一条能观察到 Y squash |

## 阻塞与退出条件

- P1 若判定 Host transform hook 无法完成异步子请求，且没有其它现有 hook 可用，则必须停止并写 `STATUS/blocker-HOST-<xxx>.md` 申请 SSOT 例外。ARCH-003 禁止改 Host，因此若现有能力不够，只能调整 CTX-006 的实现语义。
- P3 必须保证 `BlogSquashCommitted` 只有一个 durable writer。任何把 `applySquash` 直接当 writer 的写法都是违规。
- 不得新增 `SHOCK-UNMIGRATED` 以外的 TODO 标记。中间态必须显式 fail closed（抛 `InvalidOperationException`）而非静默跳过。

## 与本计划冲突的历史决策

- `CompanionHost.TransformRaw` 目前声明为同步 `obj list -> obj list`；若 P1 发现必须异步，该签名需变为 `obj list -> Task<obj list>`，并检查 `PluginHostInterop` 的 emit 模板。
- `XWire.applyTransform` 当前对 Work Session 只处理 X probe；若需要同时 arm Y，需要在该函数内读 `SessionAssociationProjection` 找到 Y session id。
