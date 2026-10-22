# M22 库与宿主子批收据

当前状态、正式验收计数、源码交付和精确SHA的CI证据以[最终结果](final-result.json)为准；以下保留各子批的历史收据，不代证最终候选。用户已明确允许本次本机采用CI现有15分钟整轮总限时，单项5秒、有限物理期限、真实回收和源码默认预算保持。完整发行第六至八轮的原门禁失败仍保留于[施工记录](execution.md)，本文件不自动关闭既有GAP。

## 库首轮

两父合并提交 `c1aa9af87a61b0a37fc712daf1dc346fe1f7cbd5`，版本 0.66.1，55 个公开入口。首轮原始命令、日志摘要及包摘要见 [库收据](../../wanxiangli/evidence/upstream-merge-20261022/README.md)。其后 delivery/sandbox 修正已独立复验，并以最终提交 `cf54390da393ea92e71eb1a37243b7c77ab94f96` 普通推送自有 main：Node22 全套 1009 pass/0 fail/7 skip，新包 Node/Bun 消费通过。Bun 定向两项 post-await 硬期限既有失败不计通过；完整最终证据见 [追踪](../../wanxiangli/evidence/upstream-merge-20261022/sandbox-bun-followup.md)。

预算增量 `003995a` 的 Node22 全套为1015 pass/0 fail/7 skip，Bun预算为10 pass/0 fail，真实临时包安装后的Node22/Bun消费均退出0，包SHA256 `24953f9b4cb16dc15fda1c74f6a03ae166d6f59b4fc2b8f5e99e05e54aa438d4`。这些结果仅覆盖该精确源码，不覆盖主仓尚未运行的新调用/归属增量，也不抹去Bun既有硬期限失败。

## 主仓源码处置

| 簇 | 处置 | 本次已做的检查 | 尚待验收 |
| --- | --- | --- | --- |
| ModelCapacity | 本地 Model/Surface/Borrowing/Ledger/Queue 迁至 Execution/Routing，同一 Int64 owner 保留。上游 float 借用/队列/容量桥未接线，不能宣称职责已全部抽出。permit 保留 Int64 与 WeakMap provenance。 | 源码与原本地实现按 namespace 变化逐文件核对；EMR002/010 语法检查。 | 实际新编译产物的 routing 全套、严格 reconciliation 与数值/handle 反例。 |
| Process 验收迁移 | 六个冲突文件保住真实 native/custodian、强 End/StopRequested/Output/Fault、ENOSPC 与关闭反例；净接 bytesHeld、Unicode 连续流与结果封存后的 late-output 用例；无用 Terminal 夹具删除。 | generation 3 正式六文件通过，含真实 Node/Bun 物理进程；监督器回收通过。 | 最终候选完整发行。 |
| ProcessEventLog | 接 segments 的命名/分组/布局/完整 canonical decode 与 retention 惰性尾行判定；保留 partial-prefix copied-lines、读写一致判定、write/fsync/unlink 切点、二次冷恢复与磁盘 stat 缓存事实。跨 segment 尾行扫描及时关文件。 | generation 3 的 DE005 33 pass、DE006 27 pass，DC011 与 DE022 单元闭包通过；含 write/fsync/unlink 切点、10k segments 原 5 秒预算通过。 | DE022 真实 compiler integration 与最终完整发行；完整批次 I/O 竞争曾使 DE005 超时，不计通过。 |
| Git | 保留 TextProcessRunner 强错误通道；porcelain 只作纯解析。rebase 查询保留错误、空路径拒绝及 node:path.isAbsolute。生产 Git 专用通道复用 ProcessRunner/NodeProcessHost 的真实监督，把 stdout/stderr bytes 分路临时存储、正常终态后分别解码；不猜 mixed spool。 | generation 3 change-integration003 正式通过；含大 Unicode grep、真实 hook 超大 stderr 分路及临时文件清理反例。 | 最终候选完整发行。 |
| Omp/SecondHost | 机械接当前 HostPorts/ProductHooks 契约；缺 Config API 返回 Refused；None hooks 不补恒 Allow；真实资源、持久 opening 与现有工具流程保留。 | build15 Fable/772 ESM 通过；全仓 control-pyramid clean。 | 中立端口最终定向及完整发行；未运行真实 omp，不声称 Host smoke。 |
| GAP 台账 | 五块冲突保留本地新证据、归因与限制，确实已删除的五包仅归档承载；旧结果不作为本次通过证据。 | 当前树已无退休包跟踪文件；无冲突与 diff 检查。 | 随最终候选更新本次验收结果。 |

构建失败记录仍保留于 `/private/tmp/m22-main-build-2.log`；其中 Git 的 Fable 不支持 API 已改为 Node path API，Omp/SecondHost 类型修正已由后续构建确认。行为和失败过程见[施工记录](execution.md)；主仓提交、推送、精确SHA的CI与未部署状态分别见[最终结果](final-result.json)。
