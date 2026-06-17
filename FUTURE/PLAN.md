为在 **Opencode** 端引入基于可见历史重写的“高级特征替换与历史截断”能力，我们将方案收敛为一套**单调分号、原始历史保留、可见历史投影、消息可见内容裁剪、独立 `backtrack` 工具跳转**的实现。

本方案继续严格遵循“内核-外壳”分离原则：
* **外壳（Opencode 插件层）** 负责宿主 API 适配、分号、消息扫描、合成消息注入。
* **内核（纯函数层）** 负责 `id` 编解码、可见历史折叠、裁剪投影等纯逻辑。

---

### 一、核心语义

#### 1. `id` 与宿主线格式
1. 每一个**真实工具调用结果**都被视为一个独立的 `id`，也是一个可被 `backtrack.anchor` 锚定的真实事件。
2. 每个 session 的 `id` 由 `0` 开始，**只增不减，绝不复用**。
3. 分号一旦成功发出，该 `id` 立刻视为已消费；**成功 / 失败 / 中止**都不回收。
4. 工具输出的宿主兼容编码统一如下：
   * 若输出是 **JSON object**，追加字段：`id_: N`
   * 若输出是 **string**，前置前缀：`#id_: N\n`
   * 若输出是 **非 object 标量**（如 `true` / `42`），先按字符串处理，再前置：`#id_: N\n`

#### 2. 可见历史重写语义
1. 宿主底层**原始历史永不物理删除**。
2. 所有裁剪都只发生在**提供给模型的可见历史投影**上。
3. `user` 消息**永不裁剪**。
4. 每个真实工具调用结果天然形成一个可引用锚点。
5. 一旦触发重写，系统会：
   * 将锚点内容替换为统一字符串：`#id_: N\n<note>`
   * 删除该锚点之后当前可见历史中的**所有非 user 可见内容**
   * 再把该次 `backtrack` 之后消息流中的后续真实工具结果按规则追加回来
6. 裁剪边界以**消息的可见内容 / 可见事件**为语义。
7. 若某个非 `user` message 在投影后没有剩余可见内容，则该 message 会从可见历史中移除。
8. **`backtrack` 事件在最终提供给模型的可见历史中永远不可见**。它只作为原始历史中的一种控制事件存在，用于驱动可见历史重写；给模型的最终可见消息流中不保留 `backtrack` 自身的工具输出占位，也不把 `backtrack` 自身呈现为可见事件。

#### 3. `backtrack` 与合法锚点
1. 向下正常追加而不裁剪历史时，assistant 直接调用普通工具；**普通工具不再携带任何 rewrite 参数**。
2. 发生重写时，assistant 必须显式调用独立工具 `backtrack`。
3. `backtrack.anchor` 必须指向一个**当前仍可见**的 `id`（即当前可见历史投影中仍存在的真实工具调用结果）。
4. `backtrack.note` 必须是**非空**高密度总结。
5. 已经从可见历史中裁掉的旧 `id`，后续不得再次引用。

#### 4. 多次 `backtrack` 的顺序语义
1. 普通工具调用只产生各自的真实工具调用结果。
2. `backtrack` 本身不引入新的跳转语义字段，只有：`anchor` 与 `note`。
3. 若历史中出现多次 `backtrack`，则按它们在原始消息流中的出现顺序依次应用。
4. 后出现的 `backtrack` 可以基于前一次重写后的**当前可见历史**继续跳转。

写入前置规则消息中的原文如下：

> If you issue multiple backtrack calls, they are applied in message order. Each backtrack uses the currently visible history at that point.

---

### 二、命名与数据约定

#### 1. 对 LLM 暴露的工具名与参数名
* `backtrack`
* `anchor`
* `note`

#### 2. 纯逻辑与组件名
* `Seq`: per-session 原子分号器
* `Codec`: `id_` / `#id_:` 的解析与剥皮层
* `Projector`: 从完整原始消息流折叠出可见历史
* `Prelude`: 面向所有 agent 的前置规则注入
* `backtrack`: 对 LLM 暴露的独立重写工具

#### 3. 合成消息 ID 前缀
* `rewrite-prelude-user-*`
* `rewrite-prelude-assistant-*`

它们和现有 `caps-synth-*` 一样都是**合成前置上下文**，但不参与 `id` 扫描、锚点合法性判断与裁剪折叠。

---

### 三、详细设计

#### 1. `Seq`：per-session 原子分号器
该组件是**唯一允许保留的瞬时内存状态**。它不保存任何历史语义，只保存分号运行所必需的最小状态。

每个 session 维护：
* `nextId`: 下一个可分配 `id`
* `lock`: per-session 原子锁

初始化规则：
1. 某个 session 第一次进入 `tool.execute.before` 时，读取**完整原始历史**。
2. 用 `Codec` 扫描历史中所有真实工具输出，取已出现的最大 `id` `maxId`。
3. 将 `nextId` 初始化为 `maxId + 1`。

分配规则：
1. `tool.execute.before` 获取 session 锁。
2. 分配当前 `nextId` 供当前工具输出编码使用。
3. `nextId <- nextId + 1`。
4. 释放锁。

这保证：
* 并发 `before` 不会撞号
* 所有真实工具调用结果都有唯一锚点
* rewrite 只依赖 `id`

#### 2. `Codec`：统一剥皮层
该层必须在内核中实现为纯函数，负责三件事：

1. **解析 `id`**
   * 从 object 输出中读取 `id_`
   * 从**已编码的工具字符串结果**中读取 `#id_: N\n` 前缀，得到 `id`
   * `Codec` 只对宿主写回时编码过的真实工具结果做解析，不能把普通文本误判为 `id` 前缀

2. **剥掉字符串前缀**
   * 将 `#id_: N\n<content>` 还原为 `<content>`
   * 仅在比较、去重、摘要拼接、投影判断时使用剥皮后的内容

3. **重新编码输出**
   * object 输出追加 `id_`
   * string / scalar 输出前置 `#id_: N\n`

这层会被三处共同使用：
* `tool.execute.after`：写回宿主可持久化的 `id`
* `Projector`：从完整原始历史读 `id`
* `read` 去重逻辑：在比较前先剥掉 `#id_:` 前缀

#### 3. 对 LLM 暴露的 schema
宿主提供一个普通工具 `backtrack`：
* `backtrack.anchor`: Currently visible id to rewrite from.
* `backtrack.note`: Non-empty concise note to keep at the anchor.

普通工具的 schema 不发生 rewrite 相关变化。

长规则不再塞进每个工具描述，而是由 `Prelude` 统一注入到对话最前面。

#### 4. `Prelude`：全 agent 前置规则注入
该规则消息采用与 `CAPS` 类似的“合成 user + assistant 前置消息”方式注入，但与 `CAPS` 不同的是：
* **所有 agent 都注入**
* **不受任何 excluded-agent 列表影响**
* **不参与 `id` 扫描与裁剪投影**

前置规则建议写成一个简明但完整的英文说明，至少包含：
* `id` 单调递增且绝不复用
* 普通工具调用不会触发 rewrite
* 何时调用 `backtrack`
* `backtrack` 的填写方式
* `user` 消息永不删除
* 锚点后的非 user 可见历史内容会从可见历史中丢弃
* 多次 `backtrack` 按消息顺序依次生效

最低要求正文如下：

> You can rewrite visible history by calling the `backtrack` tool. Normal tool calls never rewrite history. `backtrack.anchor` must be a currently visible id. `backtrack.note` must be a non-empty concise note. Ids never reuse. User messages are never removed. When rewriting, the chosen anchor is replaced with a concise note and later non-user visible history is discarded. If you issue multiple backtrack calls, they are applied in message order, and each one uses the currently visible history at that point.

#### 5. 生命周期钩子流转

##### `tool.execute.before`
此钩子需要改为接收 `ctx`，因为它必须访问宿主完整原始历史。

职责：
1. 读取 session 的完整原始历史。
2. 去掉可能存在的 stale `rewrite-prelude-*` / `caps-synth-*` 合成消息，只保留真实原始历史用于分析。
3. 运行一遍与 `experimental.chat.messages.transform` **共享同一预处理与 `Projector` 语义**的元信息模式：只算出当前可见 `id` 集合与当前可见最大 `id`。该模式与 transform 中使用的 `Projector` 完全一致，仅是输出元信息而非折叠后的消息流，确保 `tool.execute.before` 与 `experimental.chat.messages.transform` 对“当前可见历史”的判定不漂移。
4. 若当前工具是 `backtrack`，校验 `anchor` / `note`：
   * `anchor` 必须指向当前可见 `id`
   * `note` 必须非空
5. 若当前工具不是 `backtrack`，通过 `Seq` 原子分配下一个 `id`。

##### `tool.execute.after`
职责：
1. 若当前工具不是 `backtrack`，读取当前工具分配到的 `id`。
2. 将原始工具输出编码为：
     * object 输出：追加 `id_`
     * string / scalar 输出：前置 `#id_: N\n`
3. 若工具为 `backtrack`，原始负载只需返回最小确认信息，不编码新的 `id`，也不在这里做任何历史裁剪。
4. 保留原始负载本身，不在这里做任何压缩总结或历史裁剪。

##### `experimental.chat.messages.transform`
这是整个系统的核心：它拿到的输入被视为**完整原始消息流**。

建议执行顺序如下：

1. **清理旧的合成前置消息**
   * 删除已有 `rewrite-prelude-*`
   * 删除已有 `caps-synth-*`

2. **原始 `read` 去重预处理**
   * 仅对真实 `read` 工具输出做去重
   * 比较前必须先通过 `Codec` 剥掉 `#id_:` 前缀
   * 这一步在可见历史投影之前执行，避免后续压缩 note 再被 dedup 掉

3. **可见历史折叠**
   * 调用 `Projector`，从完整原始流折叠出本轮真正应给模型看的历史
   * 折叠结果中**不包含 `backtrack` 事件自身的可见内容或占位**；`backtrack` 只改变其他可见历史内容

4. **注入前置规则消息**
   * 无条件注入 `Prelude`

5. **注入 CAPS 消息**
   * 保持现有 `CAPS` 行为与 excluded-agent 规则
   * 但 CAPS 发生在 rewrite prelude 之后

6. **原地替换 `output.messages` 内容**
   * 继续遵守宿主“数组引用不可更换”的约束
   * 尽量复用未受影响 message / state 的 live 引用

#### 6. `Projector`：可见历史折叠器
它是纯函数，输入是**完整原始消息流**，输出是**裁剪后的可见消息流**。

**关键不变量**：
* `backtrack` 事件在最终可见历史中永远不可见。
* `backtrack` 只作为控制事件驱动重写，不改变自身在可见历史中的存在性（它本就不存在）。

##### 第一步：抽取真实事件
从原始消息中抽取三类真实事件：
* `UserMessage`
* `TextMessage`
* `ToolResult`

其中 `ToolResult` 需要携带：
* `id`
* `anchor`
* `note`
* `originalOutput`

##### 第二步：按消息流顺序应用
按完整原始消息流顺序扫描所有真实事件。

* 遇到普通工具输出时，按 `id` 正常追加到当前可见历史。
* 遇到 `backtrack` 时，立即基于**当下可见历史**执行一次 rewrite：
  1. 找到 `anchor` 对应的锚点事件。
  2. 把该锚点的可见输出替换为统一字符串：`#id_: anchor\n<note>`。
  3. 删除当前累计可见历史中位于锚点之后的所有非 user 可见内容。
  4. 继续处理该 `backtrack` 之后的后续消息流。

这保证最终形态类似：
* rewrite 前：`#0 #1 #2 #3`
* 之后调用 `backtrack(anchor=#1, note=...)`
* 再之后新的工具输出得到 `#4 #5`
* rewrite 后可见：`#0 #1(note) #4 #5`

##### 第四步：重建消息结构
折叠器最终必须把仍可见的真实事件重新装配回 message：
* `user` message 保留所有原始可见内容
* 非 `user` message 只保留仍可见的真实事件对应的内容
* 没有剩余可见内容的非 `user` message 直接移除

---

### 四、实现边界与兼容性

1. 本方案不依赖 `orchestratorSystemPrompt`；行为约束全部来自：
   * `backtrack` 工具 schema 中的简短字段描述
   * `Prelude` 合成前置消息

2. 本方案不依赖运行时维护“可见历史栈”；可见历史完全由 `Projector` 每次从完整原始历史重放得出。

3. 唯一保留的内存状态是 `Seq` 的最小瞬时分号状态。

4. `CAPS` 注入仍保留，但不参与 `id` 分配与 rewrite 折叠。

5. `read` 去重必须在统一剥皮层之后做比较，否则 `#id_:` 前缀会破坏 dedup 命中率。

6. `Codec` 解析 `id` 时，必须限定在“宿主已编码的真实工具字符串结果”范围内，避免把普通文本中的 `#id_:` 前缀误判为锚点。

---

### 五、验证要点

至少覆盖以下测试：

1. **分号初始化**
   * 从已有完整历史中读出最大 `id` 并以 `max + 1` 继续分配

2. **单调 `id`**
   * rewrite 之后继续分配更大的 `id`
   * failure / abort 也会消费 `id`

3. **编码与剥皮**
   * object 输出追加 `id_`
   * string 输出追加 `#id_:` 前缀
   * dedup 比较前能正确剥掉前缀

4. **`backtrack` 校验**
   * `anchor` 仅允许引用当前可见 `id`
   * `note` 为空时拒绝
   * 普通工具调用不参与 rewrite 参数校验

5. **rewrite 校验**
   * 仅允许 `backtrack` 引用当前可见 `id`
   * 已裁掉的旧 `id` 再次引用时拒绝

6. **多次 `backtrack` 顺序语义**
   * 多次 `backtrack` 按消息流顺序依次应用
   * 后一次 `backtrack` 只能引用当时仍可见的 `id`
   * 不再依赖任何分组或 note 合并

7. **消息可见内容裁剪**
   * `user` 消息内容永不丢失
   * 锚点后的非 `user` 可见内容被删除
   * 后续新工具输出的可见内容仍被追加回来

8. **`backtrack` 不可见性**
   * 最终可见历史中不存在 `backtrack` 事件自身的工具输出占位
   * `backtrack` 只改变其他可见历史内容，自身不进入模型可见消息流

9. **`tool.execute.before` 与 `experimental.chat.messages.transform` 一致性**
   * 两者对当前可见历史的判定复用同一预处理与 `Projector` 语义
   * 对同一原始历史，`before` 判定的可见 `id` 集合与 transform 投影结果一致
   * 不存在 before / transform 判定漂移

10. **前置规则注入**
    * `Prelude` 对所有 agent 生效
    * 不受 `CAPS` excluded-agent 规则影响

11. **宿主引用约束**
    * `output.messages` 数组引用保持稳定
    * 对未变更的 message / state 尽量保留原引用
