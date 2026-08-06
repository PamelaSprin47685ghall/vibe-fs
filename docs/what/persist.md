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
