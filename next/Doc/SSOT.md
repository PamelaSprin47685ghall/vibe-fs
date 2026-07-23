# 新架构实现经验

| Field | Value |
| :--- | :--- |
| **Exported** | July 23, 2026 at 11:45 PM |
| **Model** | ChatGPT |
| **Messages** | 4 |

---

## 💬 Conversation

### 👤 User

我改主意了，与其重构这个，不如实现我的新想法。

每个 session (X) 都有一个伴随 session (Y)，Y 用便宜的模型。Y 的开头：

{消息0: You are the blogger of a coding agent session. See below for the session content.}

每次向 X 发送新请求（工具返回或者用户输入）时候，向 Y 发送：

{消息N+1: 此处是本次向 X 发送的请求比上次向 X 发送的请求增加的部分，转换成 yaml 格式。包括工具的输入输出，用户，思考，模型输出 ... }
{消息N+2: You are the blogger of a coding agent session. Write a new paragraph for these delta messages. Avoid raw code or stream of consciousness and maximize information density.}
将 X 的模型输出（不包括思考）称为 A 版工作记录，将 Y 截至目前输出的所有正式内容称为 X 的 B 版工作记录（不包括 blogger 的思考）。另外 blogger 不能调用工具。B 版工作记录天然是 X 的摘要。当 X 上下文溢出的时候，我们用 B 版工作记录打包成一条消息，替换 X 的所有上下文，从而可以零延时压缩上下文。如果某次 X 发送请求时候 Y 还在工作，则不打断，不排队，跳过本次，下次的 delta 会包含两次的内容。以此类推。当 Y 上下文溢出的时候，我们同样用 B 版工作记录替换，只需要把 delta 设置成只包含一条消息的 B 版工作记录。因此无论 X 的上下文大还是 Y 的上下文大，都不影响机制。压缩可以再压缩（自然的，不用额外写代码），越远的越模糊，天然符合认知规律。

---

在新想法中，inspector/coder/browser/meditator 不再伪装成同步工具，而是天然异步。

主代理 manager 的工具没有 read,write,edit 等任何常规工具，也不能同步调用 subagent。只有
- fork(agent=executor/inspector/coder/browser/meditator/reviewer/hex[6], prompt=...), 表示新建子代理，或者给已有的子代理训话 (hex[6] 是已有的子代理的标识符)。新的子代理会拿到主代理的 B 版工作记录，这样天然知道背景，无需啰嗦解释。
- join(), 表示等待任意一个子代理返回（无法指定是哪一个，哪个先回来就是哪个，返回值里面会告诉你是哪个子代理回来了，以及子代理的 A 版工作记录，注意 reviewer 的 verdict 工具只接受 PERFECT/REVISE，不再接受描述，A 版工作记录就是描述）

executor 只能说话，没有工具，没有伴随。
inspector 可以用 executor，可使用 executor(command=..., estimated_output_bytes=..., estimated_running_secs=..., estimated_mem_usage=medium/large) 同步调用. 如果输出大于 estimated_output_bytes 3 倍，则触发摘要. 如果耗时大于 estimated_running_secs 3 倍，则杀. estimated_mem_usage = large 的，在整个 opencode 进程内，同时只能运行一个，别的排队。不再支持 python / js 直接 execute。inspector 没有伴随，启动的 executor 用 A 版工作记录当作背景。

形如：
{消息 0: You are a summerizer for a command output, ... [仅为示意，大概如此] See below for the master session for which you are working.}

{消息 1...N: A 版工作记录}

{消息 N+1: The command is ..., output:
... [仅为示意]
}

coder 可以同步调用 inspector，鼓励并行调用。coder 有伴随，调用时用 B 版工作记录。
browser 只能 read 和上网。meditator/reviewer 只能同步调用 read/glob/grep/inspector，没有伴随。新版不再集成 fuzzy_grep fuzzy_glob 等工具。

Fallback 重试还要。智能催促也要，但不和 todo 挂钩了，只用来 enforce 审查循环。select_methodology 不要了。PTY 功能保留，只有 manager 能用，异步语义，也是 fork 的一种，fork prompt 为 "content..." 可 write，"" 可以 read，"[#SIGTERM]" / "[#SIGKILL]" 可以杀,以此类推各种 SIG. 提供一个 list(...) 工具,不仅能列出 pty 也能列出所有 fork 出去的子代理,一视同仁.

万象阵不要了。新角色 orchestrator, 专门 fork manager，也是只能 fork/join.这样比万象阵还厉害。fork manager 自动开 git worktree 隔离, 自动让新的 manager 进入 /loop 模式,和万象阵一样最后 rebase + ff 回来. 用户向 orchestrator 发消息时如果工作区脏的,会报错不准发.

现在要用 kiss 新架构实现这个, 有哪些经验教训? 你对这些新点子怎么看?

---

### 🤖 ChatGPT

## 总判断

**我赞成推倒当前重构方案，改做这套新架构。**

它比旧万象术更接近真正的 KISS：

- 
   不再把异步子代理伪装成同步工具。
   
- 
   不再把普通控制流持久化成 Stage、Phase、Lease、Owner、Generation。
   
- 
   不再依赖 todowrite 充当调度器和上下文存储。
   
- 
   不再维护一个独立的万象阵 DAG 平台。
   
- 
   Manager 只负责协调，专业代理负责执行。
   
- 
   Git、子会话、进程、模型输出各自保留真实语义，不再塞进一锅“全局状态”。
   

现有 README 所描述的产品核心仍是同步子代理、todowrite、方法论联动、SubsessionActor、通用 nudge、模糊工具和独立万象阵；你的新方案不是增量升级，而是**产品模型整体换代**，不应继续兼容旧概念。

不过目前方案还有 **6 个必须纠正的协议漏洞**。不先修正，新的实现仍会在半年后重新长出复杂状态机。

---

## 一、B 版工作记录：这是整个方案最有价值的创新

### 1. 它解决的是“认知连续性”，而不是普通摘要

你的 blogger 有三个优点：

1. 
   **始终在主链路之外运行**，不增加 X 的响应延迟。
   
2. 
   **增量写作而不是临时压缩**，不会在即将溢出时才慌忙读几十万 token。
   
3. 
   越久远的信息经过越多次压缩，越近的信息越完整，确实自然符合人的记忆规律。
   

这比“上下文到 90% 后启动 compaction agent”优雅得多。

OpenCode 当前的正式 compaction 流程仍会调用一次专门的 compaction 模型；`experimental.session.compacting` 只能修改它的 prompt 或注入 context，不能直接把已有 B 版作为最终 summary 返回。因此，“真正零模型调用的替换”目前不能假定插件接口天然支持，Phase 0 必须做宿主 spike；否则需要在消息送入模型前增加自己的 context projection 边界。

### 2. 最大漏洞：压缩时不能只放 B，必须放“B + 未覆盖尾部”

假设：

- 
   Blogger 已经总结到消息游标 100。
   
- 
   X 又产生了 101～108。
   
- 
   Y 正在工作，按照你的规则不排队。
   
- 
   此时 X 突然溢出。
   

如果直接用当前 B 替换 X 全部上下文，那么 101～108 会永久丢失。

正确的不变量必须是：

```
压缩后的 X 上下文
= B 版记录（覆盖至 cursor=C）
+ 原始尾部消息 (C, current]
```

因此每一版 B 必须携带一个由运行时赋予的水位：

```
BlogCheckpoint {
    through: TranscriptCursor
    text: string
}
```

这里的 `through` 不能让 blogger 自己生成，也不能根据文本比较猜测。

于是你真正获得的是：

> 
> **零等待压缩，而不是仅靠 B 压缩。**
> 

B 落后一两轮没有关系，未总结部分原样跟在后面。等 blogger 下一次完成，再把尾部吸收进去。

### 3. Delta 不能是“比上次增加的字符串”

必须按稳定消息身份切片：

```
delta = transcript events in (lastAcknowledgedCursor, currentCursor]
```

OpenCode 的 `message.updated` 可能对同一消息发布多次，插件事件处理还是 fire-and-forget；依赖到达次数或文本 diff 会产生重复、乱序和漏记。现有调查已经确认应按 message ID、part ID、event seq 去重，并串行投递到自己的队列。

建议内部使用：

```
TranscriptCursor =
    messageId
    + partId
    + stable revision/finalized marker
```

给 blogger 展示时可以渲染成 YAML，但 **YAML 只是表示层，不是内部事实格式**。

尤其是代码、终端输出、用户输入中可能包含 `---`、冒号、多行字符串甚至伪造指令。使用 YAML 时，所有原始内容必须放在 literal block 中，并明确标记为不可信 transcript：

```
- kind: tool_result
  message_id: msg_x
  part_id: prt_y
  content: |-
    ...
```

### 4. “B 是 Y 所有输出的拼接”仍然会无限增长

这里需要稍微修正定义。

应区分：

```
BlogSegments   = 自上次重基以来的新段落
BlogCheckpoint = 已重基的完整 B 版记录
B              = BlogCheckpoint + BlogSegments
```

当 Y 自己接近溢出时，执行同一套操作：

```
输入：当前完整 B
要求：重写成一份可独立使用、更紧凑的工作记录
成功后：
    BlogCheckpoint = 新输出
    BlogSegments = []
```

如果只是把旧 B 当作普通 delta，再要求“写一个新段落”，那么新段落只会评论旧 B，而不会自动替代旧 B；旧内容依然存在，空间并没有释放。

这不是要建立 CompactionState 状态机，只需要一个明确的分支：

```
AppendDelta
RebaseCheckpoint
```

两个普通函数足够。

### 5. B 不能成为事实源

Blogger 会遗漏，也可能误解。因此：

- 
   B 可以告诉模型“目前大概做到了哪里”。
   
- 
   B 不能决定 child 是否仍在运行。
   
- 
   B 不能决定 reviewer 是否已经 PERFECT。
   
- 
   B 不能决定某文件是否真的修改。
   
- 
   B 不能决定某命令是否通过。
   

权威来源仍然分别是：

| 事项 | 权威来源 |
| :--- | :--- |
| 活跃代理、未领取结果 | fork runtime |
| 代码内容 | 工作区文件 |
| 完成变更 | Git tree/commit |
| 审查结论 | reviewer 的结构化 verdict |
| 命令结果 | process result |
| 用户要求 | 原始当前请求 + B 记录 |

这是从旧架构得到的最重要教训：

> 
> **文本负责认知，结构化值负责控制。**
> 

原来的很多复杂度正是因为把模型文案、idle、todo、nudge 和协议状态互相解释。KISS 文档本身已经要求只持久化外部事实，不把“程序运行到哪一步”伪装成领域状态。

### 6. “包含思考”不应成为正确性前提

可以把宿主公开暴露的 reasoning part 提供给 blogger，但必须遵守：

- 
   宿主不提供 reasoning 时也完全可运行。
   
- 
   流式未结束的 reasoning 不进入已确认 delta。
   
- 
   B 不保留原始思维流，只保留可验证的结论、尝试、失败与决策。
   
- 
   不把 reasoning 当作恢复协议。
   

更稳妥的 blogger 提示词应是：

> 
> 根据可观察 transcript 记录目标、决策、动作、结果、错误、未决问题和精确标识符。不要复述代码，不要记录思维流，不要把推测写成事实。
> 

---

## 二、fork / join：方向完全正确，但必须补齐运行身份

### 1. 两个控制动词足够表达任意动态 DAG

Manager：

```
fork → fork → fork → join → 根据结果继续 fork → join
```

已经能表达：

- 
   并行调查
   
- 
   串行依赖
   
- 
   投机执行
   
- 
   多轮修订
   
- 
   Map/Reduce
   
- 
   审查闭环
   
- 
   动态任务分解
   

所以从**表达能力**看，orchestrator 确实比固定万象阵 DAG 更强。

但应准确描述为：

> 
> 表达能力更强，天然恢复能力更弱。
> 

旧万象阵的 DAG 是显式数据；新的依赖图存在于 orchestrator 的对话和当前 fork 表中。进程重启后是否恢复，需要单独决定，不能指望 B 版工作记录自动变成可靠调度图。

### 2. `join()` 必须消费一个完成邮箱

正确语义：

```
已有未领取 completion：
    立即弹出最早的一项

没有 completion，但仍有运行任务：
    等待下一项

没有运行任务，也没有 completion：
    立即返回 EMPTY，而不是永久等待
```

completion 必须“先入箱、后 join”，不能让 `join()` 临时订阅 terminal。否则极快完成的子代理可能在 join 前返回，结果永久丢失。

这与现有 KISS 子代理文档中“waiter 必须先于 send 注册”的经验完全一致。

### 3. 结果不能只有六位 agent ID

同一个 agent 可以被继续训话，所以要区分：

```
AgentId = 物理伴随子会话
RunId   = 该子会话本次请求
```

`join()` 至少返回：

```
{
  agent_id,
  run_id,
  role,
  status,
  output,       // 本 Run 的 A 版工作记录
  verdict?      // reviewer 才有
}
```

否则会出现：

- 
   同一 coder 第一次结果晚到，却被误认为第二次训话结果。
   
- 
   reviewer 的旧 verdict 覆盖新一轮审查。
   
- 
   cancel 后迟到输出被当成当前输出。
   

OpenCode 取消后仍可能出现迟到或重复事件，所以必须以本地 RunId 判定“这是否仍属于当前 run”，不能靠 idle 或事件先后。

六位十六进制 ID 可以保留为 UI 简写，但生成时必须检测碰撞。内部仍用完整随机 ID。

### 4. 对正在运行的同一 agent 再 fork：应失败，不应排队

建议：

```
fork(agent=hex, prompt=...)
```

仅当该 agent 处于 Idle 时接受。

如果正在运行：

```
BUSY: join the current run before continuing this agent
```

不要偷偷排队。否则会再次出现：

- 
   取消到底取消当前还是排队任务；
   
- 
   fallback 属于哪次 prompt；
   
- 
   训话是否覆盖旧训话；
   
- 
   用户中断后哪些 pending 仍有效。
   

你已经在 blogger 中选择了“不排队、合并后续 delta”，子代理也应该遵循同样精神。

### 5. 生命周期要保持二分

继续保留现有 KISS-07 中最重要的一条：

| 对象 | 寿命 |
| :--- | :--- |
| 物理 agent session | 可多次继续 |
| 单次 Run attachment | 一次 fork 到 terminal |

`join()` 消费的是 Run completion，不是销毁 agent。只有父 session 结束、明确 close 或用户 abort 时才关闭物理 agent。

---

## 三、角色拓扑：大体优秀，但 executor 命名必须改

### 1. Manager 无文件工具是正确的

Manager 只有：

```
fork
join
list
```

这是本次设计最重要的约束之一。

它迫使 Manager：

- 
   不亲自钻进文件细节；
   
- 
   不在调查、编码、测试之间频繁切换身份；
   
- 
   不把自己的上下文塞满代码和终端输出；
   
- 
   真正成为协调器。
   

Manager 的 B 版记录也因此会非常高质量：主要是需求、决策、分派和结果，而不是数万行工具噪音。

### 2. `executor` 同时作为 agent 名和命令工具名，非常危险

你的设计里同时有：

```
fork(agent=executor)
inspector 调用 executor(command=...)
```

同一个词表达：

1. 
   一个无工具、只能说话的模型角色；
   
2. 
   一个真正启动系统进程的工具。
   

日志、提示词、权限矩阵、错误信息都会混乱。

建议二选一：

| 对象 | 建议名称 |
| :--- | :--- |
| 无工具思考型代理 | `advisor` / `analyst` |
| 进程执行工具 | `exec` / `command` |

这是必须改，不是美观问题。

### 3. Coder 同步调用 Inspector 是合理的例外

总体结构变成：

```
Manager
  └─异步→ Coder
              └─同步→ Inspector
                          └─同步→ Command
```

这里同步是合理的，因为它处于一个明确的局部操作中：

- 
   Coder 需要查一个事实后继续改代码。
   
- 
   Inspector 需要命令结果后生成调查结论。
   
- 
   不涉及 Manager 同时协调多个长期工作。
   

建议 coder 每次 inspector 调用都创建**一次性 inspector session**，不要让一个持久 inspector 同时接收并行调用。多个调用自然并发，各自独立。

### 4. 伴随关系建议冻结为静态表

| 角色 | 伴随 blogger |
| :--- | :--- |
| orchestrator | 是 |
| manager | 是 |
| coder | 是 |
| advisor/executor agent | 否 |
| inspector | 否 |
| browser | 否 |
| meditator | 否 |
| reviewer | 否 |
| blogger | 否 |

不要做配置化“任意角色可开 blogger”。静态装配更容易证明没有递归伴随和成本爆炸。

---

## 四、Inspector / Command：总体正确，但“estimated”实际是预算申请

### 1. 三倍规则可以保留，但要换一种理解

LLM 给出的：

```
estimated_output_bytes
estimated_running_secs
estimated_mem_usage
```

不能被信任为准确预测。

运行时实际应计算：

```
output_limit = clamp(estimated_output_bytes * 3, minimum, global_maximum)
deadline     = now + clamp(estimated_running_secs * 3, minimum, global_maximum)
```

所以它本质上是：

> 
> 子代理申请多少资源，运行时给三倍容忍，但仍受全局硬上限。
> 

不要让模型通过填一个极大 estimate 绕过系统上限。

### 2. 所有阶段共享一个绝对 Deadline

必须沿用 KISS-05 已经总结出的经验：

```
spawn
→ 写 stdin
→ 等退出
→ kill
→ drain stdout/stderr
→ dispose
```

全部消费同一个绝对过期时刻，不能每层重新获得“3 × estimated_running_secs”。

否则又会重现原来多层 timeout、取消不收敛、锁中毒的问题。现有 KISS Process 规范还要求 Spawn 返回前安装 stdout/stderr pump，超时后杀整个进程树并等待 pump 收尾。

### 3. 输出超过三倍时，“触发摘要”需要定义物理实现

不能先把无限输出全部读入内存，再发现超限。

正确路径：

```
stdout/stderr pump
→ 达到 soft limit 后不再内存累积
→ 继续有界写入临时 spool 文件
→ 命令退出
→ 交给一次性无工具 summarizer
→ 返回摘要 + byte count + truncation/spool diagnostics
```

还必须有绝对 hard limit。达到 hard limit 后可以：

- 
   停止保留后续内容但继续 drain；
   
- 
   或直接杀命令。
   

不应实现无限 spool。

### 4. large 全局只能一个：赞成

实现只需一个进程级 semaphore：

```
largeCommandGate = SemaphoreSlim(1)
```

要求：

- 
   等待 gate 受 CancellationToken 和 Deadline 控制；
   
- 
   成功、错误、取消、kill 都在 finally 释放；
   
- 
   不写日志、不持久化、不建立 MemoryLease 状态机；
   
- 
   medium 使用另一个固定并发上限。
   

这是资源限制，不是领域状态。

### 5. 删除 Python/JS 进程内 execute：赞成

可以通过普通 command 启动 `python` 或 `node`，但插件本身不再提供特殊的 in-process 执行后门。

这样所有执行统一经过：

- 
   工作目录
   
- 
   环境变量
   
- 
   stdout/stderr pump
   
- 
   资源预算
   
- 
   kill tree
   
- 
   权限控制
   

---

## 五、Fallback 和智能催促

### 1. Fallback 要留，但只能留在一次模型请求内部

对每个 agent run：

```
try current model
→ retryable failure：同模有界重试
→ 明确未接受：切下一模型
→ acceptance unknown：停止，先 reconcile
→ 成功：结束
```

不要持久化：

- 
   当前第几个模型；
   
- 
   当前 retry phase；
   
- 
   remaining models；
   
- 
   fallback owner；
   
- 
   fallback lease。
   

这些只是一次 `runTurn` 的局部变量。现有 KISS-09 已经明确总结了：AcceptanceUnknown 时禁止盲目切模型，Fallback 应是局部尾递归而不是独立状态机。

### 2. “智能催促”应改名为 ReviewGuard

旧 Nudge 最大的问题是它最终变成了第二个调度器。

你现在只保留审查闭环，完全合理，但不要再沿用通用 nudge 架构。实现应只是 Manager 即将结束时的一道纯门禁：

```
allowFinish =
    worktreeTreeHash = lastPerfectReviewTreeHash
```

如果不满足：

```
注入一次系统消息：
当前工作树尚未经过 reviewer PERFECT，
或 PERFECT 后又发生了修改。继续审查循环。
```

然后重新运行 Manager。

这不依赖 todo，不监听 idle，不扫描候选动作，不抢占 prompt，不需要 lease。

还要设置总上限，例如：

```
MaxReviewGuardRounds
```

耗尽后明确失败，不能无限自催。

### 3. Reviewer 的协议设计很好

Reviewer：

- 
   正文输出详细意见；
   
- 
   `verdict` 工具只接受 `PERFECT | REVISE`；
   
- 
   join 中将正文作为 A 版工作记录；
   
- 
   verdict 作为结构化字段单独返回。
   

必须规定：

- 
   每个 reviewer run 恰好一个 verdict；
   
- 
   没有 verdict 或多个冲突 verdict → `INVALID`；
   
- 
   `PERFECT` 绑定审查时的 Git tree hash；
   
- 
   之后任意文件变化立即使 PERFECT 失效。
   

这比从 reviewer 文案中正则提取结论可靠得多。

---

## 六、PTY：统一表面可以，魔法字符串不可以

把 PTY 与 agent 一起显示在 `list()` 中是好想法：

```
a1b2c3  coder      running
d4e5f6  reviewer   ready
102abc  pty        running
```

但内部不要强迫它们共享同一生命周期实现：

- 
   agent 是可重复 Run 的会话；
   
- 
   PTY 是有 stdin/stdout 和进程退出的资源。
   

“一视同仁”应只发生在**句柄、list、completion mailbox** 层。

以下协议不建议采用：

```
"[#SIGTERM]"
"[#SIGKILL]"
```

这是典型的“把协议藏在字符串里”。模型、用户输入或真实终端文本都可能意外碰撞。

仍然可以只保留一个 `fork` 工具，但增加结构化字段：

```
fork(agent="pty", prompt="command")       // 创建
fork(agent="102abc", prompt="text")       // 写入
fork(agent="102abc", prompt="")           // 发起读取
fork(agent="102abc", signal="TERM")       // 信号
fork(agent="102abc", signal="KILL")
```

`signal` 使用 enum。这样工具数量没有增加，也没有魔法文本。

每次 PTY read/write/signal 都拥有自己的 RunId；`join()` 返回对应 operation completion。PTY 自然退出时另发一个 `pty_exit` completion。

---

## 七、Orchestrator：确实可以取代万象阵，但发布协议必须更严格

### 1. 新 Orchestrator 的本质

它不是 DAG Scheduler，而是：

> 
> **能够动态 fork ManagerJob 的上层 Manager。**
> 

每个 `fork(manager)` 背后由运行时完成完整资源流程：

```
创建 branch/worktree
→ 启动 manager
→ 自动进入 ReviewGuard
→ manager 完成且当前 tree 获得 PERFECT
→ 准备发布
→ rebase
→ 再验证
→ ff
→ join 向 orchestrator 返回
```

Orchestrator 只看到 fork/join，非常干净。

### 2. “先审查，再 rebase，再 ff”还不够

多个 manager 并行时：

```
M1 和 M2 都基于 HEAD=H0
M1 先合入，目标变成 H1
M2 必须 rebase 到 H1
```

即使 rebase 没有文本冲突，M2 原先的测试和审查也只针对 H0。

所以必须是：

```
并行阶段：
    manager 工作
    reviewer 对原 worktree PERFECT

串行发布阶段：
    获取短 integration gate
    检查目标工作区 clean
    rebase 到最新目标
    重新运行 reviewer / 验证
    ff
    释放 gate
```

如果 rebase 冲突：

- 
   保留 worktree；
   
- 
   恢复同一个 manager；
   
- 
   把冲突与新基线告诉它；
   
- 
   重新进入 ReviewGuard；
   
- 
   不向 orchestrator 报“已完成”。
   

现有 KISS-12 已经确认共享 Git ref 的 FF 必须串行；你的新架构应保留这条经验，只删除显式 DAG、wave、scheduler 等框架。

### 3. ManagerJob 完成最好由运行时自动提交

建议不要求 coder 或 manager 手工管理 Git commit。

运行时在首次 PERFECT 后：

```
检查工作树
→ stage 允许的变更
→ 自动生成 commit
→ rebase
→ post-rebase review
→ ff
```

审查绑定的是 tree hash，所以创建 commit 不会使审查失效；rebase 后则必须重新验证。

### 4. 脏工作区门禁合理，但必须定义豁免

用户给 orchestrator 发消息前：

```
git status --porcelain --untracked-files=all
```

非空即拒绝。

但插件自身的文件不能放在根工作区中导致永远 dirty：

- 
   worktree 放在仓库外的 sibling 目录或 Git common dir 下；
   
- 
   runtime 日志、临时 spool、状态文件放在 `.git` 或系统 cache；
   
- 
   不依赖 `.gitignore` 偷偷隐藏用户真正的文件。
   

发布前还要重新检查，而不仅是用户输入时检查。

---

## 八、推荐的最小正式架构

不要再建立“万象术领域大平台”。第一版只需要下面几个模块：

```
HostAdapter
  ├─ TranscriptProjector
  ├─ ContextProjector
  └─ SessionTerminalAdapter

Companion
  ├─ DeltaRenderer
  ├─ BlogCheckpoint
  └─ CompanionRunner

ForkRuntime
  ├─ ForkTable
  ├─ CompletionMailbox
  ├─ AgentRunner
  └─ ParentCancellationScope

Roles
  ├─ Manager
  ├─ Coder
  ├─ Inspector
  ├─ Browser
  ├─ Meditator
  ├─ Reviewer
  └─ Advisor

Process
  ├─ CommandRunner
  ├─ OutputPump
  ├─ LargeCommandGate
  └─ PtyRunner

Review
  └─ ReviewGuard

Orchestrator
  ├─ ManagerJob
  ├─ WorktreeResource
  └─ SerialPublisher

Fallback
  └─ RunTurnWithFallback
```

不要出现：

```
AgentCoordinator
AgentLifecycleStateMachine
ForkStage
JoinPhase
CompletionLease
ReviewNudgeOwner
BlogGenerationManager
OrchestratorDAGRegistry
```

内部不可避免的几种状态，例如 process 的 `Running/Killing/Exited`、agent 的 `Idle/Running/Closed`，留在资源实现文件即可，不进入业务 Journal。

---

## 九、必须冻结的 15 条不变量

1. 
   X 压缩永远是 `B through C + raw tail after C`。
   
2. 
   Blogger 失败永不阻塞 X。
   
3. 
   Delta 由稳定 cursor 产生，不做文本 diff。
   
4. 
   B 是认知缓存，不是控制事实。
   
5. 
   一个 agent 同时最多一个 Run。
   
6. 
   每个 Run 都有独立 RunId。
   
7. 
   completion 先入邮箱，join 后消费。
   
8. 
   join 不会丢 fast completion。
   
9. 
   没有 active/ready 时 join 明确返回 EMPTY。
   
10. 
   父 session abort 会有界取消全部子 Run 和 PTY。
   
11. 
   Fallback 只在单次 Run 内，AcceptanceUnknown 不换模型。
   
12. 
   Reviewer PERFECT 绑定精确 Git tree hash。
   
13. 
   PERFECT 后发生任何修改都必须重审。
   
14. 
   共享目标 ref 的发布严格串行。
   
15. 
   rebase 后必须重新验证再 ff。
   

---

## 十、实施顺序

### Phase 0：宿主能力 spike

只验证四件事：

1. 
   能否在每次模型请求前拿到确定的完整 transcript projection。
   
2. 
   能否不调用 compaction 模型，直接替换历史消息。
   
3. 
   tool/reasoning/message part 的最终化和 ID 时序。
   
4. 
   child terminal、abort、迟到事件如何可靠相关。
   

这里不通过，不能开始 blogger 主体。

### Phase 1：ForkRuntime

只实现：

```
fork(no-tool agent)
join
list
RunId
completion mailbox
parent cancel
fallback
```

先杀死 fast-completion、cancel-late-result、busy-continue、join-empty 四类 bug。

### Phase 2：Companion

实现：

```
cursor
delta rendering
busy coalescing
B + raw tail
blogger self-rebase
failure isolation
```

先只接 manager。

### Phase 3：Inspector 和 Command

完成：

- 
   单绝对 Deadline；
   
- 
   pump-before-return；
   
- 
   输出软硬上限；
   
- 
   large 全局 gate；
   
- 
   kill process tree；
   
- 
   无孤儿、无 poison。
   

### Phase 4：角色矩阵

依次接：

```
coder → browser → meditator → reviewer
```

不要一次性实现全部角色。

### Phase 5：ReviewGuard

用 Git tree hash 闭合 `/loop`，彻底删除 todo、select_methodology 和通用 nudge。

### Phase 6：PTY

接入统一 handle/list/completion 表面，但保留独立 Process 实现。

### Phase 7：Orchestrator

先实现单 manager worktree 完整发布，再实现两个 manager 并行和串行 rebase/ff。不要先做动态多层并发。

### Phase 8：删除旧实现

当新架构覆盖：

- 
   普通编码；
   
- 
   并行调查；
   
- 
   reviewer 循环；
   
- 
   fallback；
   
- 
   context replacement；
   
- 
   PTY；
   
- 
   多 worktree manager；
   

即可一次性删除：

```
todowrite SSOT
select_methodology
SubsessionActor 旧层
同步 coder/inspector/browser 工具
通用 nudge
fuzzy_*
万象阵 DAG/Scheduler/HTTP 控制面
旧 ContextBudget/Compaction 协调器
```

---

## 最终评价

这套新设计的核心思想是对的，而且比当前 KISS 文档里仍然保留的 Session Flow、Todo Flow、Review Flow 和 Squad Flow 更进一步：

> 
> **不再由插件替模型保存“下一步应该做什么”，而是让模型通过 fork/join 直接表达控制流；插件只负责资源、身份、完成邮箱、上下文投影和 Git 发布。**
> 

真正应该坚持的版本是：

```
异步 fork/join
+ 伴随 blogger
+ B 水位与原始尾部
+ 静态角色权限
+ 结构化 reviewer verdict
+ Git tree hash ReviewGuard
+ 串行 rebase/verify/ff
```

需要明确否决的只有四点：

```
只用 B 压缩而不带 raw tail
把所有 blogger 输出永久拼接
executor 角色与 executor 工具同名
用 [#SIGTERM] 一类魔法字符串控制 PTY
```

修正这四点后，这不是“另一次重构”，而是一套足以成为万象术正式版基础的全新产品架构。

---

### 👤 User

我会把 opencode 官方的压缩关掉, 上下文快满了就把 B 版等价替换前缀 (通过投影,并且我记住这件事,以后次次都投影替换). delta 是在投影时候做的,通过本次投影输入和上次投影输出, 在 json 层面做 delta 即可. B 不包括 Y 的输入,只包括 Y 的输出,因此旧的 B 成为 Y 的输入后,再次 B' 时候就没了. subagent 结果产生几乎立刻到达,不会延迟, 因此天然在物理时间层面保序,偶尔乱序无影响, 只要流程对即可,语义大概率对就行了. 对正在运行的同一 agent 再 fork 是 nudge 语义, 就是直接发 prompt fire forget, 系统自然会在下次 llm 请求尾部加入, 排队时间不长, 而且 fire forget 不管排不排队. executor 角色, 只作为 executor 工具的 summerizer, 语义是清晰的.coder 每次 inspector 调用都当然创建一次性 inspector session. 模型通过填一个极大 estimate 绕过系统上限是允许的. 我们不是保姆,要信任,而且也许模型就是想要这样. 3 × estimated_running_secs 就是进程的上限, 别的层面不设置 timeout, SIGKILL 应该无论如何都能确保回来, 如果 SIGKILL 都回不来,那是 bug 应该暴露而不应该兜底. 输出超过三倍时，“触发摘要”需要定义物理实现这个我赞成, 就是定为 200KB. medium 不限制并发. Fallback 要留, 但简化为 A/B 角, 优先原模型重试, 重试再失败, 永久换角色, B 角色失败再失败, 就死掉. 每 session 共 4 次失败后真死. ReviewGuard 既是 Manager 的 Guard 也要有 Reviewer 不返回的 Guard. verdict 工具仍然是 REVISE 立刻生效, PERFECT 要两次 verdict 生效 (确认机制).仍然可以只保留一个 fork 工具，但增加结构化字段, 同意.七、Orchestrator你说的都对.

但是 KISS 最大的经验是,用 DSL 取代状态机,写结构化的程序,而不是 用状态机人工展平.因此, 请你设计类似 KISS-XX 的文档, 保姆式说清楚如何 结构化 DSL 化语法糖化实现.

---

### 🤖 ChatGPT

现有 KISS 文档已经确立了最重要的原则：把 Stage、Phase、Lease、Owner 等“伪程序计数器”还原成结构化程序；但当前 README 描述的产品表面仍包含 todowrite、通用 nudge、SubsessionActor、同步子代理工具与万象阵。新文档因此不是修补旧方案，而是把该原则贯彻到底。 

我已经按你刚刚冻结的语义写成 **12 卷、约 6.6 万字节**的 KISS-N 系列。文件先加 `N` 前缀，避免尚未批准就覆盖原 KISS；正式实施时可取代旧卷。

## 新架构的核心实现判断

这次要同时建立两层 DSL。

第一层是模型可见的极小 Agent DSL：

```
Manager:      fork / join / list
Orchestrator: fork / join
Reviewer:     verdict(PERFECT | REVISE)
```

第二层是实现者使用的 F# Structured Program DSL：

```
agent { ... }
companion { ... }
process { ... }
review { ... }
orchestrator { ... }
```

所有 Builder 都建立在同一个可执行闭包内核上，不建立 AST、解释器、Workflow Engine 或动态 Stage Registry：

```
type Flow<'ctx, 'error, 'a> =
    private
    | Flow of ('ctx -> CancellationToken -> Task<Result<'a, 'error>>)
```

控制流只使用：

```
let!       等待领域动作
do!        执行领域动作
use!       持有真实异步资源
match      处理真实结果
while      表达条件循环
尾递归     表达重试、确认、找到即停
parallel   表达相互独立的局部并发
```

新架构中允许存在真实值，例如正在运行的 `Task`、完成邮箱 `Channel`、Process/PTY handle、当前 B、JSON delta 基线、A/B 模型侧和累计失败数；禁止存在 `ReviewPhase`、`FallbackStage`、`JoinOwner`、`NudgeLease`、`CompactionGeneration` 等人工展开的控制状态。

### Companion 的结构化主程序

```
Host 输入
→ canonical JSON 投影
→ 与最近一次成功 Blogger 基线做 JSON delta
→ Y 空闲则启动一次 Blogger；Y 忙则直接跳过
→ 接近上限后启用 remembered prefix replacement
→ B 替换已被 BlogBase 覆盖的前缀
→ 未覆盖后缀原样保留
→ 返回 X 的模型输入
```

不需要事件增量器、消息游标协议或 pending delta 队列。

Y 忙时不推进 `BlogBase`，所以它空闲后的下一次 JSON delta 自然包含此前跳过的多轮内容。

B 只由 Y 的 assistant 正文构成。Y 自身需要压缩时，旧 B 变成 Y 的输入，新 assistant 输出 B'；因此旧 B 自然不再属于 B 输出，无需删除状态或 compaction generation。

### Fork/Join 的结构化主程序

```
let forkNew role prompt =
    agent {
        let! child = createChild role parentB
        let handle = register child
        attachTerminalListener handle
        do! sendPrompt child prompt
        return Forked handle.Id
    }
```

已有 Agent ID 再 `fork`：

```
let nudgeExisting child prompt =
    agent {
        postPromptFireAndForget child.SessionId prompt
        return Nudged child.Id
    }
```

`join()` 只是读取完成邮箱：

```
let joinAny () =
    agent {
        let! result = completionChannel.Reader.ReadAsync(ct)
        return result
    }
```

不指定 child，不排序，不建立 waiter registry。偶发完成顺序差异直接交给 Manager 理解。

### Process 的结构化主程序

```
let runExecutor request =
    process {
        use! largeLease =
            match request.EstimatedMemoryUsage with
            | Medium -> noLease
            | Large -> acquireGlobalLargeLease ()

        use! child = spawnWithPumps request
        let! result =
            waitOrKill child
                (request.EstimatedRunningSeconds * 3)

        return! maybeSummarize request result
    }
```

这里严格采用你的取舍：

```
唯一进程时限 = 3 × estimated_running_secs
巨大 estimate 合法，不做隐藏 clamp
Medium 不限并发
Large 全进程同时一个
SIGKILL 后不再叠加清理 timeout
无法物理返回就是必须暴露的实现 bug
```

命令输出超过 `3 × estimated_output_bytes` 后写 spool，并以 200KB 为块交给无工具 Executor Agent 做 map/reduce 摘要。200KB 是摘要批尺寸，不是全局输出上限。

### Fallback 的结构化主程序

每个 Session 只有：

```
type ModelSide = A | B

type FallbackMemory =
    { mutable Side: ModelSide
      mutable Failures: int }
```

递归规则：

```
第 1 次失败：A 重试
第 2 次失败：永久切 B，并立即尝试
第 3 次失败：B 重试
第 4 次失败：SessionDead
```

成功不清空累计失败，也不切回 A。

没有 RemainingModels、FallbackPhase、Governor、Attempt Registry 或 fallback journal。

### ReviewGuard 的结构化主程序

Reviewer：

```
REVISE
→ 第一次立即生效

PERFECT
→ 第一次工具返回“请再次确认”
→ 第二次连续 PERFECT 且 Git tree 未变
→ ConfirmedPerfect(treeHash)
```

Reviewer assistant terminal 但没有有效 verdict：

```
→ 直接 nudge 同一 Reviewer Session
→ 继续等待 verdict
```

Manager assistant 尝试结束但当前 tree 没有双 PERFECT：

```
→ Guard 向同一 Manager 追加提示
→ Manager 继续 fork/join 审查
```

Guard 不读取 todo，不扫描 next action，不建立 Nudge Coordinator。

### Orchestrator 的结构化主程序

```
用户消息前检查目标工作区 clean
→ fork ManagerJob 时创建隔离 worktree
→ Manager 自动进入 ReviewGuard
→ Manager 在 worktree 中工作并获得初次双 PERFECT
→ 形成 candidate commit
→ 获取目标 ref 的短 integration semaphore
→ rebase 到最新目标
→ 冲突交回同一 Manager
→ rebase 后再次双 PERFECT
→ fast-forward
→ join 才返回 published
```

工作并行，发布串行；不再建立 DAG、wave、Scheduler、HTTP 控制面或 Squad 状态机。

### 分卷内容

| 文档 | 实现重点 |
| :--- | :--- |
| `KISS-N00` | 两层 DSL 与状态机断根 |
| `KISS-N01` | 统一 Flow 内核、CE、尾递归、资源作用域 |
| `KISS-N02` | JSON delta、B、前缀投影、Y 自压缩 |
| `KISS-N03` | fork/join/list、nudge、完成邮箱 |
| `KISS-N04` | 完整角色及静态能力矩阵 |
| `KISS-N05` | Process、200KB 摘要、Large Gate、PTY |
| `KISS-N06` | 累计四失败 A/B Fallback |
| `KISS-N07` | Manager/Reviewer Guard、双 PERFECT |
| `KISS-N08` | Worktree、rebase、复审、串行 FF |
| `KISS-N09` | OpenCode Adapter 与纯投影管线 |
| `KISS-N10` | 十阶段实施、测试矩阵、删除清单 |
| `KISS-NEXT` | 总索引和全部冻结决策 |

文档中还专门纠正了两个很容易写错的资源时序：

- 
   子代理必须 **先安装 terminal listener，再发送 prompt**。
   
- 
   `fork Manager` 不能在返回 handle 前 `use! worktree`；worktree 所有权必须转交后台 ManagerJob，由整个发布程序的 `use!` 最终释放。
   

### 文件

这套文档已经可以直接交给工程师从 `Phase 0 — GuideContract 真编译` 开始实施；不需要再先设计另一层架构。


---

*Exported by [AI Chat Exporter for ChatGPT](https://github.com/Rajat-XR/ai-chat-exporter) · July 23, 2026 at 11:45 PM*
