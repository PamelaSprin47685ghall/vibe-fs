# prefix-stability — HOW（实现模型与约束；非 normative）

## 1. 实现模型

### 1.1 前缀快照与候选（`Domain/PrefixCandidate.fs`）

```fsharp
type PrefixSnapshot =
    { FrozenRecordPrefixRef: BlobRef
      FrozenRecordPrefixDigest: BlobDigest
      CutoffExclusive: int
      CoveredPrefixDigest: string
      SealRoot: string
      SyntheticMessageId: string }

type PrefixProbe = { ProbeId: string; BasedOnEpochId: PrefixEpochId; Candidate: PrefixSnapshot }

[<RequireQualifiedAccess>]
type XProjectionChoice = UseCommittedEpoch | UsePrefixProbe of PrefixProbe
```

- `CutoffExclusive` 是 X provider-visible messages 的 index，只在产生 `CoveredPrefixDigest`
  的编号下有意义——两者同生共死（COMPANION-011）。
- 类型放 Domain 而非 Journal：`AttemptExecutionProfile` 携带候选（PROMPT-008）、fold 校验
  （PERSIST-010）、selector 构造（CTX-011）——一个类型三处共用，保证 profile 副本与
  committed 副本可比（CTX-012 要求 promoted snapshot 与成功请求用的 byte-identical）。
- `ProviderRequestKind.mayCarryProbe`：只有 WorkMain。

### 1.2 投影意图（`Domain/XPrefixProjection.fs`）

- `forSnapshot`：`None → KeepPhysicalPrefix`；`Some → ActivatePrefixEpoch
  { SyntheticMessageId; Memory; DropLeading = CutoffExclusive }`。
- `forChoice`：probe 与 committed 走同一函数（probe 不是另一种请求，CTX-012 要求
  promoted 与 sent byte-identical）。
- `requiredBlob`：probe 候选必须读**候选**的 blob，读 committed 的 blob 会把旧 prefix
  配到新 synthetic id 下——fold 检测不到，因为是两个各自合法的半套。

### 1.3 epoch 投影（`Context/Prefix/Epoch.fs`）

```fsharp
type ActivePrefixEpoch =
    { EpochId: PrefixEpochId
      Snapshot: PrefixSnapshot option
      ReanchoredRuns: Set<ProviderRunIdentity> }
```

- `Snapshot=None` 是两种历史的诚实合一：从未 promote 与 compaction 已退休（HOST-006）——
  两者行为相同（发 raw history），一个状态。
- `applyRebase`：校验 `previousEpoch = current`、`nextEpoch = successor`、cutoff 不后退、
  candidate 不是 identical（`CandidateNotNew`——不烧 epoch 换零变化）。
- `applyReanchor`：`ReanchoredRuns` 集合防同一 compaction 重锚两次。**epoch check 与
  ReanchoredRuns 防不同失败**：前者防 replay line（crash 在 append 与 fold 之间），
  后者防 repeated decision（同一观察被消费两次，epoch 已前进）。
- `sameCandidate` 排除 SealRoot/SyntheticMessageId（COMPANION-013 由前三字段派生，包含会
  循环比较）。

### 1.4 fold 接线（`Context/Companion/Blogger/ContextFactFold.fs`）

- `PrefixRebaseCommitted` → `tryUpdatePrefix` + `PrefixEpochProjection.applyRebase`。
- `ContextReanchored` → **一个** session 级更新原子做两件事：prefix 退休 +
  PrefixCoverage 归零（`BlogProjection.applyReanchor`）；Frames / RecordCoverage /
  XTrace 存活（COMPANION-008）。原子性结构性保证，不靠读者追踪两步。
- TodoCheckpoint（`PrefixRebaseCommittedV2`，`EvidenceKind=TodoCheckpoint`）经同一
  `tryUpdatePrefix` 路径——无第二 SSOT（CTX-015）。

### 1.5 权威判定（`Domain/ProviderProjection.isAppendOnlyPrefix`）

- 比较 `Tools`（相等非前缀）、`System`、`ProviderId`、`ModelId`、`Variant` 与完整 message
  前缀（`next.Messages |> List.truncate (length previous) = previous.Messages`）。
- 生产前置 proof（`Context/Prefix/XWire.fs`）与回归测试共用同一函数
  （cache.md §11：`assertPrefix` fail fast）。

### 1.6 TodoCheckpoint（`Domain/MagicTodoPrefixEpoch.fs`）

- 输入只接受 obligation-ledger 投影出的 committed Accepted 子链：Pre-T1 `planComplete=false` checkpoints
  不进入 prefix rebase。`desiredCutoff(T1)=None`；后续 committed checkpoint 返回 previous committed Accepted；
  `requiresLag1Rebase` 在 committed 子链长度 ≥2 时为真。
- `buildTodoCheckpointCommit`：与 probe 共用 `PrefixRebaseCommittedV2` 形状，
  `EvidenceKind = TodoCheckpoint(Tk, coveredBefore)`；`SolvingProviderRun = None`
  （seal 前提交，provider 结局无关）。

## 2. 与相邻包的分工

| 机制 | owner |
|---|---|
| candidate 何时有资格（CTX-011） | context-compression |
| 压缩结果如何渲染（TOML / wire） | provider-projection |
| 何时观察到 compaction（containment 决策） | context-compression（HOST-006 收容层） |
| desired cutoff 的 committed Accepted 子链（从首次 accepted planComplete=true 起） | obligation-ledger |
| system prompt / Persona 内容 | participant-identity / provider-language |

## 3. 已知非目标（HOW 层）

- `ReanchoredRuns` 集合的持久化（compaction 消息永远留在 transcript，epoch check 不够）是
  实现机制；「同 compaction 只重锚一次」才是命题（PREFIX-STABILITY-006）。
- ordinary synthetic `skill({ name: "" })` + `<skill_content name="">…</skill_content>` / Cursor `NUL+BOM+MarkerText`
  是 HOST-013 的 wire HOW（card 明确可整体替换）；新 occurrence 的 MarkerText 已是最终 skill-content payload。
- `CoverableFrameCount`（vs 存储 CoverableBRef）是等价压缩 HOW（context-compression 侧）。

## 4. 历史与弃权

### 4.1 源 → 覆盖映射

| 源 | 信息落点 |
|---|---|
| 历史 HOST-005/006/013 | WHAT-001/002/006/010/011/014；WHY §4.1/4.3 |
| 历史 why/host（决策 9–13） | WHY §4.3；WHAT-010/011 考古 |
| 历史 COMPANION-009/010/011/013 | WHAT-002/008/009/015 |
| 历史 shape/companion（COMPANION-009 表） | WHAT-002/004；HOW §1.3 |
| 历史 CTX-010/011/012/015 | WHAT-002/003/004/005/009 |
| 历史 why/context（ActivePrefixEpoch 理由） | WHY §4.4；WHAT-004/005 |
| 历史 PROMPT-014 | WHAT-007 |
| 历史 TODO-009 | WHAT-004/005 |
| 历史 ARCH-004 | WHAT-001 |
| 历史 change（cache） | WHY §4.1；WHAT-001/010/011/013 |
| 历史 change（cursor-pair-hint）§12 | WHY §4.2；WHAT-013 边界 |
| 历史 change（pair-parallel-tools） | 只取 prefix 相关：placement 不破坏 bracket；正文 craft 归 cognitive-environment |
| 历史 requirements-design card（13-context-continuity） | 全部 OWNS/DOES NOT OWN 裁决 |
| 历史 COVERAGE（PROMPT-014/HOST-013/HOST-006/COMPANION-009..013/CTX-010..015 行） | WHAT 命题归属 |

### 4.2 弃权（GARBAGE / 明确不归本包）

- **Pair Hint 正文（简体中文思考纪律、parallel wave craft）**：属 `cognitive-environment`
  （CHANGES-AUDIT：pair-parallel-tools → cognitive-environment）。本包只拥有「若属于 prefix
  identity 则稳定」。
- **elapsed 采样（`SessionStartedAt → now`）**：HOST-013 的 wall-clock 计量归 `time-capability` TIME-007；
  本包只拥有「历史 marker 永不重算 elapsed」（PREFIX-STABILITY-011 边界）。
- **`PairProgrammingGuidelineAppended` legacy 无 anchor 事实**：是 migration sediment
  （fail closed 不迁移）；本包以 WHAT-010 的 fail-closed 表述，不立「如何迁移」命题。
- **`NeedRebase` / `RebaseRequested` Stage**：被拒方案（TODO-009/012 GARBAGE）；本包
  以「唯一 SSOT」表述（WHAT-004）。
- **按容量切 epoch**：被拒方案（COMPANION-009 考古）；本包以「三证据源」表述（WHAT-002）。

## 5. 依赖理由（DEPENDS ON）

- `provider-projection`：prefix 是 projection 产物；本包只拥有稳定性合同，不拥有意图/表示。
- `context-compression`：候选的资格判定（CTX-011）是替换前提。
- `provider-language` / `participant-identity`：identity/language 材料若进入 prefix identity
  必须稳定；内容本身归它们（INDEX.md 骨架四边）。
