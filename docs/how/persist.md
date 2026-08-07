# Journal — 目标实现

## 需求意图与范围（A2 需求意图）

### 1. 问题陈述
在系统崩溃、进程硬杀或硬件断电场景下，如果允许先修改内存状态再异步刷盘，或者在读取日志时静默跳过损坏行，会导致恢复后的投影看见未落盘的“虚幻未来”或建立在破坏基础上的矛盾事实。Journal 子模块提供 NDJSON append-only 日志、内容寻址 BlobRef 存储，以及两相 `Requested → Accepted` 外部副作用持久化协议。

### 2. 输入输出与规则边界
- **输入**：领域事件事实、外部副作用请求、大正文 Blob 字节流。
- **输出**：持久化的 NDJSON 事实行、`BlobRef` 写入收据、`Requested` / `Accepted` 事实时序。
- **核心边界与不变量**：
  1. 写盘优先于内存修改（PERSIST-002/003）：只有 `Committed` 后才能替换内存权威状态；`CommitUnknown` 必须进入 fail-closed reconcile，不能当作“命令未发生”重试。
  2. 尾部截断与 Fail-Closed（PERSIST-004）：只允许截断最后一条不完整 envelope；中间损坏必须拒绝启动，绝对禁止截断后把后续事实当作不存在。
  3. 内容寻址 BlobRef（PERSIST-007）：大文本正文按 SHA-256 摘要存为只读 Blob，同字节产生同 BlobRef（写入幂等）；载荷绝对不可变，更新必须产出新 BlobRef。
  4. 两相副作用契约（PERSIST-009）：外部副作用走 `Requested → 幂等执行/核对 → Accepted`；崩溃后仅有 Requested 表示结局未知，必须先按效果身份核对，不能假定未发生或盲目重试。

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
