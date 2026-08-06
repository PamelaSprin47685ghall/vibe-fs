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

Epoch 切换仅两源，且必须 `EpochId+=1`：

1. 成功 prefix probe 提升（CTX-010/012）  
2. Host compaction 重锚 → `Snapshot=None`（HOST-006）  

平常回合：FrozenRecordPrefix 不变，Y frames 可增长，X 前缀字节不变。  
禁止按容量/token 主动切换 epoch。

## Coverage 类型边界

| 类型 | 谁写 | 谁读 |
|------|------|------|
| RecordCoverage | 仅 BlogEntryCommitted | LWR gap、ingest |
| PrefixCoverage | probe 成功 / reanchor 归零 | prefix replacement 证明 |

混用 = 用更窄 B 替换更宽摘要，或用半 turn 做 replacement。
