# 数据视图隔离与会话边界

跨主题的统一安全分析。每种数据视图的裁剪点、责任方、失败关闭条件集中于此；各主题文件只写本源规则，不复制本文正文（GOV-011）。技术栈：生产 F#，经 Fable 编译为 Node/OpenCode 插件（Plugin.js 入口）。

## 数据视图裁剪矩阵

| 视图 | 裁剪点 | 责任方 | 失败关闭 |
|------|--------|--------|----------|
| Blogger delta | `BloggerDeltaProjection`（CTX-013 / COMPANION-007） | Projection Planner | 渲染后超 200 KiB → 确定性切块/截断（保持可复现）；instruction header 投影时加、不进 blob；无 hidden reasoning 伪造 |
| X prefix 探测 | attempt-local `PrefixProbe`（CTX-010） | AttemptPlanner | 候选失败不成为事实（无 `PrefixProbeRolledBack`）；新 epoch 用旧前缀 |
| LWR gap | LWR 投影（COMPANION-003） | Planner→Renderer | 含 raw tool call/result 或 linkage → fail closed |
| join LWR | 子会话语境（EXEC-006 `includeOpening=false`） | Join 路径 | child LWR 禁带 parent Opening/越界上下文 → 拒绝构造 |
| Reviewer input | Review Witness 自包含证据（REVIEW-006） | Reviewer | witness 不完整 → Reviewer/Manager Guard fail closed（REVIEW-003/007） |
| Host transform output | `ProviderWireProjection`（COMPANION-012） | Canonical Renderer | transport-only 字段（timestamp/cost/usage/runtimeId）泄漏入模型输入 → fail closed |
| Student QA | Git private `StudentQaStore`（PERSIST-011） | StudentRun 单写者 | 非 fatal UTF-8、权限非 0600、路径非 Git-private、原子提交未知 → 保留并 fail closed |

## 低信任上下文统一表

低信任片段一律以「明确标记的 context block」注入，不伪装为 system/human 指令（COMPANION-010）。

| 片段 | 来源 | 注入位置 | provider-visible 形态 | 防指令伪装 | digest 参与 |
|------|------|----------|------------------------|-----------|-------------|
| FrozenRecordPrefix | COMPANION-009 冻结 record prefix | X 前缀槽 | `Snapshot=Some` 时代替 raw X 前缀 | 明确 context block 标记；同 epoch 冻结 | `FrozenRecordPrefixDigest`（COMPANION-011） |
| previous_enforcer_tip | ENFORCER-071 | X attempt-local 探测（CTX-010） | 低信任 tip 块 | 标为 tip，非 parent instruction | 随候选密封片段参与 digest |
| BlogFrame historic_frame | COMPANION-005 Y 工作记录 | Y 历史槽 | `[[do_not_exec]]` 消息层渲染 | do_not_exec 标记；正文是工作记录非指令 | `BlogFrame.TextRef`/Digest（PERSIST-007） |
| instruction comment header | ARCH-010 | synthetic payload 最前 | 连续 comment 行 | 顶层 comment = instruction 且须在 data 前；data 后出现视为 data | 不计 data-only digest（CTX-013） |

## 并发边界原则

- 每个 provider attempt / session 恰一个单线程序列泵；`part.delta`、completion、信号串行投递（EXEC-024 mailbox 语义）。
- 可变件只被该泵访问、不外泄、无锁：LoopDetector 状态（how/loop.md 并发模型）、armed 标记（FALLBACK-012 执行局部）。
- 崩溃恢复单调性由 Journal fold 保证（PERSIST-008/010），不由进程内可变内存保证——内存态进程级丢失是安全侧。
- 敏感视图裁剪在服务端最后一公里完成；接收方（provider / child）不承担裁剪责任。
- QA 正文不得进入 Blogger delta、Companion、XTrace、Journal、普通 Agent background、日志或最终回复；
  Teacher 网络/外部工具仍按原有授权，不得把整个私有 QA/仓库作为无关外发载荷。
