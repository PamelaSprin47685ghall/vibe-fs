# 问题 5：compaction 与 nudge owner 仲裁

## 一、Flow 管线位置

本问题映射到管线上的两处：
1. **`scan` 内 terminalOrigin 分类**：只有 `HumanTurnCompleted` 才允许普通 nudge，其他终止来源跳过。
2. **`stateFlow |> map plan` 中的 lease demand 预览**：CompactionEpisode 活跃期间 NudgeBlockReason = CompactionActive；可变 owner 只在 step 的事务中由 canonical `ContinuationLease` 仲裁。

## 二、当前根因

当前 nudge 主要根据 session idle/error/status idle 判断"自然停止"。但 session idle 可能来自：真人回答结束、compaction 结束、title 生成结束、fallback continuation 结束、recovery 尝试结束、nudge 自己结束、tool subturn 结束。

这些事件被压扁成了同一个 `isNaturalStop`。

虽然代码会跳过 synthetic assistant agent（如 compaction、title），但其做法往往是继续往前寻找上一条非 synthetic assistant。这样 compaction 刚完成时，系统可能拿到压缩前那条旧 assistant 消息，误以为它刚刚自然结束，然后发 nudge。

另外，fallback 的 `Consumed` 只反映某一瞬间状态机是否消费了事件。fallback transition 一旦把 phase 改成 Idle，后面的 nudge 观察者可能看到 `Consumed=false`，误以为事件没人处理。

当前 nudge snapshot 已经有 work state、block status、anchor 和 dedup，但没有"终止事件来源"和"当前 continuation owner"这两个关键维度。

## 三、OpenCode v1.17.13 compaction 协议

目标版本具有：
* `experimental.session.compacting`
* `experimental.compaction.autocontinue`
* `session.compacted`
* `assistant.summary = true`
* `part.synthetic = true`

OpenCode compaction 默认会执行 auto-continue。它会直接创建 synthetic user message，不经过 `chat.message` Hook，并最终产生普通 assistant 过程和 idle。这正是"压缩后错误触发 nudge"的官方协议根源。

## 四、引入 TerminalEventOrigin

Nudge 不再只接收 `naturalStop=true/false`。它应接收明确分类：

* HumanTurnCompleted
* HumanTurnAborted
* CompactionSummaryCompleted
* CompactionContinuationCompleted
* FallbackContinuationCompleted
* TitleCompleted
* NudgeCompleted
* ToolSubturnCompleted
* Unknown

默认只有 `HumanTurnCompleted` 可以进入普通 todo/review nudge 判定。`Unknown` 应保守跳过，而不是积极 nudge。

terminalOrigin 在 `scan` 的 Step 中携带，纯 `deriveAction` 继续只消费结构化 snapshot，不负责扫描 Host 原始消息。

## 五、continuation owner 仲裁

### 5.1 SessionGateDemand 统一优先级

参见 PRD-01。每个 session 同时只能有一个能发送 synthetic prompt 的 owner，且 owner 的唯一权威表示是 `ContinuationLease.owner` 及其 lease 状态。不得再维护独立的 fallback owner、nudge owner 或 current continuation owner 状态并让它们互相仲裁。owner 必须通过对应 lease 的 settle/cancel/fail 事件释放，不能只看 session 当前是否 idle；已开始 dispatch 的取消必须进入 `DispatchUnknown`/`Reconciling`，在事实收敛前不得释放给另一 owner。

### 5.2 NudgeBlockReason

纯 `deriveAction` 不反向依赖 Shell 可变 runtime。Shell/host adapter 读取 fallback observation 后转换为 `NudgeBlockReason`，构造 snapshot，传入纯函数。

建议从二值状态扩展为原因枚举：Allowed、UserCancelled、FallbackActive、CompactionActive、SyntheticTurn、PendingDelivery、RunnerOwnsTurn、DuplicateAnchor、UnknownTerminalOrigin。`NudgeBlockReason` 只表达 snapshot 中的可观察门禁，不拥有或改变 lease；owner 决定只能在 canonical lease 事务中发生。

## 六、CompactionEpisode 生命周期

### 6.1 建立状态

```text
CompactionEpisode
- episodeID
- sessionID
- contextGenerationBefore
- state
- autoContinueEnabled
- summaryMessageID
- continueMessageID
- startedAt
```

状态至少包括：Compacting、SummaryProduced、AutoContinuePlanned、Compacted、ContinuationObserved、ContinuationRunning、Settled、Cancelled、Failed。

### 6.2 开始信号

`experimental.session.compacting` 是最可靠的开始信号。在此 Hook 中：
* 创建 compaction episode；
* 将 NudgeBlockReason 设为 CompactionActive；
* 保存旧 context generation；
* 可向 `output.context` 注入 review task 等关键上下文。

### 6.3 auto-continue 信号

`experimental.compaction.autocontinue` 中记录 `AutoContinuePlanned`，读取默认 `enabled`，通常保持 enabled=true，不要再由 fallback 或 nudge 重复创建 continuation。

除非产品明确要求完全接管 compaction continuation，否则不应关闭官方 auto-continue。

### 6.4 识别 Compaction summary

稳定信号优先 `assistant.summary = true`。`agent="compaction"`、`mode="compaction"` 可以作为兼容辅助，但不是最终协议。

### 6.5 识别 synthetic continue

目标版本中 synthetic 标记位于 part，`part.synthetic=true` 是稳定字段，`part.metadata.compaction_continue=true` 是内部字段。该消息不经过 `chat.message`，会通过普通 message.updated 被观察到。

近期识别顺序：当前存在 CompactionEpisode → summary 已产生 → 随后出现 synthetic user part → metadata 可用于确认但不是唯一依据。

### 6.6 session.compacted

`session.compacted` 可以确认压缩处理已完成，但不能单独证明 auto-continue 已 settle、下一轮 assistant 已完成、现在可以发普通 nudge。因此从 Compacted 到 Settled 期间仍然阻止 todo/review nudge。

### 6.7 何时解除阻塞

若 auto-continue enabled：
* 观察 synthetic continuation；
* 观察其对应 assistant；
* 等该 assistant 成为 terminal；
* 确认没有 Abort；
* 才将 episode 设为 Settled。

若 auto-continue disabled：
* 在 `session.compacted` 后；
* 确认 Host idle；
* 确认没有 pending continuation；
* 再解除阻塞。

禁止使用固定 100ms、500ms 或"一秒窗口"作为正确性条件。

### 6.8 compaction 完成后

* 增加 context generation；
* 清除旧 assistant anchor；
* 清除基于压缩前消息生成的 pending nudge；
* 不触发普通 nudge；
* 等下一次真正人类轮次完成后再重新判断。

不能通过"忽略 compaction agent，然后使用前一条 assistant"来补偿。

## 七、fallback auto-continue 的处理

fallback continuation 应从开始到 settle 一直拥有该终止事件。流程：
1. fallback 请求 continuation；
2. 取得 owner；
3. continuation busy/idle/error 全部归 fallback episode；
4. fallback 判断是否继续下一个模型或结束；
5. 发出 `fallback_episode_settled`；
6. 释放 owner。

普通 nudge 不能在第 3 步看到某个 idle 就抢跑。

规范行为只有一种：`fallback_episode_settled` 写入后，该 fallback episode 不再发出任何 todo/review nudge，也不因 backlog 重新争抢 lease。backlog 只能在下一次真正的 `HumanTurnCompleted` 终止事件上重新进入 nudge 判定；settle 后的 idle、error 或 stale continuation 事件不得制造补偿 nudge。

## 八、Fallback/Compaction 状态不能侵入纯推导核心

不应让 `Kernel.NudgeDerivation.deriveAction` 直接依赖 FallbackRuntime。

`deriveAction` 目前是纯函数：输入 snapshot，输出 `NudgeAction`。直接把可变 runtime 注入内核会造成：Kernel 反向依赖 Shell、单元测试变复杂、host-specific 状态进入纯领域层、重放结果可能依赖当前内存状态。

更合理的方式：Shell/host adapter 读取 fallback observation，将其转换成 `NudgeBlockReason`，构造 snapshot，`deriveAction` 继续只根据 snapshot 判定。

## 九、Anchor 只能辅助迁移

仓库已定义 `compactionAnchorBody`、`hasCompactionAnchorPrompt`、compaction front-matter 提取和重建机制。

近期补丁中利用 anchor 辅助识别 compaction continuation 具有较低实施成本。但 anchor 有以下局限：文案未来可能改变、用户可能自己输入相同文本、compaction prompt 可能被截断、front matter 可能被重新编码、不同 Host 使用不同 wording。

最终系统不能根据一句英文文本决定 continuation 身份。Anchor 的正确地位是：旧历史兼容、调试、迁移期补充证据。最终身份仍来自 provenance 和 event projection。

近期补丁应在 Host adapter 收集 snapshot 时识别最后终止事件来源、最近 synthetic prompt origin、compaction anchor 是否存在、compaction 是否刚完成、是否正在 auto-continue，然后转换成 `NudgeBlockReason.CompactionActive` 或类似状态。纯 `deriveAction` 仍只消费结构化 snapshot。

## 十、Phase 条件修正

不能写成含糊的"Phase 不等于 Idle 或 Exhausted"。应明确：
* Phase 属于 Retrying/Scanning/ScanningToolCallText/RecoveringToolCallText：阻止；
* Phase 为 Idle：仍需检查 owner、gates 和 terminal origin；
* Phase 为 Exhausted：必须等 fallback episode 明确 settled 后再决定；
* Lifecycle 为 Cancelled/TaskComplete：直接阻止 nudge。

会话开始时（尚未观察到首个有效 `HumanTurnCompleted`）禁止发送 emergency todo prompt，也不得以 todo backlog、初始 idle 或恢复事件绕过该时间门槛。首个对话启动只建立 projection/lease demand；只有真正人类轮次完成且没有更高优先级 lease/block 时，才可进行一次普通 nudge 判定。

## 十一、必测事件序列

* human answer → normal idle：应允许一次 nudge。
* human answer → compaction started → compaction idle：零 nudge。
* title generation idle：零 nudge。
* fallback send → busy → idle → next fallback：零普通 nudge。
* fallback settle：本 episode 零重复 nudge。
* fallback settle 后 backlog 仍在：直到下一次 `HumanTurnCompleted` 前零 nudge。
* nudge 自己的 prompt 完成：不能递归 nudge。
* conversation start（没有 `HumanTurnCompleted`）：零 emergency todo prompt。
* Esc → idle：零 nudge。
* compaction 后旧 assistant 仍在历史：不能拿它作为新 anchor。
* session.error 被 fallback 消费后又出现 status idle：仍然只能有一个 owner。
* restart replay 时 owner 和 episode 恢复一致。
* `experimental.session.compacting` 打开 block；
* autocontinue planned 时普通 nudge 为零；
* summary assistant 不被视为真人完成；
* synthetic continue 不经过 chat.message 也能识别；
* `session.compacted` 不立即解除 block；
| auto-continue settle 后才解除；
| compaction Abort 时 cancellation 优先；
| 用户输入相同文案不被误判。
