# degeneration-guard — WHAT

## DG-001: 问题与非目标

检测目标为流式输出中的低多样性循环（单 token 循环、短句反复等）。非目标包括：不估算上下文窗口、不按错误文本分类失败、不发明独立的恢复预算、不将 delta 内容拼装为领域事实，以及不按角色或语言动态调整阈值。

## DG-002: 传感器是 ARCH-002 的定点例外

LoopSensor 仅从 Host 流式事件中提取 assistant 文本与 reasoning 思考流的 delta 文本喂给 detector。传感器严禁向 Journal 写入业务事实，严禁从 delta 推断终态或权限，业务层仅观察强杀后的 reconcile 结局。

## DG-003: 判定指标

观测单位由 `gpt-tokenizer/o200k_base` 定义。对 token 流维护指数衰减的加权相异计数（weighted distinct count $D_t$）。当 $D_t \le \text{LOOP\_WEIGHTED\_DISTINCT\_THRESHOLD}$ 时单次越阈即判定为 LOOP，无迟滞期。

## DG-004: artifact-local 固定参数与仓库滴定

检测参数在构建期由仓库全量 strict UTF-8 语料滴定生成，参数仅存在于生成产物中，严禁写入 tracked 源码。生产 fresh detector 以分布均值 `NORMAL_WEIGHTED_DISTINCT` 作为无罪 prior。

## DG-005: O(1) 更新与有界内存

每个 token 仅查询并更新该 token 的最近出现步数，更新时间为 $O(1)$。状态仅保存有限词表中已见 token 的最近步数映射，内存严格有界，不随输出流长度增长。

## DG-006: detector 生命周期绑定单次 ProviderRun

每个 provider attempt 创建独立的全新 detector，严格绑定至该次 `ProviderRunIdentity`。Attempt 结束、强杀或 session 销毁时立即丢弃，严禁跨 attempt 复用。

## DG-007: 命中只停止当前物理 attempt

检测命中 LOOP 且该会话为具有 physical parent 的 managed sub-session 时，记录 `LoopKillArmed` 并调用 Host 接口中断当前物理 attempt，忽略后续 delta。严禁在 abort 返回前提前发送 continuation。

## DG-008: LoopKillArmed 是进程内局部事实

`LoopKillArmed` 为仅存在于当前进程内存的局部标志，不写入 Journal，崩溃重启后安全丢失。

## DG-009: 强杀桥接标准 recovery，不造第二状态机

强杀导致的 `TurnAborted` 在命中 `LoopKillArmed` 时清除标记，并直接桥接至 `FallbackController.recordConfirmedFailure`，作为标准 provider failure 推进游标并由恢复预算决定是否继续。

## DG-010: 作用域与豁免

仅对插件 Owned 且具有 physical parent 的 managed 会话（WorkSession、CompanionSession、BloggerSession）进行检测。非 Owned 会话、user-facing root、compaction 运行及非 managed 内部运行一律豁免。

## DG-011: continuation 是独立叶子

LoopKill 后的 continuation 采用固定的 instruction-only 格式（`runtime/loop-continue`），作为 `ProviderRetryAttempt` 的独立分支，不与普通 provider failure 混用。

## DG-012: detector 不是业务 truth / retry controller

检测器仅负责提前中止退化 attempt，严禁绕过 FallbackController 自行修改 Offset 或分发压缩请求。诊断字段仅允许白名单指标，严禁将完整循环文本输出至日志。
