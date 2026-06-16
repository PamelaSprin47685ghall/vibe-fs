为在 **Opencode** 端引入基于可见历史重写的“高级特征替换与历史截断”能力，我们将方案收敛为一套**单调分号、原始历史保留、可见历史投影、part 级裁剪**的实现。

本方案继续严格遵循“内核-外壳”分离原则：
* **外壳（Opencode 插件层）** 负责宿主 API 适配、分号、消息扫描、合成消息注入。
* **内核（纯函数层）** 负责块号编解码、可见历史折叠、turn 合并、裁剪投影等纯逻辑。

---

### 一、核心语义

#### 1. 块号（Block ID）与宿主线格式
1. 每一个**真实工具输出**都被视为一个块（block）。
2. 每个 session 的块号由 `0` 开始，**只增不减，绝不复用**。
3. 分号一旦成功发出，该号立刻视为已消费；**成功 / 失败 / 中止**都不回收。
4. 工具输出的宿主兼容编码统一如下：
   * 若输出是 **JSON object**，追加字段：`id_: N`
   * 若输出是 **string**，前置前缀：`#id_: N\n`
   * 若输出是 **非 object 标量**（如 `true` / `42`），先按字符串处理，再前置：`#id_: N\n`

#### 2. 可见历史重写语义
1. 宿主底层**原始历史永不物理删除**。
2. 所有裁剪都只发生在**提供给模型的可见历史投影**上。
3. `user` 消息**永不裁剪**。
4. `tool` 输出只是锚点；一旦触发重写，系统会：
   * 将锚点块的可见内容替换为统一字符串：`#id_: N\n<summary>`
   * 删除该锚点之后当前可见历史中的**所有非 user 文本 part**
   * 再把**当前 turn 自己的新输出**按规则追加回来
5. 裁剪边界是 **part 级**，不是 message 级。
6. 若某个非 `user` message 在投影后没有剩余 part，则该 message 会从可见历史中移除。

#### 3. 追加（Append-only）与合法锚点
1. 向下正常追加而不裁剪历史时：
   * `rewrite_anchor_id = 当前可见最大块号 + 1`
   * `rewrite_summary = ""`
2. 发生重写时：
   * `rewrite_anchor_id` 必须指向一个**当前仍可见**的块号
   * `rewrite_summary` 必须是**非空**高密度总结
3. 已经从可见历史中裁掉的旧块号，后续不得再次引用。

#### 4. 同一 assistant turn 的并行语义
同一 assistant turn 内若发起多个工具调用：
* 每个调用仍各自携带自己的 `rewrite_anchor_id` 和 `rewrite_summary`
* 最终可见历史折叠时，按同一 turn 的预分配块号集合将它们归为一组
* 该组的**有效锚点**取所有调用锚点的最小值
* 该组的**合并总结**按预分配块号升序拼接各调用 summary

写入前置规则消息中的原文如下：

> If you issue multiple tool calls in the same assistant turn, we will use the minimum anchor of all, and the ordered merge of all per-call summaries.

---

### 二、命名与数据约定

#### 1. 对 LLM 暴露的参数名
* `rewrite_anchor_id`
* `rewrite_summary`

#### 2. 仅供插件内部使用的隐藏字段
这些字段由 `tool.execute.before` 注入，但必须从 LLM 可见 schema 中剥离：
* `_block_id`: 当前调用预分配的块号
* `_turn_id`: 当前 assistant tool turn 的分组标识
* `_turn_slot`: 当前调用在本 turn 中的分配顺序

#### 3. 纯逻辑与组件名
* `SessionBlockSequencer`: per-session 原子分号器
* `ToolBlockCodec`: `id_` / `#id_:` 的解析与剥皮层
* `VisibleHistoryProjector`: 从完整原始消息流折叠出可见历史
* `HistoryRewritePrelude`: 面向所有 agent 的前置规则注入

#### 4. 合成消息 ID 前缀
* `rewrite-prelude-user-*`
* `rewrite-prelude-assistant-*`

它们和现有 `caps-synth-*` 一样都是**合成前置上下文**，但不参与块号扫描、锚点合法性判断、turn 分组与裁剪折叠。

---

### 三、详细设计

#### 1. `SessionBlockSequencer`：per-session 原子分号器
该组件是**唯一允许保留的瞬时内存状态**。它不保存任何历史语义，只保存分号运行所必需的最小状态。

每个 session 维护：
* `nextBlockId`: 下一个可分配块号
* `openTurnId`: 当前尚未关闭的 assistant tool turn id
* `nextTurnSlot`: 当前 open turn 内下一个分配顺序
* `lock`: per-session 原子锁

初始化规则：
1. 某个 session 第一次进入 `tool.execute.before` 时，读取**完整原始历史**。
2. 用 `ToolBlockCodec` 扫描历史中所有真实工具输出，取已出现的最大块号 `maxSeenId`。
3. 将 `nextBlockId` 初始化为 `maxSeenId + 1`。

分配规则：
1. `tool.execute.before` 获取 session 锁。
2. 若当前没有 open turn，则创建一个新的 `openTurnId`，并把 `nextTurnSlot` 置为 `0`。
3. 分配当前 `nextBlockId` 作为 `_block_id`。
4. 注入 `_turn_id = openTurnId` 与 `_turn_slot = nextTurnSlot`。
5. `nextBlockId <- nextBlockId + 1`，`nextTurnSlot <- nextTurnSlot + 1`。
6. 释放锁。

关闭规则：
* 在 `stream-end` / `stream-abort` 等 session 轮次结束事件到来后，关闭 `openTurnId`
* 下一次新的 tool burst 会重新开启一个新 turn

这保证：
* 并发 `before` 不会撞号
* 同一 turn 的多次工具调用天然拿到同一 `_turn_id`
* 同一 turn 内的顺序由 `_block_id` 与 `_turn_slot` 双重固定

#### 2. `ToolBlockCodec`：统一剥皮层
该层必须在内核中实现为纯函数，负责三件事：

1. **解析块号**
   * 从 object 输出中读取 `id_`
   * 从 string 前缀 `#id_: N\n` 中读取块号

2. **剥掉字符串前缀**
   * 将 `#id_: N\n<content>` 还原为 `<content>`
   * 仅在比较、去重、摘要拼接、投影判断时使用剥皮后的内容

3. **重新编码输出**
   * object 输出追加 `id_`
   * string / scalar 输出前置 `#id_: N\n`

这层会被三处共同使用：
* `tool.execute.after`：写回宿主可持久化的块号
* `VisibleHistoryProjector`：从完整原始历史读块号
* `read` 去重逻辑：在比较前先剥掉 `#id_:` 前缀

#### 3. 对 LLM 暴露的 schema
所有向 LLM 暴露的工具都追加两个**简短**参数：
* `rewrite_anchor_id`: Visible history anchor to rewrite. Use current visible max id + 1 for append-only.
* `rewrite_summary`: Non-empty concise summary when rewriting history. Use an empty string only for append-only.

长规则不再塞进每个工具描述，而是由 `HistoryRewritePrelude` 统一注入到对话最前面。

所有隐藏字段 `_block_id` / `_turn_id` / `_turn_slot` 必须在 `tool.definition` 中剥离，不向 LLM 公开。

#### 4. `HistoryRewritePrelude`：全 agent 前置规则注入
该规则消息采用与 `CAPS` 类似的“合成 user + assistant 前置消息”方式注入，但与 `CAPS` 不同的是：
* **所有 agent 都注入**
* **不受任何 excluded-agent 列表影响**
* **不参与块号扫描与裁剪投影**

前置规则建议写成一个简明但完整的英文说明块，至少包含：
* block id 单调递增且绝不复用
* append-only 的填写方式
* rewrite 的填写方式
* `user` 消息永不删除
* 锚点后的非 user 文本会从可见历史中丢弃
* 并行同 turn 的精确语义

最低要求正文如下：

> You can rewrite visible history when calling tools. `rewrite_anchor_id` must be a currently visible block id, or current visible max id + 1 for append-only. `rewrite_summary` must be non-empty when rewriting and must be empty for append-only. Block ids never reuse. User messages are never removed. When rewriting, the chosen anchor is replaced with a concise summary and later non-user visible history is discarded. If you issue multiple tool calls in the same assistant turn, we will use the minimum anchor of all, and the ordered merge of all per-call summaries.

#### 5. 生命周期钩子流转

##### `tool.definition`
职责：
* 给所有工具补充 `rewrite_anchor_id` / `rewrite_summary`
* 从 LLM 可见 schema 中去掉 `_block_id` / `_turn_id` / `_turn_slot`
* 保留已有 `_ui` 逻辑

##### `tool.execute.before`
此钩子需要改为接收 `ctx`，因为它必须访问宿主完整原始历史。

职责：
1. 读取 session 的完整原始历史。
2. 去掉可能存在的 stale `rewrite-prelude-*` / `caps-synth-*` 合成消息，只保留真实原始历史用于分析。
3. 运行一遍 `VisibleHistoryProjector` 的**元信息模式**，只算出当前可见块集合与当前可见最大块号。
4. 校验 `rewrite_anchor_id` / `rewrite_summary`：
   * append-only 必须是 `maxVisibleId + 1` 且 summary 为空
   * rewrite 必须指向当前可见块且 summary 非空
5. 通过 `SessionBlockSequencer` 原子分配 `_block_id` / `_turn_id` / `_turn_slot`。
6. 把这些隐藏字段注入当前工具参数，供 `after` 和后续 `transform` 使用。

##### `tool.execute.after`
职责：
1. 读取 `_block_id`。
2. 将原始工具输出编码为：
   * object 输出：追加 `id_`
   * string / scalar 输出：前置 `#id_: N\n`
3. 保留原始负载本身，不在这里做任何压缩总结或历史裁剪。

##### `experimental.chat.messages.transform`
这是整个系统的核心：它拿到的输入被视为**完整原始消息流**。

建议执行顺序如下：

1. **清理旧的合成前置消息**
   * 删除已有 `rewrite-prelude-*`
   * 删除已有 `caps-synth-*`

2. **原始 `read` 去重预处理**
   * 仅对真实 `read` 工具输出做去重
   * 比较前必须先通过 `ToolBlockCodec` 剥掉 `#id_:` 前缀
   * 这一步在可见历史投影之前执行，避免后续压缩 summary 再被 dedup 掉

3. **可见历史折叠**
   * 调用 `VisibleHistoryProjector`，从完整原始流折叠出本轮真正应给模型看的历史

4. **注入前置规则消息**
   * 无条件注入 `HistoryRewritePrelude`

5. **注入 CAPS 消息**
   * 保持现有 `CAPS` 行为与 excluded-agent 规则
   * 但 CAPS 发生在 rewrite prelude 之后

6. **原地替换 `output.messages` 内容**
   * 继续遵守宿主“数组引用不可更换”的约束
   * 尽量复用未受影响 part / state 的 live 引用

##### `event`
职责：
* 在 `stream-end` / `stream-abort` 时关闭 `SessionBlockSequencer` 的 `openTurnId`
* 不影响 `nextBlockId`

#### 6. `VisibleHistoryProjector`：part 级可见历史折叠器
它是纯函数，输入是**完整原始消息流**，输出是**裁剪后的可见消息流**。

##### 第一步：抽取真实事件
从原始消息中抽取三类事件：
* `UserPart`
* `NonUserTextPart`
* `ToolBlockPart`

其中 `ToolBlockPart` 需要携带：
* `blockId`
* `turnId`
* `turnSlot`
* `rewriteAnchorId`
* `rewriteSummary`
* `toolName`
* `originalOutput`

##### 第二步：按 `_turn_id` 分组
把同一 `_turn_id` 的所有工具输出归为一个 turn 组。

组内排序以 `_block_id` 升序为准。

##### 第三步：求每个 turn 的有效 rewrite 决议
对每个 turn 组计算：
* `effectiveAnchorId = min(all rewriteAnchorId)`
* `mergedSummary = 按 _block_id 升序拼接所有非空 rewriteSummary`

若该组所有调用都是 append-only，则：
* 不重写旧历史
* 只把当前 turn 的内容按块号升序追加到可见历史尾部

若该组触发 rewrite，则按以下顺序处理：
1. 在当前累计可见历史中找到 `effectiveAnchorId` 对应的锚点块。
2. 把该锚点块的可见输出替换为统一字符串：`#id_: effectiveAnchorId\n<mergedSummary>`。
3. 删除当前累计可见历史中位于锚点之后的所有非 user part。
4. 将当前 turn 自己的新 part 重新追加进来。

这保证最终形态类似：
* rewrite 前：`#0 #1 #2 #3`
* 当前 turn 预分配：`#4 #5`
* 当前 turn 决议锚点：`#1`
* rewrite 后可见：`#0 #1(summary) #4 #5`

##### 第四步：重建消息结构
折叠器最终必须把存活的 part 重新装配回 message：
* `user` message 保留所有原始 part
* 非 `user` message 只保留 surviving part
* 没有 surviving part 的非 `user` message 直接移除

---

### 四、实现边界与兼容性

1. 本方案不依赖 `orchestratorSystemPrompt`；行为约束全部来自：
   * 工具 schema 中的简短字段描述
   * `HistoryRewritePrelude` 合成前置消息

2. 本方案不依赖运行时维护“可见历史栈”；可见历史完全由 `VisibleHistoryProjector` 每次从完整原始历史重放得出。

3. 唯一保留的内存状态是 `SessionBlockSequencer` 的最小瞬时分号状态。

4. `CAPS` 注入仍保留，但不参与块号分配与 rewrite 折叠。

5. `read` 去重必须在统一剥皮层之后做比较，否则 `#id_:` 前缀会破坏 dedup 命中率。

---

### 五、验证要点

至少覆盖以下测试：

1. **分号初始化**
   * 从已有完整历史中读出最大块号并以 `max + 1` 继续分配

2. **单调块号**
   * rewrite 之后继续分配更大的块号
   * failure / abort 也会消费块号

3. **编码与剥皮**
   * object 输出追加 `id_`
   * string 输出追加 `#id_:` 前缀
   * dedup 比较前能正确剥掉前缀

4. **append-only 校验**
   * `rewrite_anchor_id = maxVisible + 1` 且 `rewrite_summary = ""` 通过
   * 其它组合正确拒绝

5. **rewrite 校验**
   * 仅允许引用当前可见块号
   * 已裁掉的旧块号再次引用时拒绝

6. **并行同 turn 语义**
   * 同 turn 多工具调用按同一 `_turn_id` 分组
   * 有效锚点取最小值
   * summary 按 `_block_id` 升序合并

7. **part 级裁剪**
   * `user` part 永不丢失
   * 锚点后的非 `user` 文本 part 被删除
   * 当前 turn 的新 part 仍被追加回来

8. **前置规则注入**
   * `HistoryRewritePrelude` 对所有 agent 生效
   * 不受 `CAPS` excluded-agent 规则影响

9. **宿主引用约束**
   * `output.messages` 数组引用保持稳定
   * 对未变更的 part / state 尽量保留原引用
