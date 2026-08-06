# 验证原则

条款前缀：`VERIFY-`。  
本文件是 **proof 唯一权威**：应运行什么、证明什么、失败意味着什么。  
运行证据在 CI artifact，不进 Git 工作树。实现命令入口见 `AGENTS.md` / `npm run check*`。

`VERIFY-*` 定义不得迁出本文件（检查器与测试锚点依赖）。

## 阅读地图

| 条款 | 回答的问题 |
|------|------------|
| VERIFY-001 | 测试金字塔层级（0–5） |
| VERIFY-002 | 晋级阶梯，禁止跨级 |
| VERIFY-003 | Canary mock 剧本：键、匹配、幂等、故障轴、冷边界 |
| VERIFY-004 | 因果推进门禁与**禁止退化清单**（机器解析锚点） |
| VERIFY-005 | Architecture gates / 单一写入口 / Host 边界 |
| VERIFY-006 | No-Go 发布否决 |
| VERIFY-007 | Wire / Semantic / BloggerDelta 三种投影 |
| VERIFY-008 | 测试语言边界（`.fs` 生产 / `.mjs` 契约面） |

读顺序建议：001→002 定层；008 定入口；003+007 定 canary；004 定挂死语义；005+006 定发布门。

## VERIFY-001：测试金字塔

```text
0. 静态检查（规范一致性、旧符号灭绝、架构门禁）— 不是测试，不需要产物
1. 纯函数测试（Fallback fold、authority fold、review witness）
2. 资源契约测试（Flow Using、Completion Channel、Process pumps）
3. Fake Host 轨迹（blogger busy skip、nudge、fallback、guard）
4. OpenCode E2E（canary，real OpenCode Host + mock provider）
5. 发布门禁（三轮 × 全部 e2e + package + packing）
```

第 0 层是纯文本与文件系统检查，永远不依赖编译产物，因此在任何阶段都可运行。第 1–3 层的语言与入口由 VERIFY-008 规定。

## VERIFY-002：五级晋级阶梯

不允许跨级。

1. 纯状态测试：不涉及 Host、事件、网络
2. 单边界集成：一次 Host signal -> 一次 durable fact -> 一次 dispatcher
3. 录制事件重放：确定性重放，不依赖真实 SSE
4. 单 canary：`CANARY_REPEAT=1`
5. 发布门禁：三轮 x 完整 check:release

## VERIFY-003：Canary Mock 剧本

剧本是 mock 的压缩表示法：压掉重复的对话前缀，不压掉语义。

### 两层形式

```text
书写形式（TOML，对话，人读）
  ↓ 载入期编译，一次性
运行时索引（前缀 → 响应，机器查）
```

编译产物可 dump 供调试，但不是源。人写对话，机器查前缀。

不得让作者手写前缀数组——那会使每条边重复前面所有轮次，加一步要改所有下游边，读者看不出这是一段对话。

### 静态全集

一个 scenario 恰好一个 TOML 文件，在 Host 启动前一次性加载。

禁止：运行期变更剧本（`loadScripts` 之类的中途加载）。理由：剧本若是时间的函数，则不可静态审阅、不可静态校验、同一请求在不同时刻合法性不同，且与纯函数模型直接冲突。

重启不需要新剧本。重启后的 continuation / guard / recovery prompt 追加新消息，语义前缀因此不同，命中不同的对话步。若某 canary 在重启后产生逐字节相同的请求却期望不同响应，它在用隐藏状态区分因果——这是缺陷，不是待支持的特性。

### 运行时键

```text
lane  最长匹配的 head 判别式（可选；缺省单 lane）
turn  最后一条 user 消息的语义内容（ProviderSemanticProjection 前缀匹配）
step  该 user 消息之后的 assistant 消息条数
```

三者皆为请求的纯函数，无外部状态。

`step` 把「同一轮对话的第几步」从 mock 侧游标变成请求内容的可数属性：Host 每个 provider step 追加一条 assistant message（HOST-010 证据）。因此 mock 不需要记账推进。

使用 Semantic 而非 Wire 投影是强制的（VERIFY-007）：canary 跨 Session、跨重启、跨 runtime 复用同一剧本，而 message ID / call ID / timestamp 每次都不同。用 Wire 作键会让剧本永不命中。

### 匹配

```text
最长前缀唯一命中 → 返回该响应
命中 0 条        → fail closed
同长度冲突       → 载入期即拒绝，不留到运行期
```

禁止用排序或打分消解歧义。不得按子串长度、谓词数量、路径下标或任何 specificity 评分挑选「最像的一条」。

分叉只允许来自 provider 可见内容的差异，且同一分叉点的条件必须互斥（载入期校验）。这对应真实因果：模型看到不同的工具结果，说不同的话。

### 幂等

同一 `(lane, turn, step)` 第 N 次出现返回同一响应，N 无上限。

禁止：

```text
turn 编号 / lane cursor / path 下标决定返回哪个响应
「可跳过可选边」之类的推进规则
按已观察次数改变响应
对失败响应删除缓存以便下次返回别的东西
```

任何「同一键在不同时刻返回不同内容」的机制都使 mock 退化成队列，canary 随之失去确定性。

### 故障注入是独立正交轴

provider 失败、SSE 中断、超时、畸形参数属于传输层故障，不是内容。它们不得通过破坏内容幂等性来表达。

```text
内容   ：(lane, turn, step) → Content    纯函数，幂等，无计数
故障   ：(turn, step, 物理 attempt 序号) → Delivery   独立轴，允许计数
```

`Delivery = Ok | ProviderError | Disconnect | Stall | NeverEnd`

物理投递次数是世界上真实可数的东西（每次 HTTP POST 独立），因此故障轴允许计数而不违反纯函数原则。

故障计划必须前置声明、有限、可穷举。这样 Host 重试测试成立而内容仍是纯函数：重试请求的语义前缀不变，内容返回同一响应；故障计划决定这一次是否在传输层失败。

### 冷边界显式声明

前缀缓存不变量（ARCH-004）的合法例外只有两处，且都有正式定义：

```text
COMPANION-009  Epoch 切换：新 SealRoot，允许一次显式 prefix rebase
FALLBACK-004   Fallback 换边：只改 EffectiveAgent，system prompt 因此可变
```

这两处必须由 scenario 显式声明发生位置。禁止由 mock 嗅探请求形状推断（例如「tools 与 system 未变即视为 epoch 切换」）——那会放过在不该切换处发生的切换。

未声明处任何前缀断裂 fail closed。

### 不得重新推导领域概念

mock 只能观察 provider wire 上真实存在的东西，且不得对身份做二次推断。

禁止：

```text
从 tools 数组的形状猜测 CanonicalRole
从 prompt 正文猜测 Agent、Role 或 tier
在 prompt 中埋入仅供测试识别的标记文本
嗅探自定义 HTTP header 以补齐身份
匹配来自其它产品的历史 prose 常量
在匹配前把内容截断到固定长度
```

角色由 `AttemptExecutionProfile`（PROMPT-008）唯一决定。剧本需要区分 lane 时，区分依据必须是语义前缀本身的差异。若两条 lane 在 wire 上不可区分，剧本是欠定的——作者错误，不是 mock 该猜的事。

harness 可以使用 out-of-band session 身份（前缀封印、诊断、路由），但它单向：记账可以观察内容，内容不可观察记账。

### 载入期校验

以下六项在载入期判定，不需要产物、不需要 Host，属 VERIFY-001 第 0 层：

```text
同一 (turn, step) 声明两个不同响应   → 冲突，拒绝载入
两条同长度前缀在同一 turn 下冲突      → 欠定，拒绝载入
fault 引用不存在的 turn 或 step      → 悬空引用，拒绝载入
epoch 引用不存在的步                 → 悬空引用，拒绝载入
must 引用不存在的步                  → 悬空引用，拒绝载入
声明了但任何 flow 都到不了的步         → 死边，拒绝载入
```

最后一条只有静态全集才可能检查——这是禁止运行期加载换来的直接收益。

### 书写形式为 TOML

JSON 不适合人读剧本：无注释、引号噪声、多行文本必须转义、深层嵌套强制大量括号。

剧本是唯一描述「模型会怎么回应」的地方，可读性直接决定它能否被信任。因此书写形式必须为 TOML：

```text
内联表   用于固定形状的小结构（args、when、flow 步骤）
表头     用于可变长序列（turn、step、fault、epoch）
多行字符串 用于长 user 文本，零转义
注释     承载条款引用，写在被约束的那一步旁边
```

`id` 只在被 `flow` / `must` / `fault` / `epoch` 引用时才需要。不得为编号而生成 id。

TOML 语法要求根级键值对先于任何表头，否则会被静默归属到最后一个表。载入器必须硬检查这一点。缩进对 TOML 无语义，需由 formatter 保证全部剧本一致。

## VERIFY-004：因果推进门禁

本条款描述的是原理，不是现有实现。

原理必须继承并发扬，任何重构不得丢弃或降级。现有实现（`tests/e2e/watchdog.js`、`scripts/run-canary-staggered.mjs`、`tests/unit/runner.mjs`、`stability-checker.js`）只是一次并不完善的落地，它不因先存在而获得权威。

判断标准始终是本条款文字，不是当前代码的行为。

### 核心原理：没有进展就杀死，而不是等总超时

错误的做法是给整个套件一个大的 wall-clock 上限，跑完才发现卡住了。它的代价是三重的：

```text
诊断信息在超时点已经腐烂——卡住的原因发生在几分钟前
一个挂死的用例消耗整个 CI 窗口，而不是消耗一个静默窗口
「慢」和「死」不可区分，因为唯一的信号是总时长
```

正确的做法是问「距离上一次真实进展过了多久」。挂死在秒级被发现，诊断在因果现场抓取，慢与死天然可分。

```text
禁止：以套件总时长作为唯一的挂死判据
必须：以「距上次因果进展的静默时长」作为挂死判据
```

wall-clock 上限可以作为兜底存在，但不得是唯一或首要的判据。兜底值必须集中定义，不得散落为字面量。

### 因果 Watchdog

每个 scenario 拥有一个 scenario-local watchdog，静默窗口集中定义为唯一常量。

推进只由语义事件投喂：

```text
算作进展：被消费的剧本步、显式语义检查点（turn 活动、assistant terminal、
          idle-after-activity）、Host 重启的各个阶段
不算进展：原始 SSE、provider HTTP 流量、session.created 之类的生命周期噪声、
          任何「有字节在动」的证据
```

区分标准是：该事件是否证明被测因果链前进了一步。传输层有动静不等于语义有进展——一个反复重连的 SSE 读者能永久续期一个错误的 watchdog。

背景进展（非阻塞车道，例如 blogger sidecar）必须被记录但不得续期：

```text
advance(blocking = true)   → 重置静默计时器
advance(blocking = false)  → 只记录，不重置
```

超时时先转储诊断（事件尾部、待命中的剧本步、最后一次进展的 reason 与 lane、最后一次背景进展距今多久），再退出非零。诊断必须包含「最后一次进展是什么」，否则 watchdog 只是一个更快的超时。

计时器必须不持有事件循环：所有其它句柄关闭后进程自然退出，watchdog 只在仍有东西（挂死的 SSE 读者、泄漏的 server）维持事件循环时开火。这样它不会把干净结束的 scenario 拖到静默窗口结束。

### 覆盖必须无缝

watchdog 装好之前的窗口同样需要因果判据。启动阶段（进程拉起到就绪）必须有独立的就绪判据，不得只靠兜底 wall-clock 覆盖。

```text
禁止：存在一段「只有总超时保护」的时间窗
```

### 单测运行器：超时即遗忘

每个测试有独立的硬超时。超时的测试被判失败并遗忘，运行器继续下一个。

```text
必须：超时后清理该测试的计时器与等待，立即继续
禁止：让被遗弃的测试在稍后 reject，从而掩盖真正的失败
禁止：一个测试超时导致整个套件停摆
```

第二条禁止项是实践中最容易踩的：如果超时只是 reject 了竞态 Promise 而没有关掉后续计时器，那个计时器会在下一个测试运行时开火，把失败归因到无关的测试上。运行器必须有测试覆盖这一点。

若运行器声称「断言投喂心跳」，则该心跳必须真实连通并有测试证明。声明了但未接线的心跳等于没有心跳，且比没有更坏——它让读者相信存在一层不存在的保护。

### 事件交错启动

E2E canary 全并行运行在一个池里。canary N+1 的启动条件是 canary N 输出了精确的就绪标记，而不是固定 sleep。

```text
必须：因果 bark（就绪标记出现即启动下一个）
禁止：按序号计算的固定延迟
```

理由与 watchdog 相同：固定延迟在快机器上浪费时间，在慢机器上仍然撞车，且两种情况都不可诊断。因果启动自动适配机器速度。

配套的两个门禁：

```text
就绪门禁：未在有限窗口内输出就绪标记 → 该 canary 失败（不是放行）
早退门禁：在输出就绪标记之前退出 → 该 canary 失败
```

第二条防止「进程秒退所以看起来很快就绪」被当成成功。

### Release gate

```text
恰好 3 轮，不是「最多 3 轮」也不是「直到通过」
每轮独立 shuffle 启动顺序
禁止 repeat-until-pass
```

shuffle 的作用是暴露隐式顺序依赖。固定顺序下的绿灯只证明「这个顺序可以」。

canary 清单必须是单一事实来源。用于日志或断言的数量常量必须从清单派生，不得独立维护。

### 泄漏检查

每个 scenario dispose 后检查全空：

```text
PID / 端口 / session / worktree / 临时目录 / lock / runtime journal
```

每个 scenario 独占 workspace、HOME/XDG、Provider、端口、Journal、spool、进程组、diagnostics。

### 静态门禁必须命中真实路径

harness 内的静态检查（禁止固定 sleep 等）其路径判据必须与实际目录一致。指向不存在目录的检查恒为通过，是伪门禁，等同于没有检查。

### 禁止退化清单

以下任一出现即为门禁退化，等同于 VERIFY-006 的 No-Go：

```text
把 wall-clock 总超时当作唯一挂死判据
让原始 SSE 或 provider 流量续期 watchdog
让背景车道进展续期 watchdog
删除 watchdog 的诊断转储，只保留退出码
让 watchdog 计时器持有事件循环，使干净结束也要等满静默窗口
存在只有总超时保护的时间窗
声明了断言心跳但未接线
用固定 sleep 代替因果 bark 交错启动
就绪超时或就绪前退出被当作通过
Release gate 变成「最多 N 轮」或「重跑直到通过」
数量常量与清单各自维护
静态门禁的路径判据指向不存在的目录
延长静默窗口或测试超时以掩盖竞态
```

最后一条是最隐蔽的：调大超时永远能让红灯变绿，而它消灭的是发现问题的能力，不是问题。

## VERIFY-005：Architecture Gates

Gate 只阻断语义违规，不阻断尺寸。

### 硬阻断

```text
Kernel 引用 Host raw obj
多个 Fallback writer
多个 Prompt sender
未授权工具进入 provider-visible schema
未处理 Result/Outcome
循环依赖
单文件同时拥有多个不相关副作用边界
同一算法在多处定义（duplicate algorithm owner）
碎片 SSE 事件进入业务层（ARCH-002）
```

### 单一写入口门禁

```text
FallbackCursorAdvanced / FallbackExhausted → 只能由 FallbackController
任何 user-shaped prompt                    → 只能由 PromptDispatcher
PTY completion                             → 只能由 backend onExit
Review confirmed                           → 只能派生，不能赋值
```

### Host 边界门禁

`Fable.Core.JsInterop`、动态属性访问、`createObj`、`unbox`、`jsNative` 只能出现在 Adapter/Codec 文件。

### 不设行数门禁

行数不是门禁项。 文件长度既不硬阻断也不告警。

理由：行数是症状而非病因。真正要禁的是样板、框架礼仪、错误抽象、重复知识——这些由上面的语义门禁直接命中。用行数代理会产生反向激励：为过门禁而做机械拆分（`*Helpers.fs`、`*Fields.fs`、`*Core.fs`），把一个内聚语义边界切成互相调用的碎片，可读性反而更差。

因此机械后缀命名（`*Helpers`、`*Primitives`、`*Fields`、`*Emit`、`*Service`、`*Core`）仍需显式 allowlist——这才是防止拆分逃逸的真门禁。

禁止：删空行、合并语句、一行多事、滥用分号来压缩行数。这类改写既不改善也不劣化门禁结果，只损害可读性。

### Gate 是静态检查器，不是测试

所有 Architecture Gate 都是文件系统加正则的纯文本判断，不得实现为测试用例。它们属于 VERIFY-001 第 0 层，住在 `scripts/`，不依赖任何编译产物：

```text
scripts/check.mjs                  串行执行 focused checks
scripts/checks/spec.mjs            规范内部一致性（条款唯一、无悬空引用、前缀归属、导航完整）
scripts/checks/architecture.mjs    源码根、fsproj 完整性、分层边界、资源读取位置、无 .gen.fs、无旧路径
```

把它们放进测试套件会造成两个错误：需要先编译才能检查源码；门禁失败与行为失败混在同一个红灯里，无法分别处理（退火期必须能分层打开反馈）。

## VERIFY-006：No-Go（出现任一项不得发布 0.5.0）

```text
仍支持 manager/coder/reviewer 等旧 Agent 名称
仍支持 build 或 plan alias
任意公开创建操作可以省略 fast/deep
万象术仍从环境变量读取模型
万象术发送 Prompt 时仍设置 Model
Authority journal 仍保存 model ID
Cursor pattern 在固定失败次数上判死（FALLBACK-005：Offset 循环无界）
成功时重置 Offset（FALLBACK-004：成功只清零 ConsecutiveFailureCount）
把 HostSignal.ProviderRetry.Attempt 当作 ConsecutiveFailureCount（FALLBACK-010）
超过 AutoRecoveryBudget 后仍继续自动请求（FALLBACK-005）
Blogger 或 Executor 名称进入 LLM tool schema
Blogger 不是从 fast-blogger 开始
重启后 fallback cursor 丢失
重启后 journal 旧 model 覆盖新 opencode.json
拼错 Agent 被静默当作新 handle
旧 journal 被猜测性迁移
```

注意 Cursor 与预算的区别：`Offset` 的 A/A/B/B 循环无界（第 4 次失败绝不判死），`ConsecutiveFailureCount` 的自动恢复预算有界（默认 12 后写 `FallbackExhausted`）。两者都写错才是 No-Go；把有界预算误读成"循环有界"，或把无界循环误读成"预算无界"，都违反 FALLBACK-005。

## VERIFY-007：三种 Provider Projection

必须区分三种 projection，分别用于不同目的。三者是不同类型，不得隐式互转，不得由同一个函数产生。

### ProviderWireProjection

用途：前缀缓存门禁（ARCH-004）、Seal Barrier（COMPANION-009）、Review input proof（REVIEW-010）。

包含实际发送到 provider 的所有 wire-visible 字段：provider/model/variant、tools、system、messages、tool call IDs、tool result IDs。

判断标准：精确字节相等。

因为包含 ID，它只在同一 Session 的同一条时间线内可比较。跨 Session 比较 Wire projection 无意义。

### ProviderSemanticProjection

用途：Canary fixture 匹配（VERIFY-003）、行为比较、BloggerDeltaProjection 的唯一上游。

排除：message IDs、call IDs、timestamps、runtime metadata、directory、status、finish reason、cost、usage。

判断标准：语义相等（规范化后字符串相等）。

因为排除 ID，它跨 Session、跨重启可比较。

### BloggerDeltaProjection

用途：送往 Companion Blogger 的 TOML delta（CTX-013）。

由 `ProviderSemanticProjection` 进一步有损降级：图片内容替换为 `image_omitted` 占位（CTX-013），按 200 KiB 输入合同切块（CTX-003），渲染为确定性 TOML 文本。

判断标准：逐字节相等的 TOML 文本。

它不是缓存键，不参与 Seal，不参与剧本匹配。

### 关系

```text
Host/provider event
  ├─ ProviderWireProjection    （含 ID，字节相等，本地时间线）
  └─ ProviderSemanticProjection（去 ID，语义相等，跨会话）
        ├─ XTraceProjection    （唯一语义 source：XTrace、LWR gap、terminal capture）
        ├─ BloggerDeltaProjection（Y 输入，TOML 文本，≤200 KiB 切块）
        └─ LifecycleWorkRecordProjection（跨 Session artifact，COMPANION-003）
              └─ Provider/Wire renderer（fork envelope、join work_record）
```

```text
Wire → Semantic → XTrace → BloggerDelta / LifecycleWorkRecord
```

每一步都是单向有损投影，各自允许一个显式命名的降级函数。反向不存在：丢失的 ID 与图片内容无法恢复。

XTrace 是 Y delta、LWR gap、terminal capture 的共同唯一 source（COMPANION-007、COMPANION-012）。同一 segment 的语义解析不得分叉；从 source 到各 artifact 允许显式有损投影：BloggerDelta 保留 tool call/result 作压缩输入，LifecycleWorkRecordProjection 剔除 raw tool call/result（COMPANION-003）。

禁止：

```text
让同一个 projection 同时承担字节相等和语义相等
用 Semantic projection 做 Seal（会让不同 run 的 seal 相同 → REVIEW-003 崩塌）
用 Wire projection 做 canary fixture 键（永不命中 → VERIFY-003 崩塌）
用 BloggerDeltaProjection 做 Seal、缓存键或剧本键
从 Wire 直接构造 BloggerDelta（跳过 Semantic 会带入 ID）
在三者之间隐式转换或复用同一类型别名
用 PrefixCoverage 计算 LWR gap，或用 RecordCoverage 直接做 prefix replacement（COMPANION-011）
把 raw tool call/result 写入 LWR（COMPANION-003）
```

## VERIFY-008：测试语言边界

生产代码是 `.fs`。第 1–3 层测试全部是 `.mjs`，直接消费 `dist` 发布产物。

```text
src/Wanxiangshu/**/*.fs           生产实现，唯一 .fs 领域
dist/**/*.js     Fable 发布产物，生产入口与测试入口是同一份字节
tests/unit/**/*.mjs     第 1–3 层测试，node:test 运行，无编译步骤
tests/e2e/**/*.mjs       第 4 层 canary harness
scripts/*.mjs          第 0 层静态检查
```

### 理由

不是为了省编译时间，而是让语言边界物理性地阻止测试触碰实现内部。

从 `.mjs` 能干净进入系统的入口，恰好是 SSOT 已认定为事实的那几个；无法干净触碰的，恰好是 SSOT 留给实现自由的部分：

| 可测（契约面） | 条款 |
|--------------|------|
| Journal envelope 的 NDJSON 文本 | PERSIST-001 |
| ProviderWireProjection / ProviderSemanticProjection | VERIFY-007 |
| Host hook 的 input/output 对象形状 | HOST-003 |
| 纯 fold 与纯判定函数（值进值出） | ARCH-005 |
| Port 接口（typed fake 实现为 JS 对象） | ARCH-005 |

| 不可测（实现自由） |
|------------------|
| F# record 字段布局与顺序 |
| 私有辅助函数 |
| 内部类型层次与模块划分 |
| 中间数据结构选择 |

类型系统守生产代码边界，语言边界守测试代码边界。同一原理的两次应用。

### 入口规则

测试只允许经由契约面进入：

```text
允许：序列化文本、纯函数、公开 Port、Host hook 对象、发布产物 export
禁止：断言 DU 的 tag 序数
禁止：断言 Fable 命名约定（Module_ 前缀、$reflection、FSharpMap 内部结构）
禁止：为测试可见性而在生产代码新增 export
禁止：只断言真值——字段改名后 undefined 会静默通过
```

断言必须比对完整结构或完整序列化文本。挑单个字段看真假会在重命名后静默失效——`.mjs` 没有编译期重命名保护，这一条是它的对价。

### Fable 约定 facade

Fable 编译产物的命名与容器形状是编译器产物，不是领域概念。它们必须被隔离在唯一一个文件内：

```text
tests/unit/domain.mjs   唯一允许知道 Fable 输出形状的文件
```

该 facade 承担：`Module_` 前缀名到领域名的映射、DU 到 case 名的读取、`FSharpMap` 到条目的转换、`DateTimeOffset` 等值类型的正确构造。

这与生产代码的 Host 边界门禁（VERIFY-005）完全同构：动态属性访问只能出现在 Adapter/Codec，Fable 约定只能出现在 facade。

facade 自身需要元测试。已知陷阱：`DateTimeOffset` 必须构造为携带 offset 的值，直接传 `new Date()` 会让时间比较反向错误且不报错——一个写错的测试会宣布错误的实现是正确的。

### 陈旧产物 fail closed

`.mjs` 测试消费 `dist`。若产物早于 `src/Wanxiangshu/**/*.fs`，测试运行器必须拒绝运行并报错，不得使用旧产物给出绿灯。

### 测试命名

测试名直接引用条款 ID：

```text
PROMPT_005_submitted_receipt_is_not_authority
FALLBACK_003_duplicate_signal_advances_once
FALLBACK_005_budget_exhaustion_stops_auto_attempts
REVIEW_003_second_run_must_contain_challenge
EXEC_009_retired_handle_never_reforks
ORCH_007_changed_target_requires_rereview
```

粒度原则：入口粗，覆盖细。入口只有契约面，但每个条款都要有最小反例。一个测试只验证一条因果链。

### 例外

需要 F# 计算表达式语义保真的资源契约测试（`Flow` 的 `use!` 异常保留顺序等），仍从 `.mjs` 通过已有公开入口调用。这不构成为测试污染生产：`Flow.run` 本身就是公开消费入口。

不得为了测试新增生产 export 或放宽可见性。若某语义只能通过新增 export 验证，说明它缺少契约面——先补契约，不补 export。
