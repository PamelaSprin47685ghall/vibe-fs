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

## OpeningMaterial / WorkRecord 段所有权

行为：`what/companion.md` COMPANION-003/014/015；Opening 关闭政策：`GLORY-074`；Manager floor：`TODO-001/015`。  
本节只划唯一 owner；不复述四段文案。

| 关注点 | 唯一 owner | 边界 / 禁止 |
|------|------|------|
| XTrace（原始语义轨迹） | HOST-005 / XTrace capture | Companion 只读；Strength Candidate 永不入迹 |
| `OpeningPolicy` | GLORY-074（Immediate \| BlindPlan） | Companion 不另造阶段 PC；关闭边界一旦成立永不移动 |
| `OpeningBoundary` / `WorkRecordStart` | Life / XTrace Opening cursor **纯推导**（TODO-001） | 禁止 Stage fact；禁止绑回 `WorkActivated` |
| `OpeningMaterial` | preserved XTrace `[work start, OpeningBoundary)`（COMPANION-014） | **唯一** Opening 事实源；禁止 `OpeningPromptRaw` / AssignmentText / AuthoritativeRequirements 拼接重建 |
| BlindPlan constitutive Opening | T1 `todowrite` call + canonical accepted result（TODO-015） | `XTrace.forOpening` 保留；不得当 incidental tool 滤入 Recent work |
| WorkRecord 四段标题 | COMPANION-003：`Opening` / `Chronicle` / `Recent work` / `Closing report` | 旧标题已删、无 alias；Closing = prose claim，无 universal fixed schema |
| Chronicle | 有效 Y frames（BlogEntryCommitted） | squash 只处理本 X frames（COMPANION-006） |
| Recent work | RawGapFromX（未覆盖 suffix）经 LWR 投影 | ≠ receiver-relative recentness；LWR gap **剔除** raw tool |
| Closing report | `TerminalOutputRaw` | 不复经 transform；不经 Y |
| LWR 物化 | `LifecycleWorkRecordProjection`（既有 range API） | process/Finality：`includeOpening=false`；禁止第二套 work-record renderer（TODO-008） |
| `includeOpening` 投影策略 | COMPANION-015：父→子 true；子→父 false；同 Session frozen prefix true | Canonical record **保留** Opening，即使投影省略 |
| Opening → Y | **禁止** | Opening always raw：never Blogger / never prefix-replaced；survives compaction / reanchor / recovery |

```text
Opening closes → WorkRecordStart
after Opening → ordinary Chronicle / Recent / Y machinery
```

禁止平行 owner：`OpeningPromptRaw` blob、Birth/Activation 记录冒充 Opening、第二套 LWR/process-evidence 投影、用 TodoCheckpoint rebase 擦除 Opening。
