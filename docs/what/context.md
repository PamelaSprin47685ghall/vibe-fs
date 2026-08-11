# 上下文恢复 — 可观察行为

条款前缀：`CTX-`。  
分派边界见 `shape/context.md`。  
probe / squash / delta 算法见 `how/context.md`。

## 核心裁决

**不预测，只恢复。** 正常请求总是先直接执行。只有真实 provider attempt 失败之后的恢复槽，才允许尝试前缀替换或 frame 压缩。

## CTX-001：不观察容量

不得读取、查询、推导、缓存任何模型上下文窗口大小。  
禁止 contextWindow / remainingTokens / headroom / nearLimit / shouldCompact / ensureCapacity 等一切同义词概念。  
禁止 tokenizer、模型窗口表、字节→token 换算。  
管理员配置与 provider 元数据不得改变本条。

唯一允许的字节计量：CTX-003 输入合同，以及文件/进程类既有合法计数（EXEC-011）。

## CTX-002：不主动预测溢出

请求前不得判断「是否接近上限」。  
禁止按投影长度比例、剩余输出预算、Y 字节阈值、累计 token、型号选压缩点。  
真实失败是唯一恢复触发信号。第一次溢出表现为失败——这是主动接受的代价。

区分：HOST-006 重锚读的是「已发生的 compaction 事实」，不是「还剩多少空间」。

## CTX-003：最低环境合同

支持的 LLM 在扣除固定 system/tools/封装后，至少能接收 **200 KiB** provider-visible 动态输入。  
`BloggerDeltaLimitBytes = 200 * 1024` 是**输入合同**，不是窗口估算：不与窗口比、不算比例、不触发主动 squash。  
计量点：TOML 渲染后的 UTF-8 字节。

## CTX-004：输出预算属 provider

插件不计算 squash 应占多少 token，不检查压缩比。  
唯一内容校验：`isValidTerminal = 非空 ∧ 非 XML-only`（与 FALLBACK-008 对齐）。

## CTX-005：失败不分类

控制流只看 snapshot `Outcome`：`Completed | Failed | Aborted`。  
不得按错误文字/类型名区分溢出、网络、限流等。  
「溢出」只许出现在诊断，不得进 Journal 字段或 probe/squash 判定。  
compaction 来源同样不分类。

## CTX-006：恢复槽

合取三者才允许恢复动作：

```text
armed ∧ primed ∧ hasMaterial
```

| Session | 动作 | 额外 LLM |
|---------|------|----------|
| X | prefix probe（不先永久提交） | 无 |
| Y | frame squash（有效则提交） | 一次 |

无材料时发正常主请求——正常状态，不是错误。  
恢复槽是机会，不是「必然压缩」。

## CTX-009：X 不发压缩请求

Work Session 从不向主模型发送「请压缩历史」类请求。压缩只发生在 Y 的 squash 或 X 的 prefix 替换投影。

## CTX-010：attempt-local prefix probe

恢复槽中替换 X 前缀时，**不**立即改 ActivePrefixEpoch。候选只进不可变 `AttemptExecutionProfile.ProjectionChoice`。

```text
probe 成功 → 提升为 ActivePrefixEpoch（写 PrefixRebaseCommitted，EvidenceKind=Probe）
probe 失败 → 丢弃候选；后续非 probe 槽用旧 epoch
```

禁止先提交再回滚；故无 PrefixProbeRolledBack 类事实。  
`A′` 失败不禁止 `B′` 用等价候选重试。  
本条只描述失败驱动 probe 路径；TodoCheckpoint 路径见 CTX-015，二者共用同一 ActivePrefixEpoch，不另造 epoch SSOT。

## CTX-013：Blogger delta TOML

- data-only TOML 冻结进 blob；instruction header 投影时加。  
- 硬上限 200 KiB 渲染后字节；超限确定性切块/截断策略保持可复现。  
- 含 decision-relevant host-visible reasoning；无 hidden reasoning 伪造。  
- 与 LWR gap 分投影，禁止混用 renderer 输出当 canonical digest。

## CTX-014：诊断边界

可观测诊断不得变成控制输入：不得用日志字段驱动 Fallback/probe/squash 分支。

## CTX-015：ActivePrefixEpoch · TodoCheckpoint evidence

既有 `ActivePrefixEpoch` / `PrefixRebaseCommitted` 是**唯一** prefix epoch SSOT。Magic Todo lag-1 rebase **必须**进入该合同，不得平行 todo-only epoch、不得 `NeedRebase`/`RebaseRequested` Stage（TODO-009、TODO-012）。

```text
EvidenceKind = Probe | TodoCheckpoint
```

TodoCheckpoint commit 与 probe 同等级字段，至少含：`EpochId`、`PrefixSnapshot`、`Cutoff`、`SealRoot`、`YBundleRef`/`YBundleDigest`、`ProviderPrefixDigest`；以及 `TriggerTodoWriteId`、`CoveredBeforeTodoWriteId`（option）。若保留命名包装，必须是本事实的等价投影，不得缺字段旁路。

### desired cutoff（非事实）

```text
desiredCutoff(Tk) = Before(T(k-1) tool-call)   // T1 无 prior → 无 TodoCheckpoint 替换
```

仅由 **Accepted** checkpoint 链纯推导（cadence TODO-006；commit 语义 TODO-009）；**不**需要 durable Requested 事实；Accepted **本身不**提交 PrefixEpoch。

### commit 时点

```text
下一真实 provider attempt seal / 绑定之前
→ 若 desired 严于 ActivePrefixEpoch
→ 物化 PrefixCoverage 可证明的 complete-turn Y prefix
→ 原子 append PrefixRebaseCommitted（EvidenceKind=TodoCheckpoint）
→ ActivePrefixEpoch 切换
→ 该 attempt 与全部 retry 使用新 epoch
```

禁止：先发新 prefix 后补 committed；Accepted 后立刻 committed；provider 成功才 commit。  
**provider 成败不回滚**已 seal 的 epoch；崩溃后 boot fold 按 Accepted 链重算 desired，并在下次 seal 前 commit。

### coverage 分型（与 COMPANION-003 同构；coverage TODO-008；rebase 消费 TODO-009）

| 世界 | coverage | 材料 | 用途 |
|------|----------|------|------|
| Process review / LWR | RecordCoverage | Y + canonical RawGap；frontier/request-range bounded | 评审证据（非 prefix 证明） |
| Manager lag-1 rebase | PrefixCoverage | CoverableRecordPrefix / proven Y only | X→Y prefix replacement |

禁止：LWR RawGap 进入 prefix replacement；用 RecordCoverage 推导可替换性；用 PrefixCoverage 填 LWR gap；session head LWR 冒充 bounded range；第二套 work-record / prefix renderer。

投影形状（TodoCheckpoint 生效后）：

```text
Opening（永久 raw，byte-stable）
+ proven Y prefix through TodoRebaseCutoff
+ cutoff 之后 raw X（含上一 checkpoint call/result 整段）
```

非 Magic 路径（无 Accepted TodoCheckpoint 链）保持 CTX-010/012 既有行为不变。

## CTX-016：WorkRecordStart Opening floor

删除 planning/Activation 业务 floor 后，Opening 保护改由结构性 cursor（不是 Stage）：

```text
ManagerLife.WorkRecordStart
  = Opening HumanRoot semantic range 的 exclusive end
  （由 LifeOpened / XTrace Opening 纯推导）

Blogger effectiveStart
  = max(RecordCoverage, Life.WorkRecordStart)
```

```text
Opening 永久 raw、byte-stable
Opening 不交给 Y 改写，不随 TodoCheckpoint rebase 消失
process-review LWR：includeOpening=false，不得再复制 Opening
```

禁止：Blogger 从 0/session head 起步吞掉 Opening；把 Opening floor 绑回 `WorkActivated`（TODO-001）。
