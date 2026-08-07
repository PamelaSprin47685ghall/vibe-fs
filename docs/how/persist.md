# Journal — 目标实现

## Implements

行为合同见 `what/persist.md`；本文件只描述 append、blob、fold 和 durable effect 算法。

## Ownership

Journal、blob 与 projection 边界见 `shape/persist.md`。

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
2. `BlobDigest` 对写入 blob 的 UTF-8 内容字节计算；读取时按同一字节重算，失配 → fail closed，不得按路径猜测对齐。
3. 顺序：先落磁盘并取得 receipt，成功后才 append 引用该 blob 的 journal envelope。Blob 写失败时没有可引用的 receipt；Journal append 若为 `CommitUnknown`，按 PERSIST-003 fail closed，不能重试原命令。
4. 载荷不可变；全文重写以新 `BlobRef` 呈现，旧 blob 由回收策略清理，不原地涂改。

不得把 digest 当随机身份；`BlobRef` 的路径由完整 digest 确定，身份永远是内容本身。

---

## PERSIST-009：Durable Effect

```text
Requested / Claimed
→ 按确定性效果身份执行或核对
→ Accepted / Created / Published
```

| 效果 | Request | Accepted | Reconcile |
|------|---------|----------|-----------|
| Worktree | `WorktreeCreateRequested` | `WorktreeCreated` | `git worktree list` / Sweep |
| Publish | `PublishClaimed` | `Published` | ref/head（ORCH-007） |
| Prompt | （PROMPT-011） | PhysicalAccepted | PROMPT-011 at-most-one |
| Blogger | `BloggerRequestMaterialized` | Entry/SquashCommitted | ProviderRun receipt |

崩溃后：Requested 未 Accepted → **结局未知**。先执行表中 Reconcile；仅当物理证据证明效果不存在且该效果的合同允许幂等重试时才重试。Prompt 例外地保持 Pending，按 PROMPT-011 检索 `PromptKey`，不得自动重发。Accepted → 该领域合同已确认物理完成；重复 Accepted 幂等；不得把 Accepted 折回 Requested。

### Session 创建例外

Host 在 `session.create` 返回前不分配 child SessionId → 不引入 `SessionCreateRequested`。  
accepted 证据 = 链接事实：`HandleLinked` / `CompanionBloggerLinked`。

---

## 上下文恢复 fold 实现落点（不变量见 what/persist.md PERSIST-010）

拒绝条件（不变量）权威定义见 `what/persist.md` PERSIST-010——不满足任一条拒绝 envelope，fail closed。  
本处只留实现落点：恢复 fold 逐 fact 校验的代码在 `Journal/Fold.fs` 的恢复事实分支；物理 envelope 形状见 PERSIST-001/002。
