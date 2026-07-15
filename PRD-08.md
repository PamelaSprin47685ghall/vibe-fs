# 问题 7：review nudge 原始任务

## 一、Flow 管线位置

本问题映射到管线上的 **`ReviewProjection` 从 Events fold 派生**。原始任务的 SSOT 是 `loop_activated` 事件 → `ReviewLoopFold.Active task` → `ReviewProjection`。构建 Nudge Snapshot 时调用 `activeTask`，派生 `reviewTask`，PromptContext 再输出。

## 二、当前数据已经存在但被丢弃

当前 review loop fold 会保存 Active task。`loop_activated` 事件包含 task，`ReviewLoopFold.Active task` 也能恢复它。review submission prompt 还已经使用 `original_task` front matter。

但数据在传到 nudge snapshot 时被丢掉：
* `NudgeSnapshotSource` 有 reviewLoop；
* `SessionSnapshot` 只有 todos、assistant text、work state、model 等；
* 没有 original task；
* `NudgeLoop` 最后只调用基于 todos 的 loop nudge prompt。

也就是说，这不是"任务没保存"，而是在 ReviewLoopFold → NudgeSnapshotSource → SessionSnapshot → loopNudgePromptFor 这条转换链中丢失。

当前 `loopNudgePromptFor` 只接收 todos，并不会携带原始任务。

## 三、修复方案

### 3.1 扩展 SessionSnapshot

当 review loop Active 时，snapshot 必须包含：
* originalTask
* reviewLoopId 或 reviewSessionId
* currentRound
* latestVerdict
* latestFeedback
* todos
* nudgeAnchor
* humanTurnId

### 3.2 不复制两个独立 task 状态

不建议同时维护 `reviewLoop = Active task` 和 `originalTask = Some task` 两个可变字段。

更安全的做法：task 继续只存于 `ReviewLoopFold.Active task`；`sessionSnapshotFromFold` 调用 `activeTask snap.reviewLoop`，将结果写入 `SessionSnapshot.reviewTask`；prompt 生成只读取这个值。

只有在确有性能或序列化需求时才增加派生字段，而且应在 fold 后计算，而不是作为独立可变状态维护。

### 3.3 建立统一 ReviewPromptContext

所有 review prompt 生成器只能从这个 context 构建。字段可以按场景选择是否显示，但以下不变量强制：
* review loop Active ⇒ originalTask 非空；
* review nudge ⇒ originalTask 必须输出；
* needs revision ⇒ originalTask 和 feedback 必须输出；
* double check ⇒ originalTask 必须输出；
* reviewer submission ⇒ originalTask 必须输出。

建议至少包含：original task、current round、latest feedback、affected files（若已提交过）、current todos、review loop ID、current human turn ID、prompt origin。

### 3.4 selectNudgePrompt

`selectNudgePrompt` 在处理 `NudgeLoop` 时调用新的 review 专用模板，输入完整的 review context，而不是只传 todos。

## 四、严禁使用 `task` 字段名

在 nudge prompt 渲染的 front-matter 中，键名决不能写成 `task`。

因为万象术在重启重放（Replay）时，其折叠逻辑（`inferReviewTaskFromTexts`）只要看到 `task` 字段，就会误判定为"用户重新发起了一次 With-Review 激活命令"。这会导致 review 状态和 version 发生严重错乱。

仓库已经明确区分：
* `task`：Worker With-Review 激活字段；
* `original_task`：Reviewer、double-check 和后续评审上下文使用。

Review nudge 应使用：

```yaml
original_task: <完整原始任务>
prompt_origin: review_nudge
review_loop_id: ...
review_round: ...
```

其中 `prompt_origin` 不应参与 loop 激活折叠。review nudge 不会被误识别为新的 loop activation。

## 五、跨 Compaction 的 Projection 权威路径

Compaction 后必须从持久 `ReviewProjection`（由 EventLog fold 重建）恢复 `original_task`。在构建 compaction continuation prompt 时，由该 Projection 重新注入 `original_task`、review loop identity、round 和必要 feedback；Projection 是跨 Compaction 的长期 SSOT，不能依赖压缩文本是否保留字段。

`experimental.session.compacting` 的 `output.context` 可以把 Projection 内容注入 summary 以提高可读性，但这不是身份恢复或正确性的替代路径。summary、anchor 和普通历史文本都不能取代 Projection reinjection。

在 `experimental.session.compacting` 阶段通过 `output.context` 注入当前 Projection 内容是可选的 summary 增强；真正用于 continuation 的物理重注入必须发生在 compaction 后的 continuation prompt 构建处。

## 六、Compaction whitelist

当前 compaction front-matter 白名单包括：task、verdict、double-check、squad_event。没有 `original_task`。

该白名单是旧版本兼容面；其没有 `original_task` 不得被当作 Projection 数据缺失，也不得驱动新逻辑复制 `task`。

白名单只允许作为迁移期兼容措施：旧版本若只能从 compaction front-matter 读取字段，可临时把 `original_task`、`prompt_origin` 和必要的 review loop identity 加入 whitelist，以完成历史数据迁移。白名单不是长期正确性路径，不能成为 Projection 的替代，也不得与 Projection 形成两个可变 SSOT。

迁移完成后，Projection reinjection 是唯一长期路径；新逻辑不得要求 whitelist 才能恢复 `original_task`，并应移除仅为迁移而保留的 whitelist 依赖。

## 七、Active loop 缺 task 时 fail closed

若 review loop 是 Active，但 originalTask 为空：
1. 不发送 review nudge；
2. 尝试从 event log 重新折叠；
3. 若仍无法恢复，记录 invariant violation；
4. 不允许退回无任务的普通 loop prose；
5. 不发送一个无任务 prompt 让 LLM 猜。

## 八、注意 task 与 original_task 的语义区分

当前系统里 `task` 往往用于激活 worker review mode；`original_task` 用于 reviewer 评审上下文。

不要为了补字段，简单把 `task` 到处复制，否则 review nudge 可能被误识别成一次新的 review 激活命令。

* 初次进入 With-Review Mode：使用 `task` 表示激活命令的任务。
* 后续 review nudge、double-check、reviewer continuation：统一使用 `original_task`。
* prompt origin 和 review loop ID 明确表明这是一条 continuation，而不是激活新 loop。

## 九、必测场景

* Active review，无 todos；
* Active review，有 todos；
* needs revision 后继续；
* WIP submit 后继续；
* compaction 发生在 loop activation 和 nudge 之间；
* 进程重启后恢复；
* original task 包含多行、冒号、引号、YAML 特殊字符；
* reviewer child session 收到 nudge，不应重新激活 worker loop；
* task 缺失时零 prompt；
* 多个 review round 始终保持同一 original task；
* review nudge 只包含 `original_task`，不重新生成激活字段 `task`；
* active loop 缺 task 时不发送；
| compaction 后仍从 EventLog 恢复。
