# Companion — 所有权与边界

## 资格与关联

资格唯一来源：`ManagedSessionKind`（HOST-008），不是 Role 矩阵。  
写关联、复用 Blogger metadata 的入口在 Session 装配路径；禁止从 prompt transform 临时「发明」Y。

## COMPANION-009：PrefixEpoch 与 Seal Barrier

同一 PrefixEpoch 内：`request[n+1]` 历史前缀必须**逐字节**等于 `request[n]` 的 sealed prefix。

```fsharp
type PrefixSnapshot =
    { FrozenRecordPrefixRef: BlobRef
      FrozenRecordPrefixDigest: string
      CutoffExclusive: int
      CoveredPrefixDigest: string
      SealRoot: string
      SyntheticMessageId: string }

type ActivePrefixEpoch =
    { EpochId: int64
      Snapshot: PrefixSnapshot option }
```

初始：`EpochId=0`，`Snapshot=None`。

```text
Snapshot=None → system + 全部 raw X history
Snapshot=Some → system + frozen record prefix + cutoff 之后 raw X
```

原始 Host transcript **永不物理删除**（故 HOST-006 必须关 prune）。

Epoch 切换仅下列证据源，且必须 `EpochId+=1`（单一 `ActivePrefixEpoch` 合同；禁止第二 SSOT，TODO-009 / TODO-012）：

1. 成功 prefix probe 提升（CTX-010/012）— `PrefixRebaseCommitted`，`EvidenceKind=Probe`  
2. Host compaction 重锚 → `Snapshot=None`（HOST-006）— `ContextReanchored`  
3. TodoCheckpoint lag-1 rebase（TODO-009 / CTX-015）— 同一 `PrefixRebaseCommitted` 合同，`EvidenceKind=TodoCheckpoint`

第三条**不是**新 epoch 状态机：desired cutoff 仅由 Accepted Todo 链推导；**commit 发生在下一 provider attempt seal/绑定之前**；todowrite after **不**提交 epoch；provider Failed/Aborted **不**回滚已 seal epoch（TODO-009）。

平常回合：FrozenRecordPrefix 不变，Y frames 可增长，X 前缀字节不变。  
禁止按容量/token 主动切换 epoch。

## Coverage 类型边界

| 类型 | 谁写 | 谁读 |
|------|------|------|
| RecordCoverage | 仅 BlogEntryCommitted | LWR gap、ingest |
| PrefixCoverage | probe 成功 / reanchor 归零 / TodoCheckpoint 物化的 proven Y | prefix replacement 证明 |

混用 = 用更窄 B 替换更宽摘要，或用半 turn 做 replacement。  
TodoCheckpoint Y bundle **只**用 PrefixCoverage（禁止 RawGap）；不得用 RecordCoverage 证可替换前缀，也不得用 PrefixCoverage 填 LWR gap（与 COMPANION-003 同构；义务 TODO-008/009）。
