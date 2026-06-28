# BUGFIX

## 目标

把 `multi-frontmatter` 提升为等效事实源表达：同一条 `user` 消息/同一段 prompt 可以携带多块 front matter，且要与原有的工具输入、工具输出、LLM 输入输出、历史、回放、投影、宿主 wire format 保持等效可消费。

这次修复的起点不是“引入新格式”，而是解决 `compaction` 之后全量历史被压缩掉、事件溯源锚点丢失、`with-review` 与其他状态回放失败的问题。真正的修法是：在 `compaction` 完成后触发一次真实的 `prompt()`，让 LLM 收到普通消息，再用 `multi-frontmatter` 把历史里被压掉的锚点（包括工具输入/输出、LLM 输入输出、review / KG / backlog 锚点等）补回压缩后的对话历史。

补锚点消息的正文固定写成：`See above for some messages before compaction.`

这样正文只负责提示“上面那几块 front matter 是压缩前历史的补锚点”，所有结构化事实都放在前导多块 front matter 里，不再额外发多条 prompt，也不再把事实分散到别的正文里。

## 结论

- 不再接受“只能读单 front matter / 只能看 tool 或 LLM 输入输出”的例外。
- 不再把 `multi-frontmatter` 仅仅当成 replay / projection 的局部替身，而是当成与原事实源等效的表达。
- 不用 `multi-prompt` 作为主方案；用单 `prompt` 携带 `multi-frontmatter`，让消费方支持它。

## 核心原则

1. 历史事实认解析后的 front matter 序列，也认原本的工具输入、工具输出、LLM 输入输出、宿主 wire format。
2. `user` 消息里的 `multi-frontmatter` 可以与原先依赖的事实源等效，只要消费方支持。
3. 依赖真实执行语义、工具状态或宿主 wire format 的路径，不再以“不能等效”为理由豁免，而是升级到支持等效。
4. compaction 只是这个模型的一个消费者，不是例外。

## 为什么要改

当前事实源被拆在多层语义里：历史、replay、projection、tool output、LLM input、宿主 codec。只要核心解析还停留在单块 front matter，所有上游/下游都会被迫围着单块结构打补丁，最终让锚点、compaction、review、backlog、KG 的语义分叉；把 `multi-frontmatter` 做成等效表达，是为了让这些原本有效的事实继续被同一套解析消费。

`multi-frontmatter` 的作用是把“一个 `user` turn 里可能承载多段结构化事实”标准化，并与原有事实源等效。这样历史、重放、投影、审查和补锚点都能读同一份事实，不需要靠多次 `prompt()` 人工堆结构，也不会否定工具/LLM 语义里的原有真相。

尤其在 `compaction` 场景里，真实消息不是旁支，而是补锚点的唯一落点：压缩后要重新把被折叠掉的事件锚点写回普通历史，才能让后续重放、审查和状态恢复继续成立。

## 需要升级的核心面

- `Kernel/PromptFrontMatter.fs`
- `Kernel/LoopMessages.fs`
- `Kernel/KnowledgeGraph/Job.fs`
- `Kernel/ToolOutputInfo.fs`
- `Kernel/ReviewPrompts/*`
- `Kernel/BacklogProjectionCore.fs`
- `Kernel/ReviewReplayPolicy.fs`
- `Kernel/BacklogProjection.fs`
- `Opencode/SessionIo.fs`
- `Opencode/HookExecute.fs`
- `Opencode/SessionLifecycleObserver.fs`
- `Mux/PluginCatalog.fs`
- `Mux/ReviewToolsMux.fs`
- `Omp/SessionLifecycleHooks.fs`
- `Omp/ToolResultEvent.fs`
- `Shell/OpencodeSessionEventCodec.fs`
- `Shell/HostMessagePartCodec.fs`
- `Shell/MessageTransformCommon.fs`
- 各宿主消息 codec

## 迁移顺序

1. 先把 `Kernel/PromptFrontMatter.fs` 从“单块解析”升级为“多块解析”。
2. 再让只读消费者接受多块 front matter 作为事实源。
3. 再把工具/LLM/wire format 相关路径改成支持等效语义，而不是继续硬编码单块结构。
4. 最后补测试，锁住 multi-frontmatter 在 replay / projection / compaction 下的一致性。

## 验收

- `PromptFrontMatter` 可解析多块 front matter。
- 所有消费方都能处理多块 front matter，或把 `user` multi-frontmatter 视为与原事实源等效。
- compaction 后的锚点刷新不依赖多次 `prompt()`。
- 不再存在“单 front matter 才是真相”的分支。

## 风险

- 解析与投影不同步会让 replay 偏移。
- 宿主 codec、工具结果、历史投影一起改时容易引入回归。
- 需要最小测试先锁住多块 front matter 的语义，再扩面迁移。
