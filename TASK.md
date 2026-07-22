# 继续 OpenCode Structured Flow 全量完成补足缺口

## 一、最终裁决

### 工程师声称的“全部做完”

**不成立。**

### 实际完成状态

更准确的名称是：

> **Structured Flow Foundation Prototype / Phase A～H 局部原型**

而不是：

> **OpenCode 重构完成版**

### 粗略完成度

| 口径                  |      完成度判断 |
| ------------------- | ---------: |
| Flow 与数据模型基础        |      约 70% |
| Journal 原型          |      约 60% |
| 普通 Process 原型       |      约 50% |
| Session/Prompt 算法骨架 |      约 25% |
| 真实 OpenCode 接入      |     约 0～5% |
| 用户可见功能迁移            | 低于 10% 被证明 |
| OpenCode 可切换发布      |         0% |
| 完整路线图 A～P           |      约 20% |

即使所有现有单元测试全部通过，也不能改变这个结论。

---

# 二、路线图是否不全

## 结论：路线图基本足够，不是主要责任

KISS-13 已经明确列出：

* OpenCode Host/Gateway；
  -宿主 codec 和 MessageOrigin；
  -静态 Tools；
  -MessageTransform；
  -Journal；
  -Process/PTY；
  -Session Driver；
  -Todo；
  -Subagent；
  -Review；
  -Fallback；
  -Nudge 删除；
  -完整 Session Flow；
  -Compaction/Context/Title；
  -万象阵；
  -全量测试和切换。

也明确要求行为清单、P0/P1 覆盖、真实 E2E、重启、泄漏和切换验收。

因此，下面这些缺失不能推给路线图：

```text
没有 OpenCode Hook
没有 Plugin 入口
没有宿主 Decode/Encode
没有真实 Tool 注册
没有 PTY
没有 Wanxiangzhen
没有真实 Child/Review
没有 OpenCode E2E
没有旧行为映射总账
```

### 路线图唯一值得补强的地方

应要求仓库中强制存在：

```text
docs/opencode-next-migration.md
```

每个 Behavior ID 必须标注：

```text
Not Started
Foundation Only
Contract Tested
Host Integrated
Real E2E
Cutover Ready
```

并由 CI 禁止在仍有未完成行为时打出“Done”标签。

但路线图本身已经要求迁移总账；工程师连总账都没有提交。因此这只是门禁不够机械，不是实施内容不清楚。

---

# 三、最致命的证据：根本没有 OpenCode 插件

路线图要求 OpenCode Adapter 至少提供：

```text
onChatMessage
onMessageTransform
onToolDefinition
onToolBefore
onToolAfter
onSessionEvent
onCommand
onDispose

Plugin
Hooks.register
Decode
Encode
SessionGateway
```

并要求每个 Hook 完成明确的 decode → dispatch → encode。

当前 `next/OpenCode/` 只有：

```text
Gateway.fs
```

而这个 Gateway 只做：

```text
建立 .wanxiangshu-next/runtimes
Boot.boot
Fold.apply
创建 JournalWriter
Dispose JournalWriter
```

它没有：

* OpenCode client；
* Hook 注册；
  -宿主参数 DTO；
  -MessageOrigin 解码；
  -UserMessageId/parentID 解码；
  -Session Inbox 路由；
  -Driver 激活；
  -工具注册；
  -Plugin 出口；
  -宿主返回值编码。

整个 OpenCode 生产目录只有约 **135 行**。目录结构也明确显示只有这一个文件。

所以当前代码甚至不能作为 OpenCode 插件加载。

这不是“功能还有几个 Bug”，而是**宿主层尚未实现**。

---

# 四、所谓 Driver 实际不是 Driver

当前 `Session/Driver.fs` 只实现了：

```text
ConcurrentDictionary<RuntimeId × SessionId, DriverSlot>
Activate
Cancel
Deactivate
LocalEpoch
```

它保存的只是一个 `CancellationTokenSource`。

它没有：

* Driver Task；
  -启动 `SessionFlows.run`；
  -消费 Inbox；
  -处理 Human 抢占；
  -等待 initial host terminal；
  -处理 Tool Command；
  -等待 Prompt terminal；
  -异常后 Fail Closed；
  -有界 shutdown；
  -重启重入。

换言之：

> `Driver.fs` 是一个 Driver slot 表，不是 Session Driver。

路线图中的核心不变量是“唯一 Driver 运行主程序并顺序消费 FIFO Inbox”，当前实现只完成了“禁止本进程重复登记 CTS”的很小一部分。

---

# 五、Session 主程序只是依赖注入样例

`SessionFlows.run` 的确写出了正确的黄金源码：

```fsharp
let run s =
    session {
        do! passReview s
        return! s.Finish()
    }
```

这一点值得肯定。

但 `SessionScript` 中真正重要的行为全部是外部注入：

```fsharp
ContinueWork: unit -> SessionFlow<unit>
RequestReview: unit -> SessionFlow<unit>
Finish: unit -> SessionFlow<SessionOutcome>
CommitTodoFrom: SendOutcome -> SessionFlow<unit>
```

生产工程没有构造真实 `SessionScript` 的代码。

测试中使用的是：

* mutable counter；
  -假 Todo View；
  -假 Review View；
  -`session { return () }`；
  -手写 fake `ContinueWork`。

所以当前证明的是：

> 给我一个已经实现好所有领域动词的 Session，我可以正确地排列它们。

但整个重构的主要工作恰恰是：

> 实现这些领域动词，并把它们接到 OpenCode。

这一部分尚未完成。

---

# 六、PromptProtocol 只有纯决策，没有 Prompt 协议执行

当前完成了：

```text
HistoricalPromptIndex
LocalPromptProtocol
evaluateSendOnce
recordSubmitted
recordTerminal
PromptKey
```

这是一组有价值的纯函数。

但没有实现：

```text
PromptRequested commit
调用 OpenCode transport
取得 UserMessageId
PromptSubmitted commit
FIFO 等 assistant parentID
PromptTerminal commit
AcceptanceUnknown reconcile
Deadline
本地 Pending 状态持有与恢复
```

没有任何函数真的向 OpenCode 发送 Prompt。

因此它不是“Prompt 协议已完成”，而是：

> **Prompt 协议决策模型已起草。**

---

# 七、Journal 做得最多，但仍未达到可用 Runtime

Journal 是本次交付最扎实的部分：

* 每 Runtime 独立文件；
  -CreateNew；
  -固定 Frontier；
  -活跃文件只读；
  -EOF 半行忽略；
  -CommitUnknown；
  -进程内串行 Writer；
  -时间归并；
  -事实 codec；
  -Historical Prompt Fold。

这些工作不是敷衍。

但仍有几个严重问题。

## 7.1 Projection 是全局单例，不是按 Session 隔离

当前 `ProjectionSet` 是：

```fsharp
{ Todos: TodoSnapshot option
  LastReview: ReviewVerdict option
  HistoricalPrompts: Map<...>
  RuntimeId: RuntimeId option }
```

只有一个 `Todos` 和一个 `LastReview`。

`foldEnvelope` 处理 Todo 和 Review 时根本不查看：

```fsharp
env.Stream
```

因此：

```text
Session A TodoChanged
Session B TodoChanged
```

最终只留下后一个全局 Todo。

这直接违反：

* 多 Session 并行；
  -同 workspace 多 OpenCode；
  -每功能、每 Session 小投影；
  -SessionScript 读取自己 Todo/Review。

当前日志虽然定义了 `StreamId.Session`，生产 Fold 却没有真正按 Stream 分区。

这是阻断级正确性 Bug。

## 7.2 测试掩盖了这个问题

名为：

```text
Two runtimes same session both files written third boot merges both
```

的测试实际上把事件写到：

```fsharp
StreamId.Workspace
```

没有创建任何共同的 `SessionId`，也没有断言同一 Session 的最终投影。

它只检查结果里同时出现 `rtA` 和 `rtB`。

因此测试名称声称验证了“同 Session”，实际只验证了：

> 两个日志文件都能被枚举。

这属于典型的**测试标题完成法**。

## 7.3 没有 commit → Fold → publish 的生产主路径

`JournalWriter.Append` 返回 Envelope，但工程中没有统一生产代码完成：

```text
Append
→ Flush
→ Fold 到对应 Session 投影
→ 更新 ProgressStamp
→ 发布 Snapshot
```

`Fold.foldEnvelope` 只在 Gateway 启动时和测试中使用。

Gateway 暴露的 `ProjectionSet`、`RuntimeSnapshot` 也是启动时构造的不可变值，后续 append 后不会自动更新。

因此文档要求的：

```text
Read Your Writes
```

目前只在 Journal Writer 层“写得到”，没有在 Runtime Projection 层真正闭合。

## 7.4 大量 Fact 只编解码，不参与恢复

Fact 支持：

* Child；
  -Process；
  -Squad；
  -SessionSettled。

但 `Fold.foldEnvelope` 基本忽略这些事实。

所以它们目前只是：

> 能序列化到 JSON 的类型

而不是：

> 能恢复真实业务状态的事件。

---

# 八、Tools 几乎没开始

路线图要求：

* 静态工具列表；
  -FileTools；
  -SearchTools；
  -ExecutorTools；
  -MethodologyTools；
  -ReviewTools；
  -SubagentTools；
  -todowrite 经 SessionCommandPort；
  -权限；
  -schema；
  -有界输出；
  -真实 OpenCode 注册。

当前 `next/Tools/` 只有：

```text
ToolContext.fs
MessageTransform.fs
```

`ToolContext.fs` 只定义：

```text
Tool
ToolInput
ToolOutput
SessionCommandPort
```

没有一个真实 Tool 实现，也没有 `SessionCommandPort` 实现。

MessageTransform 也只处理：

```fsharp
{ Role: string
  Text: string }
```

它无法表达真实 OpenCode 消息中的：

* tool call parts；
  -tool result parts；
  -message IDs；
  -metadata；
  -assistant parts；
  -结构化内容。

它通过字符串前缀删除系统消息：

```text
[CAPS:
[REVIEW:
[HINT:
```

这只是概念演示，不是可以替换现有 OpenCode MessageTransform 的生产模型。

---

# 九、Subagent 与 Review 都只是 Fake-friendly 接口

## Subagent

`ChildFlows.runChild` 只有：

```fsharp
let! s = c.GetOrCreateSession(request)
let! res = s.Run(request.Prompt)
return res
```

但 `GetOrCreateSession` 和 `Run` 都由调用者注入。

没有：

* OpenCode child 创建；
  -先 waiter 后 send；
  -UserMessageId/parentID 相关；
  -ChildCreated/Completed commit；
  -resume；
  -parent abort；
  -物理 child 生命周期；
  -真实取消；
  -宿主 transcript。

测试全部使用内存 fake `ChildSession`。

## Review

`ReviewReport` 已经直接携带：

```fsharp
Verdict: ReviewVerdict
```

因此连真实 reviewer 文本解析都绕开了。

`StartReviewer`、`Review`、`AcceptVerdict` 同样由 fake 注入。没有：

* reviewer prompt；
  -OpenCode child；
  -工具权限；
  -报告解析；
  -affected files；
  -真实空输出；
  -WIP；
  -parent abort；
  -E2E。

Review 当前完成的是“有界 Invalid 递归”和“复合事实构造”，不是 Review 功能迁移。

---

# 十、Fallback 只完成了循环算法

Fallback 的尾递归写法是正确进步：

```text
tryAttempts
tryModels
continueWork
```

并正确阻止 AcceptanceUnknown 后换模型。

但真正困难的部分全部在注入函数中：

```fsharp
type SendContinueFunction =
    string -> int -> SessionFlow<SendOutcome>
```

缺少：

-真实模型解析；
-OpenCode prompt；
-PromptKey；
-Requested/Submitted/Terminal；
-网络错误分类；
-工具调用文本恢复；
-空输出识别；
-宿主取消；
-reconcile；
-read-your-writes commit。

所以 Phase L 只能算“算法草图完成”，不能算 Fallback 迁移完成。

---

# 十一、Process 只完成普通命令的一半

值得肯定：

* stdout/stderr pump 在 spawn 时启动；
  -有界输出；
  -stdin；
  -取消；
  -deadline；
  -kill tree；
  -真实 OS 命令测试。

但路线图明确要求 Process **和 PTY**，当前完全没有：

```text
PtyHandle
SpawnPty
Resize
ReadUntilExit
ReadUntilIdle
ReadUntilMarker
```

目录结构也没有任何 PTY 文件。

此外，当前 Process 实现仍违背自己的规范：

* kill 异常大量 `with _ -> ()` 静默吞掉；
  -DisposeAsync 静默吞清理失败；
  -泵 drain 固定写死 5 秒；
  -kill wait 固定写死 2 秒；
  -没有复用调用方绝对 Deadline 的剩余预算；
  -kill 后立即 cancel pump，可能丢弃仍可读取的尾部；
  -没有 PID/端口泄漏断言；
  -没有 Process Fact 提交。

因此 Phase G 至多算普通 Process 原型，不是完成。

---

# 十二、万象阵完全没有实现

路线图 Phase P 要求：

```text
DAG/waves
Worktree
Slave
Verify
PublishVerified
串行 FastForward
AcceptWave
取消
孤儿
重启
HTTP 只读
```

并专门定义了 `SquadScript` 和结构化主流程。

当前 `next/` 中没有：

```text
Wanxiangzhen/
SquadScript
SquadFlow
Worktree
Slave
FastForward
```

只有 Fact DU 中预先放了几个 Squad case。

“定义了 Squad Fact”不能算“万象阵完成”。

Phase P 是 **0%**。

---

# 十三、Phase O 也是 0%

正式指南要求明确接入：

```text
CompactIfNeeded
UpdateContextBudget
EnsureTitle
```

并要求不能再偷偷挂第二条 EventBus 路径。

当前生产代码没有这三个动词，也没有对应事实、流程或测试。

---

# 十四、测试体系证明的是原型，不是迁移完成

当前共有约：

```text
33 个生产 F# 文件
约 3,000 行生产代码
17 个测试文件
70 个 [Fact]
```

数量不算少，但测试层级几乎全部是：

-纯函数单测；
-内存 fake；
-Journal 文件单测；
-本地 Process 单测；
-类型存在检查。

缺少：

-真实 OpenCode 插件加载；
-真实 Hook decode/encode；
-真实 Prompt；
-真实 MessageID/parentID；
-真实 todowrite；
-真实 child session；
-真实 review；
-真实 fallback；
-真实工具注册；
-真实 restart E2E；
-行为 ID 对照；
-旧版与新版 characterization 对照；
-OpenCode real E2E；
-cutover 测试。

OpenCode 测试目录只有 `GatewayBootTests.fs`，验证的是 Journal Gateway 能启动和释放，不是 OpenCode Gateway 能工作。

## 两个典型的弱测试

### “Runtime A 看不到 B 的运行期 append”

测试只是：

```text
保存 bootSnapshotA.Envelopes.Length
B append
再次读取同一个不可变 bootSnapshotA.Length
```

任何不可变列表都会通过。

它没有启动 Runtime A，也没有验证 A 的 Projection、Driver 或查询 API。

### “GuideContract”

主要内容是：

```fsharp
ignore typeof<...>
ignore FunctionName
```

它证明符号存在，不证明契约成立。

---

# 十五、工程师还重新引入了路线图明确批判的硬行数门禁

架构测试强制：

```text
Next_source_files_do_not_exceed_300_lines
```

而 KISS 总纲恰恰指出旧工程的硬文件尺寸门禁会奖励：

* 把完整流程切碎；
  -Helper；
  -Extras；
  -Core；
  -纯转发文件。

当前 Journal 已经出现：

```text
FactDecoders.fs
FactDecodersExtras.fs
FactDecodersHelpers.fs
FactDecodersPrompt.fs
FactSubEncoders.fs
```

这正是重构要根除的文件族命名。

也就是说，工程师不仅没有完成全部迁移，还在门禁层重新植入了旧病因。

这不是 KISS。

---

# 十六、逐 Phase 审计

| Phase | 路线图要求                         | 实际状态                   | 结论           |
| ----- | ----------------------------- | ---------------------- | ------------ |
| 0     | 指南闭合、host spike、GuideContract | 只有签名检查，无 host spike    | 部分           |
| A     | Flow 内核                       | 主体已实现                  | 接近完成         |
| B     | OpenCode no-op Host/Gateway   | 只有 Journal Gateway     | 严重未完成        |
| C     | 宿主 codec、Origin               | 只有类型，无 codec           | 未完成          |
| D     | 静态真实 Tools、CommandPort        | 只有接口                   | 未完成          |
| E     | 真实 MessageTransform           | 简化 role/text 演示        | 部分           |
| F     | Per-Runtime Journal           | 主体原型存在，但投影全局化          | 部分完成，有阻断 Bug |
| G     | Process/PTY                   | 普通 Process 部分；无 PTY    | 部分           |
| H     | Session Runtime + Driver      | 只有 CTS slot、FIFO、纯协议函数 | 严重未完成        |
| I     | Todo + ContinueWork           | 只有 View/注入接口           | 未完成          |
| J     | Subagent                      | fake-friendly wrapper  | 未完成          |
| K     | Review                        | 纯辅助算法                  | 未完成          |
| L     | Fallback                      | 尾递归算法                  | 部分           |
| M     | 删除 Nudge 并迁移行为                | next 中无 Nudge，但行为未迁移   | 不能验收         |
| N     | 完整 Session Flow               | 黄金组合存在，但未接线            | 部分           |
| O     | Compaction/Context/Title      | 不存在                    | 0%           |
| P     | 万象阵                           | 不存在                    | 0%           |
| 切换    | 全 E2E、行为映射、发布入口               | 不存在                    | 0%           |

---

# 十七、这是偷懒，还是误解“做完”

不能从代码判断工程师主观态度，但可以判断交付行为。

## 不是完全敷衍

工程师确实认真完成了：

* Flow CE；
  -ProgressGuard；
  -尾递归 Fallback；
  -Review 复合事实；
  -Per-Runtime Journal 原型；
  -CommitUnknown；
  -Process pump；
  -部分并发测试；
  -架构隔离。

所以不能说“什么都没干”。

## 但明显存在避难就易

完成的主要是：

```text
纯类型
纯函数
codec
内存 fake
文件单测
基础算法
```

跳过的主要是：

```text
真实 OpenCode
真实 Hook
真实 Prompt
真实 Tool
真实 Session Driver
真实 Child
真实 Review
真实 E2E
PTY
万象阵
切换
```

这正好是工作中最难、最容易暴露时序问题的部分。

所以准确评价是：

> **工程师不是没工作，而是完成了舒适区内的基础设施，然后严重提前宣布项目完成。**

---

# 十八、应立即撤回的声明

不得称为：

```text
OpenCode Structured Flow 重构完成
全部功能迁移完成
可以切换
```

正确状态应改成：

```text
Phase A 基础内核完成
Phase F/G 部分原型完成
OpenCode 宿主和业务迁移尚未开始或刚起步
```

---

# 十九、下一轮必须按“可运行垂直切片”交付

禁止继续增加 codec、Fact case 或 fake 测试来制造进度感。

下一里程碑必须是一个真实的最小闭环：

```text
真实 OpenCode 插件加载
→ onChatMessage decode MessageOrigin
→ initial terminal/idle
→ 本 Runtime Driver 启动
→ todowrite 经 SessionCommandPort
→ Todo 未完成
→ ContinueWork 构造 PromptKey
→ 调 OpenCode prompt
→ UserMessageId/parentID 相关
→ terminal
→ TodoChanged commit
→ while 退出
→ Finish
```

并要求：

1. 使用真实 OpenCode Harness；
2. 至少一个 real E2E；
3. 中途重启一次；
4. 不使用 fake `ContinueWork`；
5. 不允许测试直接构造 Verdict 或 Terminal 结果；
6. 所有事件进入明确 `StreamId.Session(sessionId)`；
7. Session A/B Todo 不互相覆盖；
8. 测试失败时能显示完整事件轨迹。

这条闭环没有完成之前，不得再接受“Phase H/N 已完成”。

---

# 二十、最终结论

责任排序：

1. **主要责任：工程师严重提前宣布完成。**
2. **次要责任：路线图缺少一个不可绕过的机器化完成总账，但内容本身已经足够明确。**
3. **“确实完成”：明确否定。**

当前成果值得保留，但必须重新定级：

> **一个有价值的 Structured Flow + Journal + Process 原型，不是万象术 OpenCode 新实现。**

以用户能否安装并使用为标准，它目前还没有跨过起跑线；以底层技术探索为标准，它完成了一个不错的第一阶段。
