# Persist — 可观察行为

条款前缀：`PERSIST-`。  
边界与所有权见 `shape/persist.md`。  
CAS / Git raw / adapter 算法见 `how/persist.md`。

统一 durable substrate 是 EventStore：事实 = event；修改 = append；查询 = projection；大正文 = Git raw payload；原子发布 = `refs/wanxiang/store` CAS。

## PERSIST-001：EventEnvelope

每个 durable event 必须是版本无关的 `EventEnvelope`，至少含：

```text
event_id
stream_id
event_type
parents          // 直接前驱 EventId 集合；可空 = stream 根
payload          // canonical JSON 对象
payload_refs     // opaque PayloadRef 集合；可空
```

禁止 envelope / store 携带 `schemaVersion` / `storageVersion` / `journalVersion` / `formatVersion` / `generationVersion`。  
同一 `event_type` 的 payload shape（字段名、含义、必填性）一经 committed 即冻结；新语义必须新 `event_type`（additive vocabulary）。

Canonical JSON 是 identity 协议：UTF-8、无 BOM、恰好一个 LF 结尾；object key 按 Unicode codepoint 升序；`parents` / `payload_refs` 先去重再按 canonical 文本序排序。同 `event_id` + 不同 canonical bytes → identity collision，fail closed。

## PERSIST-002：Append / Publish 原子性

`Append` / `Publish` 以 canonical ref 的 CAS 为唯一提交原语：

```text
CAS(refs/wanxiang/store, expected = Absent | R0, new = R1)
```

成功 → 新 `StoreSnapshot`（Committed）。  
不存在「部分写入」的权威历史：one event = one immutable Git blob；半条 NDJSON 不得进入 canonical root。

CAS 冲突 → 重新观察 root → 若 EventId 已在 store 中则视为已 Committed，否则基于新 snapshot 重建并 bounded retry。  
禁止独立 `CreateRef` / 第二套首次 bootstrap 协议。

## PERSIST-003：提交结局与 fail-closed

提交结局的 durable witness 是 canonical root（是否已包含该 `event_id`），不得用「再请求一次模型」或内存猜测代替。

以下必须 fail closed，进入显式恢复 / 人工处置，不得跳过坏 event 继续 fold：

- `StorageInvalid`（坏 JSON、非 canonical、identity collision、缺 parent / 成环、payload 缺失或 hash 失配、unknown authoritative `event_type`、必填字段错误）
- Append/Publish CAS retry 耗尽且 EventId 仍不在 store

`DomainConflict`（合法并发 fork）**不是** `StorageInvalid`：history 保留 competing heads，由 projection 表达冲突态，经以全部 heads 为 `parents` 的 resolution event 收敛。

## PERSIST-004：损坏与拒绝加载

权威 store 中任一 event 无法通过 canonical / identity / causal / payload closure 校验 → 拒绝以该 snapshot 构建投影或启动依赖它的 runtime 路径。  
禁止「跳过中间坏对象继续」；禁止把 DomainConflict 升级为全局 corruption。

旧 RuntimePath NDJSON / 目录 blob **不是**权威历史：不得为截断半行而打开它们（见 PERSIST-005 leave-unread）。

## PERSIST-005：无 schema 版本；leave-unread clean-break

Store **不**维护 schema / store / migration generation。  
旧 Journal NDJSON、RuntimePath `blobs/`、Student QA 私有文件、feature-owned ref：

```text
不要求可读
不要求可迁
不进入新 active domain projection
不作为 EventStore ongoing vocabulary
不要求 LegacyProjection ≡ NewProjection
runtime 永不打开（leave-unread）；允许丢弃或原地留存
```

禁止 dual-write、legacy reader / importer、fallback-to-old-store shim。

## PERSIST-008：Projection 查询

Projection 查询不得扫描完整历史。  
必须 O(1) 积分状态回答当前 epoch、frames、coverage、XTrace 锚点、effect 窗口等。  
Projection 不是第二真相源：禁止先改投影再补 event。

## PERSIST-010：上下文恢复 fold

恢复 fold 对以下事实的不变量**不满足任一条 → 拒绝 envelope，fail closed**。  
本条是 fold 不变量所有者；Magic Todo 的 cadence / desired cutoff / settlement 语义只交叉引用 TODO-*，不在此复制。

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
  Epoch +1；candidate cutoff digest 再验证；Y bundle 必须 PrefixCoverage-complete-turn
  EvidenceKind = Probe | TodoCheckpoint（单一 ActivePrefixEpoch 合同，TODO-009 / COMPANION-009）
  · Probe：attempt 含相同 ProbeId；Completed + terminal valid（既有 probe 不变量不削弱）
  · TodoCheckpoint：TriggerTodoWriteId 等字段齐备（CTX-015）；commit 在 attempt seal 前
    不要求 provider Completed；不因后续 Failed/Aborted 删除或回滚本事实
  禁止：缺字段旁路；用 RecordCoverage / RawGap 证明 replacement（TODO-008/009）；平行 todo-only epoch（TODO-009/012）

ContextReanchored
  Epoch +1；同一 ObservedCompactionMessageId 只接受一次
  Snapshot→None；PrefixCoverage 归零；RecordCoverage 与 Frames 保留
  TipDelivery.FullDeliveredTips 清空（与 Blog/Prefix 同原子 session 更新；重锚后 resolve 再发 Full main.md）
```

**Durable Todo 事实（合法 vocabulary，不削弱上表 probe/coverage 不变量）**：  
Magic Todo 域的 Journal facts（如 `TodoWriteAccepted`、`TodoReviewConcluded`、settlement 相关事实等，权威语义 TODO-001..014）经同一 EventStore 提交时，fold **必须**接受其已 committed 的 additive `event_type`，并派生 canonical Todo 投影（TODO-007）。  
它们：

```text
不推进 RecordCoverage / PrefixCoverage
不单独切换 PrefixEpoch（epoch 仍只经 PrefixRebaseCommitted / ContextReanchored）
不把 Host TodoTable 当作 canonical 恢复源（TODO-007）
不得发明 PrefixProbeRolledBack 或「provider 失败回滚 epoch」类事实
```

**Durable Strength 事实（合法 vocabulary，不削弱上表 probe/coverage 不变量）**：  
`StrengthCandidatePrepared` / `StrengthCandidatePromoted` / `StrengthFramesTraced` / `StrengthCandidateAbandoned`（STRENGTH-006/007/008/017）经同一 EventStore 提交；大 material 只经 envelope `payload_refs`。  
它们：

```text
不推进 RecordCoverage / PrefixCoverage
不单独切换 PrefixEpoch
不得发明 Strength Journal NDJSON / RuntimePath blob / feature-owned ref
Prepared ≠ XTrace 历史；仅 Promoted 后的 Traced range 关联现有 XTraceCursor
```

禁止引入：`PrefixProbeRolledBack`、`OverflowDetected`、`ContextNearLimit`、`SquashReason` 等——失败不分类（CTX-005），容量不观察（CTX-001）。  
失败的 X probe 不产生事实（CTX-010）。  
Projection 只从 committed events fold 派生 Y 有效 frames，不读物理 Y transcript 当历史源。

## PERSIST-011：（空缺）Student QA 权威文件 — G3 已删除

**编号永久空缺（retired / absent）。**  
`StudentQaStore` / 私有 `QA.md` filesystem backend 与 Student QA 知识权威文件合同已删除（G3 clean-break）。  
不得迁入 EventStore，不得发明后继 QA event vocabulary，不得 dual-write / legacy reader。  
证明义务见 `scripts/checks/student-teacher-absence.mjs` 与 `unified-store-gate` 的 `student-qa-revival` / `no-migrator` 扫描。
