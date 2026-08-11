# 上下文恢复 — 目标实现

## Implements

行为合同见 `what/context.md`；本文件只描述 probe、squash、delta 与恢复槽算法。

## Ownership

恢复数据和端口边界见 `shape/context.md`。

---

## attempt-local prefix probe 机制（行为见 what/context.md CTX-010）

行为（不立即改 ActivePrefixEpoch、成功提升/失败丢弃、禁止先提交再回滚、A′ 失败不禁止 B′ 重试）权威定义见 `what/context.md` CTX-010。

本处只留机制：候选进不可变 `AttemptExecutionProfile.ProjectionChoice`；投影形状为 system + 低信任 companion memory + cutoff 后 raw X + 当前 physical user（最后）。

---

## CTX-011：候选选择

- 候选必须严格新于已提交 epoch 的 coverage 证明。  
- 无候选 → 不构造空 probe，走正常主请求。  
- CoverableTurnCutoff 只前进；失配 CoveredPrefixDigest → fail closed（COMPANION-011）。

---

## CTX-012：提交语义

| 动作 | 成功 | 失败 |
|------|------|------|
| X probe | 提交 epoch（EvidenceKind=Probe）+ SealRoot 继承 | 无事实 |
| Y squash | BlogSquashCommitted，FrameEpoch+1 | 不改 frames/coverage |
| TodoCheckpoint | 见 CTX-015：seal 前原子 PrefixRebaseCommitted；**不**依 provider 结局 | 不得在 Accepted 后/ provider 失败路径“回滚”已 seal epoch |

squash 选择范围/级联：前半有效 frames；不混父 LWR。

---

## Blogger delta TOML 机制（行为见 what/context.md CTX-013）

行为（data-only 冻结、200 KiB 硬上限、decision-relevant reasoning、与 LWR gap 分投影）权威定义见 `what/context.md` CTX-013。

---

## TodoCheckpoint → ActivePrefixEpoch 机制（行为见 what/context.md CTX-015）

行为权威见 `what/context.md` CTX-015；cadence 见 TODO-006，coverage 见 TODO-008，rebase 见 TODO-009。本处只留算法。

### desired cutoff 推导（纯函数，无 Requested 事实）

```text
accepted = 本 ManagerLife 已 TodoWriteAccepted 的有序链
k = |accepted|
k ≤ 1 → 无 TodoCheckpoint desired 替换（T1 无 prior）
k ≥ 2 → desiredCutoff = Before(accepted[k-1] 的 tool-call)
         // 即 Before(T(k-1))；保留 T(k-1) call/result 整段为 raw X
```

仅读 Accepted；Prepared-only / 未 Accepted 不进入链。

### next-attempt seal 前 commit

```text
messages.transform / attempt admission（下一真实 provider attempt）:
  1. desired = derive from Accepted 链
  2. 若 desired 不严于 ActivePrefixEpoch.Snapshot.Cutoff → 沿用当前 epoch
  3. 否则 await Journal/Y，直到 PrefixCoverage 可覆盖 desired
  4. materialize CoverableRecordPrefix / proven Y only
     （禁止嵌入 LWR RawGap；YBundleRef 必须 PrefixCoverage-complete-turn）
  5. 在 attempt seal / 绑定之前原子 append PrefixRebaseCommitted
     EvidenceKind = TodoCheckpoint
     字段同 what/context.md CTX-015（含 TriggerTodoWriteId 等）
  6. ActivePrefixEpoch ← 新 epoch；本 attempt 与 retry 只读该 epoch
  7. 再 provider request
```

todowrite after：**只**使 desired 可推导，**不** commit PrefixEpoch，**不**强制等 Y materialize 完成。

### 失败与崩溃

```text
provider Failed/Aborted → 不回滚已 seal PrefixEpoch
crash before seal     → 不得声称 committed；重启后重算 desired，下次 seal 前再 commit
probe 路径            → 仍遵守 CTX-010（失败无事实）；与 TodoCheckpoint 共用 SSOT
```

### 投影拼装（与 COMPANION-009 同形）

```text
system
+ Opening raw（永不进 Y bundle）
+ FrozenRecordPrefix / proven Y（Snapshot=Some 时）
+ cutoff 后 raw X
+ 当前 physical user（最后）
```

WorkRecordStart 约束 Blogger effectiveStart（CTX-016），不在本算法里复制 Opening 进 Y。

### 非目标（机制层禁止）

```text
TodoCheckpointPrefixRebase 缺字段旁路事实
NeedRebase bool / RebaseRequested Stage
用 RecordCoverage 选 cutoff 或证明 replacement
用 session head LWR 当 Y bundle
seal 后因 provider 失败删除/回滚 PrefixRebaseCommitted
```

---

## WorkRecordStart 机制（行为见 what/context.md CTX-016）

行为权威见 `what/context.md` CTX-016；义务 TODO-001。

```text
WorkRecordStart = exclusive end of Opening HumanRoot on XTrace
                  （LifeOpened 时即可纯推导；无额外 Stage fact）

BloggerMain / delta ingest effectiveStart
  = max(RecordCoverage.IngestedThrough 游标, WorkRecordStart)

Y materialize for prefix / frames
  不得把 [..WorkRecordStart) 的 Opening 字节送进压缩或 FrozenRecordPrefix 的可改写段
```

Opening provider-visible 字节在同 Life 内保持可复现的稳定前缀根；TodoCheckpoint rebase 只替换 WorkRecordStart 之后、cutoff 之前的 proven Y 区段。
