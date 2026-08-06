# Journal — 可观察行为

条款前缀：`PERSIST-`。  
路径与权限边界见 `shape/persist.md`。  
Blob、Durable Effect、上下文 fold 见 `how/persist.md`。

## PERSIST-001：Envelope

每个 journal envelope 必须含：schema version、event ID、stream ID。  
序列化时间戳必须 UTC offset 归一化——否则同一事实跨时区字节不同，指纹与重放失效。

## PERSIST-002：Append 原子性

Append 只有：`Committed` | `CommitUnknown`。  
不存在「部分写入」。

## PERSIST-003：CommitUnknown

出现 CommitUnknown → runtime 进入 fail-closed reconcile，需显式恢复。  
不得用「再请求一次模型」假装写入成功。

## PERSIST-004：尾部损坏

只允许截断恢复**最后一条**不完整 envelope。  
中间损坏 → 拒绝启动（不跳过后续行）。

## PERSIST-005：旧 Schema

Pre-0.5.0 journal 不猜测迁移。启动见旧 schema → 直接失败。

## PERSIST-008：Projection 查询

Projection 查询不得扫描完整历史。  
必须 O(1) 积分状态回答当前 epoch、frames、coverage、XTrace 锚点等。

## PERSIST-010：上下文恢复 fold

恢复 fold 对以下事实的不变量**不满足任一条 → 拒绝 envelope，fail closed**：

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
