# 上下文恢复 — 边界

## 谁有权决策

- transform 内**不做**失败驱动恢复决策（看不到 attempt 结局）。  
- 恢复材料选择与提交只发生在 attempt 结局 reconcile 之后。  
- Fallback 提供 armed/primed（FALLBACK-012）；本域提供 hasMaterial 与动作选择。  
- **例外（非恢复槽）：** TodoCheckpoint desired cutoff 的 materialize + `PrefixRebaseCommitted` 提交发生在**下一** provider attempt 的 transform/seal 路径（CTX-015），仍由既有 PrefixEpoch 所有者写入，不把 commit 权交给 todowrite after。

## ActivePrefixEpoch 所有权

| 写入源 | EvidenceKind | 谁触发 | 谁写 ActivePrefixEpoch |
|--------|--------------|--------|------------------------|
| 成功 prefix probe 提升 | Probe | 恢复槽结局（CTX-010/012） | 既有 epoch 提升路径 |
| Host compaction 重锚 | （ContextReanchored） | HOST-006 | Snapshot=None，EpochId+1 |
| TodoCheckpoint lag-1 | TodoCheckpoint | 下一 attempt seal 前（CTX-015） | 同一 `PrefixRebaseCommitted` 合同 |

禁止本域或 Todo 域另造第二套 ActivePrefixEpoch / todo-only rebase SSOT（TODO-009、TODO-012）。
`session.compacted` 不得冒充 TodoCheckpoint。

## Coverage 读边界

| 类型 | 本域谁读 | 禁止 |
|------|----------|------|
| PrefixCoverage | prefix replacement / TodoCheckpoint Y bundle | 填 LWR gap；含 RawGap 的 bundle |
| RecordCoverage | 不在本域证明 prefix；LWR/评审见 companion + TODO-008 | 推导可替换前缀 |

WorkRecordStart（TODO-001 / CTX-016）是 Life 结构性 floor，不是本域 Stage；Blogger effectiveStart 消费它，prefix epoch 不把它写成第三 SSOT。

## CTX-007：按 RequestKind 分派结局

每个 attempt 三种结局来自 Outcome + isValidTerminal，不解析错误文本。  
动作按 `ProviderRequestKind`（PROMPT-008）区分：WorkMain / BloggerMain / BloggerSquash / InteractionRepair 各有固定后继（推进 cursor、写事实、发 continuation 等）——实现表见生产 `AttemptPlanner`，规范要求：**同种 RequestKind 同种结局必须同一分派**，禁止按错误字符串分叉。  
TodoCheckpoint epoch commit **不是** RequestKind 结局动作；它在 attempt seal 前完成，之后结局分派仍按本表（provider 失败不回滚 epoch，CTX-015）。

## CTX-008：恢复槽失败计数

恢复槽内失败仍走 Fallback 连续失败计数；维护子请求成功不得单独清零 count（FALLBACK-011）。  
不得为「压缩失败」另造第二套预算。  
TodoCheckpoint 路径不占用恢复槽，也不另造压缩预算。
