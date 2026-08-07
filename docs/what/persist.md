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

## PERSIST-011：Student QA 权威文件

每个 Student Logical Run 恰有一个私有 `QA.md`；存在期间它是学习知识的唯一权威状态。Journal、Host
metadata、共享表和日志只能保存 Session/attempt/路径摘要等控制身份，禁止保存问题、回答、推测阶段、
置信度、知识分支或 QA 正文。

QA 是 UTF-8 自然语言字节流，只按真实发生顺序包含：用户原始请求、Student 问题、Teacher return。
框架只插入防粘连换行；不得添加标题、角色标签、分隔线、JSON/TOML 字段或旁路 Journal。完整尾部字节
等于待追加项时幂等去重；无法证明重复时宁可保留。

每次更新必须原子且 durable：旧完整文件或新完整文件，不得出现半段 UTF-8。用户请求先于 Student
provider effect；Student 问题先于 Teacher send；Teacher return 先于交付 Student。UTF-8 损坏、读取失败
或原子提交不确定均 fail closed，保留原文件，不跳过坏字节。

最终 return 与明确取消删除 QA 及空任务目录；删除失败不宣称完成。插件重启只按 Session/LogicalRun
确定路径，不解析正文；任务终态无法证明时保留，不自动删除。
