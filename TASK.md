我已复审这次上传的**更新版 SSOT 与仓库快照**。结论是：

> 上一轮裁决只完成了一半。`host-nudge-canary` 已改成验证后 abort，但 `fallback-canary` 仍保留无 review witness 的 Manager 正常终态；同时新增 SSOT 中几条最关键的精确定义，代码还没有同步实现。

由于当前环境没有 `dotnet`，以下是静态审查，不能声称新快照已真实通过编译或 P0。

# 已正确落地的部分

1. 更新版 SSOT 已明确写死：

   * Inspector 工具集合严格等于 `executor`；
   * 只有 Manager 能操作 PTY；
   * Orchestrator 的 `fork` 必须是收窄版本；
   * Manager 无 witness 不得结束；
   * 自压缩只更新 `LatestB`；
   * P0 必须采用因果事件错峰启动（canary N 等待 canary N−1 精确输出 `[setupScenario] ready` 后 spawn）。

2. 生产角色表已经把 Inspector 收窄到 `Exec`，Manager 保持 `fork/join/list`，Coder 也已加入 one-shot Inspector 权限。

3. Host Signal 已改为单一来源优先选择、owned fail-closed，并且 Adapter 只接受 `session.status idle/retry` 与 `session.deleted`。

4. `join()` 已加入 `NothingToJoin` 和 cancel 分支，Large Gate 也开始支持取消。

5. `host-nudge-canary` 已删除 Manager 正常文本终态，在 nudge 后 abort 父 session；这一总体方向正确。

---

# 仍然阻断 P0 的问题

## P0-1：`fallback-canary` 仍未执行裁决

它仍然：

```text
向 Manager 发 prompt
→ Manager fork/reuse Coder
→ Manager 输出 “Parent completed fallback round N.”
```

每轮都有一个无 Reviewer、无双 PERFECT 的 Manager terminal。`parentTurn.awaitTerminal()` 还要求捕获该 terminal。

因此在真实 ReviewGuard 语义下，它仍必然被 nudge。当前测试没有改成直接驱动 Coder，也没有给 Manager 建立合法 witness。

### 正确修改

彻底删除：

```text
expectParentRound
parentPrompts
parent-round-N
Manager session
Manager Blogger
通过 fork result 提取 agentId
```

直接创建 Coder session，连续提交四个 root user prompts：

```text
Coder attempt 1 → A retry
Coder attempt 2 → A retry
restart
Coder attempt 3 → B retry
Coder attempt 4 → B retry / Dead
```

Fallback canary 不应依赖 Manager、ReviewGuard、Reviewer、Git tree 或 Manager mailbox。

---

## P0-2：生产 ReviewGuard 仍有“空 tree 免审查”后门

`HostReviewGuard.missingTree` 当前把这些情况返回为 `ReviewGuardNotApplicable`：

```text
空字符串
NO_HEAD_TREE
Git empty-tree hash
```

这直接违反已裁决的语义：

> Manager 无当前 tree 的 confirmed witness，绝不能正常结束。

空仓库、空提交或未修改 tree 都不等于代码已经审查。这个例外正是此前“空初始提交让 host-nudge 单跑通过”的根源。

### 必须删除

```fsharp
if isEmpty then
    ReviewGuardNotApplicable
```

Manager 角色只允许两种结果：

```text
Confirmed witness → finish
其他一切 → nudge 或明确 dependency failure
```

`ReviewGuardNotApplicable` 最多只能用于非 Manager session，不能用于 Manager terminal。

---

## P0-3：`host-nudge-canary` 的完成屏障是假因果屏障

测试现在等待：

```js
e.type === 'message.updated'
&& e.sessionID === parentId
```

这不能证明目标 `fork(existingId, prompt)` 已完成。

它可能匹配：

* Manager 的文本流更新；
* 其他 tool part 更新；
* Blogger/后台相关消息；
* nudge tool call 尚未完成时的中间更新。

而更新版 SSOT 明确规定：

* `message.updated` 是碎片，不是真实业务事实；
* Watchdog 有效心跳是**明确的 Tool 完成**，不是任意 SSE。

### 正确修改

在 `manager-fork-nudge` expectation 被消费后，读取 Manager 完整 messages，确认：

```text
tool = fork
arguments.agent = 目标 agentId
arguments.prompt = 目标 nudge
tool state = completed
tool result 明确为 Nudged/accepted
```

必须定位到确切 call ID，不能只等待任意 `message.updated`。

确认后再 abort。

---

## P0-4：Review 仍只数 ToolCallId，没有实现新 witness

更新版 SSOT 已要求两次 PERFECT：

* 来自不同 `ProviderRunIdentity`；
* 第二次所在 run 的 user 输入包含第一次后的确认请求；
* witness 包含 ManagerJob、Reviewer、Barrier、Tree、两个 RunId 和两个 ToolCallId。

当前实现仍然只有：

```text
ToolCallId
GitTreeHash
ConsecutivePerfects
RecentToolCallIds
CurrentBarrierKey
```

`VerdictSurface` 没读取 Provider Run ID；journal fact 也没有保存 Provider Run ID；fold 只要两个不同 ToolCallId、相同 tree，就会确认。

这允许 Reviewer 在**同一次 assistant/provider run 中连续调用两次 PERFECT**直接通过，违背“双重独立确认”的第一性原理。

### 必须修改

`ReviewVerdictRecorded` 至少增加：

```fsharp
ManagerJobId
ReviewBarrierId
ReviewerSessionId
ProviderRunId
RootUserMessageId
ToolCallId
GitTreeHash
Verdict
```

第二次 PERFECT 的接受条件：

```text
ProviderRunId 不同
ToolCallId 不同
Barrier 相同
Tree 相同
第二次 RootUserMessageId 对应 Guard 发出的 confirmation prompt
```

---

## P0-5：Y 自压缩仍然会强制切换 X epoch

新 SSOT 已明确：

```text
自压缩：
LatestB = B'
FrozenB 保持不变
直到下一次达到 X 上下文阈值才进入新 epoch
```

但当前实现：

```text
TrySelfRebase
→ pendingEpochSwitch = true
→ 下一次 Transform 调用 TakePendingEpochSwitch
→ SwitchEpoch(...)
→ FrozenB = LatestB
```

也就是说，Y 自压缩仍会直接制造 X 的冷缓存边界。

### 必须删除

```fsharp
pendingEpochSwitch
TakePendingEpochSwitch
self-rebase 后的 SwitchEpoch
```

`TrySelfRebase` 只能原子更新：

```text
LatestB
保持原 BlogBase
```

绝对不能触碰 `ActivePrefixEpoch`。

---

## P0-6：新的 epoch 切换算法基本没有实现

更新 SSOT 要求：

```text
ProjectedInputTokens + ReservedOutputTokens > ContextLimit
并且新 B + rawTail 确实比当前输入短
并且 coverage proof 成功
```

Estimator 不可用时：

```text
UTF-8 bytes ÷ 3
ContextLimit = min(provider_max, model_max, host_max)
任一上限未知则 fail-closed
```

当前实现仍然只是：

```fsharp
canonical JSON 字符数 / 4
>= budget * 0.8
```

并且：

* 没有 `ReservedOutputTokens`；
* 没有三个 limit 的最小值；
* 没有压缩后确实更短的检查；
* ReplacementActive 后没有后续基于阈值的正常 epoch switch；
* 反而依赖 self-rebase 的 `pendingEpochSwitch` 来切换。

### 必须改成纯函数

```fsharp
shouldSwitchEpoch:
    BudgetFacts ->
    CurrentProviderProjection ->
    LatestB ->
    BlogBase ->
    ActiveEpoch option ->
    EpochCandidate option
```

Transform 只能消费这个结果，不得自己拼阈值与 watermark。

---

## P0-7：`CoveredPrefixDigest` 不是 digest，而且从不验证

第一次 freeze 时当前代码保存的是：

```fsharp
messages
|> List.take watermark
|> jsonOfMessages ...
```

也就是完整 canonical JSON 字符串，而不是 hash。

随后每次 replacement 时只使用：

```fsharp
inject epoch.FrozenB epoch.CutoffMessageIndex
```

没有重新计算当前 prefix digest，也没有与 `CoveredPrefixDigest` 比较。

因此 message 内容发生 retry/revert/undo 后，旧 cutoff 仍可能继续删除新上下文。

### 必须实现

```text
candidateDigest = SHA-256(provider-visible messages[0..cutoff])
```

每次投影前：

```text
重新计算 currentDigest
currentDigest != epoch.CoveredPrefixDigest
→ 禁止 replacement
→ 原样返回 raw messages
```

此外，`EpochId` 不能嵌入完整 canonical JSON；应使用稳定 hash。

---

## P0-8：Synthetic ID 仍未遵循 SSOT

SSOT要求：

```text
syntheticId = hash(sessionId + epochId + semanticKind)
```

当前始终是：

```text
companion-b-head
```

这虽然固定，但不是 session/epoch 唯一身份，也无法可靠区分：

* 不同 session；
* 不同 epoch；
* restart 后的具体 synthetic fact。

应改为类似：

```text
companion-b-head-<sha256(sessionId|epochId|companion-head)>
```

并在重复 transform 时通过该确定性 ID 检测同一 epoch，而不是检测任意 `"companion-b-head"`。

---

## P0-9：P0 runner 启动门协议校验与清理

根据最新 SSOT，正确契约已更新为：**第一个 canary 立即启动；canary N 必须等待 canary N−1 输出精确的 `[setupScenario] ready` 后再启动**。`previousBarkPromise` 与 readiness marker 检测这一核心流程方向正确。

当前 runner 仍需核查与清理的细节：

1. **清理旧固定 delay 代码与错误注释**：
   彻底移除 `runPool` 中保留的 `index * STAGGER_DELAY_MS`（默认 2000ms）及相关错误注释，禁止任何固定 sleep 或轮询。
2. **启动门异常释放机制**：
   若前一个 canary 在输出 `ready` 前异常退出或超时，必须及时释放启动门并记录失败，不得永久阻塞后续 canary 启动与全套结果收割。
3. **有界 Safety Fallback**：
   允许设置有界的 ready safety fallback 仅用于释放启动门，但严禁将未 ready 场景直接判为成功。

### 规范契约要求

* 第一个 canary 立即 spawn。
* canary N 仅在 canary N-1 输出 `[setupScenario] ready` 后 spawn。
* ready 表示 listen + health 完成，后续 canary 启动后与先前 canary 继续并发运行，不等待场景结束。

---

## P0-10：PTY 权限在角色表正确，在工具 Schema 仍未正确收窄

角色权限表已把 Inspector 收窄到 Executor，这一点正确。

但 Manager 与 Orchestrator 仍共享同一个 `forkArgs`：

```text
agent
prompt
signal
```

Orchestrator 只是到 execute 阶段才检查：

```text
agent != manager → error
```

这违反新 SSOT：

> Orchestrator 的 schema 中只能看见 `fork(managerPrompt)`；PTY 和普通 agent variants 必须根本不可见。

### 必须拆成两个工具定义

```text
managerForkTool
orchestratorForkTool
```

Manager schema：

```text
new agent
existing agent nudge
PTY create
PTY operation
```

Orchestrator schema：

```text
managerPrompt: string
```

不能继续共享 union schema 再运行时拒绝。

另外，更新 SSOT规定 Browser 是：

```text
read, glob, grep, web tools
```

当前角色表只有 `Read + Network`，仍缺 `Glob/Grep`。

---

# 发布状态仍未变化

尽管新快照已经加入 `build-package.json` 和 `MIGRATION.md`，版本仍然是：

```text
0.3.0
private: true
license: UNLICENSED
```

根包、`next/package.json` 和 build package 都尚未进入 0.4.0 RC 发布状态。

---

# 建议立即执行顺序

1. 删除 ReviewGuard 空 tree 例外。
2. 把 `fallback-canary` 改为直接 Coder。
3. 把 `host-nudge` 的任意 `message.updated` 改成确切 fork tool completion。
4. 删除 `pendingEpochSwitch`，自压缩只改 `LatestB`。
5. 实现完整 epoch candidate、digest hash 与每轮 coverage 验证。
6. Review fact 加 ProviderRunIdentity 和 confirmation prompt identity。
7. 拆分 Manager/Orchestrator fork schema。
8. 校验并清理 canary runner，确保符合因果事件错峰启动 (canary N 等待 canary N−1 准确 `[setupScenario] ready`) 并具备异常释放机制。
9. 单跑两个修正 canary。
10. 再跑 P0 16/16 × 3。

当前最准确的状态仍是：

> **SSOT 已明显变细，但实现还没有追上新 SSOT；P0 15/16 的问题不只是测试卡点，仓库中仍存在多个真实产品语义旁路。**
