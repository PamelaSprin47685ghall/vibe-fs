# Journal — 目标实现

## 需求意图与范围（A2 需求意图）

### 1. 问题陈述
在系统崩溃、进程硬杀或硬件断电场景下，如果允许先修改内存状态再异步刷盘，或者在读取日志时静默跳过损坏行，会导致恢复后的状态机看见未落盘的“虚幻未来”或建立在破坏基础上的矛盾事实。Journal 子模块旨在提供基于 NDJSON 文件的 Commit-Only 追加日志、内容寻址（Content-Addressed）的 BlobRef 存储，以及两相 `Requested → Accepted` 外部副作用持久化协议。

### 2. 输入输出与规则边界
- **输入**：领域事件事实、外部副作用请求、大正文 Blob 字节流。
- **输出**：持久化的 NDJSON 事实行、`BlobRef` 写入收据、`Requested` / `Accepted` 事实时序。
- **核心边界与不变量**：
  1. 写盘优先于内存修改（PERSIST-002）：必须先成功追加 NDJSON 介质并确认刷盘，才能替换内存权威状态；写盘失败等同于命令未发生。
  2. 损坏截断与 Fail-Closed（PERSIST-004）：恢复时遇到坏行必须在损坏点直接截断，绝对禁止跳过坏行继续解析后续行（缺中间导致后续事实建在错基上）。
  3. 内容寻址 BlobRef（PERSIST-007）：大文本正文按 SHA-256 摘要存为只读 Blob，同字节产生同 BlobRef（写入幂等）；载荷绝对不可变，更新必须产出新 BlobRef。
  4. 两相副作用契约（PERSIST-009）：外部副作用走 `Requested → 执行 → Accepted` 结构；崩溃恢复时 `Requested` 未 `Accepted` 视作未发生，允许安全重试。

---

## PERSIST-007：Blob

超过阈值的正文存 blob；NDJSON 只存 digest/reference。  
顺序：先写 blob，再 append event。

`BlobRef` 与 `BlobDigest` 是核心领域引用类型，被 `BlogFrame.TextRef`（how/companion.md COMPANION-005）与 `PrefixSnapshot.FrozenRecordPrefixRef`（shape/companion.md COMPANION-009）等复用。唯一定义在 `Identity.fs`：

```fsharp
type BlobRef = private BlobRef of string      // 相对 blob 存储根的持久路径
type BlobDigest = private BlobDigest of string // 完整 SHA-256 十六进制，对规范序列化字节计算

type BlobWriteReceipt =
    { BlobRef: BlobRef
      BlobDigest: BlobDigest }
```

性质（存档侧，`RuntimePath` 下 blob 目录）：

1. 内容寻址：同一规范字节 → 同一 `BlobRef`；写 blob 是幂等的（同 digest 复用既有 payload）。
2. `BlobDigest` 对**规范序列化字节**（含 ContentType 编码约定）计算；读取时重算失配 → fail closed，不得按路径猜测对齐。
3. 顺序：先落磁盘与校验 digest，成功后才 append 引用该 blob 的 journal envelope（PERSIST-009 的 Accepted 侧）。写盘失败等同命令未发生。
4. 载荷不可变；全文重写以新 `BlobRef` 呈现，旧 blob 由回收策略清理，不原地涂改。

不得把 digest 当随机身份：`BlobId` 可作索引，身份永远是内容本身。

---

## PERSIST-009：Durable Effect

```text
Requested / Claimed
→ 幂等执行副作用
→ Accepted / Created / Published
```

| 效果 | Request | Accepted | Reconcile |
|------|---------|----------|-----------|
| Worktree | `WorktreeCreateRequested` | `WorktreeCreated` | `git worktree list` / Sweep |
| Publish | `PublishClaimed` | `Published` | ref/head（ORCH-007） |
| Prompt | （PROMPT-011） | PhysicalAccepted | PROMPT-011 at-most-one |
| Blogger | `BloggerRequestMaterialized` | Entry/SquashCommitted | ProviderRun receipt |

崩溃后：Requested 未 Accepted → 视为未发生，可重试；Accepted → 物理已完成；重复 Accepted 幂等；不得把 Accepted 折回 Requested。

### Session 创建例外

Host 在 `session.create` 返回前不分配 child SessionId → 不引入 `SessionCreateRequested`。  
accepted 证据 = 链接事实：`HandleLinked` / `CompanionBloggerLinked`。

---

## PERSIST-010：上下文恢复 fold

不满足任一条 → 拒绝 envelope，fail closed：

```text
OpeningPromptCaptured
  每 lifecycle 幂等、不可覆盖；text = 首条任务 prompt 原文

XTracePartAppended
  严格顺序 append-only；Cursor 单调；同 cursor 重复拒绝

BlogEntryCommitted
  PreviousIngestCursor = 当前；Next > Previous
  CoverableTurnCutoff 单调不减；TextDigest = blob
  attempt Completed 且 terminal valid
  （frame append 与 coverage 推进同一原子提交）

TerminalOutputCaptured
  每 lifecycle 幂等、不可覆盖

BlogSquashCommitted
  FrameEpoch +1；1 ≤ CoveredFrameCount ≤ 当前 frames
  不改变 IngestCursor / CoverableTurnCutoff / RecordCoverage

PrefixRebaseCommitted
  Epoch +1；attempt 含相同 ProbeId；Completed + terminal valid
  candidate cutoff digest 再验证

ContextReanchored
  Epoch +1；同一 ObservedCompactionMessageId 只接受一次
  Snapshot→None；PrefixCoverage 归零；RecordCoverage 与 Frames 保留
```

禁止引入：`PrefixProbeRolledBack`、`OverflowDetected`、`ContextNearLimit`、`SquashReason` 等——失败不分类（CTX-005），容量不观察（CTX-001）。  
失败的 X probe 不产生事实（CTX-010）。

Projection 只从 Journal fold 派生 Y 有效 frames，不读物理 Y transcript 当历史源。
