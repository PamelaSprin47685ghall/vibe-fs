# Host — 目标实现

## HOST-004：Reconciler

- Single-flight：同一 session 同时最多一次 reconcile。  
- Dirty：idle 到达设 dirty。  
- Unknown：一次 idle 建 Dirty latch；最多 3 次因果重读；仍 Unknown 则保持 Dirty 等下一信号。

```fsharp
type ReconciledTurn =
    { SessionId; UserMessageId; AssistantMessageId
      AgentRole: AgentRole option; Directory: string
      Parts: ProviderVisiblePart array
      Outcome: TurnOutcome }  // Completed | Failed | Aborted
```

## HOST-009：生命周期

```text
plugin start
→ create runtime services
→ register static tools/transforms
→ lazily create association/companion on first projection
→ dispose: cancel Tasks, kill PTY/process, dispose sessions
```

## Compaction 程序（归属 HOST-006）

### 预防

关闭 automatic / overflow（共键）/ autocontinue / prune。  
静态读配置不够：首个 managed session 第一轮请求后，compaction pseudo-run 必须为零。  
设置不可用优先于 pseudo-run 报错（根因在设置）。

### 收容

识别：`agent="compaction"` ∨ `mode="compaction"` ∨ `summary=true`。

```text
观察 pseudo-run
→ ActivePrefixEpoch 退役（Snapshot→None，EpochId+=1）
→ PrefixCoverage 归零
→ RecordCoverage.IngestedThrough 与 Frames 保留
→ 写 ContextReanchored（PERSIST-010，同一 ObservedCompactionMessageId 幂等）
```

入口：`HostCompactionGate` + 启动探测。关掉配置单独不算已证明预防。

## HOST-010：Transform → ProviderRunIdentity

transform input 为空对象。绑定靠因果读：在 transform 中从 SDK 找**唯一**未完成 assistant：

```text
role = assistant
time.completed 未设
parentID = transform 输出最后一条 user 的 id
id 为 session 内 assistant 最大者
```

```text
命中 0 或 ≥2 → 不写 seal
compaction / summary 路径 → 不写 seal
```

无 seal 时第二次 PERFECT 只能 PendingIdentity/Rejected（REVIEW-010）。

Canary 用 journal 代理等式，不要求共时观测 transform 内存 id ≡ ToolContext.messageID：

```text
Reviewer: ReviewVerdictRecorded.ProviderRun == ProviderInputSealed.ProviderRun
X: PrefixRebaseCommitted.SolvingProviderRun 唯一非空
```

## Marker 程序（归属 HOST-013）

链序（seal 之前）：

```text
XTraceCapture → Companion → XWire → EnforcerHost
→ PairProgrammingThoughtTransform → ReviewSeal
```

- 锚点：每个 user 或已完成 tool-result；`anchorIndex+1` 插入；从后向前处理。  
- 全锚点重放（Host 不持久化 synthetic）。  
- `id = digest(sessionId + anchorMessageId + source)`，禁止随机/时间。  
- 幂等键 = 锚点 identity + source；同锚点只插一次。  
- 排除路径按 `source` 过滤，禁止只按中文正文过滤。  
- 文本与 source 单点定义。
