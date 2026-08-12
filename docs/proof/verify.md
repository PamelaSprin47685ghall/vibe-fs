# 验证原则

条款前缀：`VERIFY-`。  
本文件是 **proof 唯一权威**：应运行什么、证明什么、失败意味着什么。  
运行证据在 CI artifact，不进 Git 工作树。实现命令入口见 `AGENTS.md` / `npm run check*`。

`VERIFY-*` 定义不得迁出本文件（检查器与测试锚点依赖）。

## 阅读地图

| 条款 | 回答的问题 |
|------|------------|
| VERIFY-001 | 测试金字塔：Pure laws → Temporal → Adapter → Long Stroke → Release |
| VERIFY-002 | 晋级阶梯，禁止跨级；禁止语义分支直跳 E2E |
| VERIFY-003 | Canary mock 剧本：键、匹配、幂等、故障轴、冷边界 |
| VERIFY-004 | 因果推进门禁、One Physical World 与**禁止退化清单**（机器解析锚点） |
| VERIFY-005 | Architecture gates / 单一写入口 / Host 边界 / Gates A–D |
| VERIFY-006 | No-Go 发布否决 |
| VERIFY-007 | Wire / Semantic / BloggerDelta 三种投影 |
| VERIFY-008 | 测试语言边界（`.fs` 生产 / `.mjs` 契约面） |

读顺序建议：001→002 定层；008 定入口；003+007 定 canary/契约面；004 定挂死语义与 One World；005+006 定发布门。

## VERIFY-001：测试金字塔

权威形态（G4R One World / Pure Time；G4R-4 cutover 后唯一事实）：

```text
0. Static architecture/proof gates
1. Pure laws（无 Host / clock / process / network）
2. Temporal（Deterministic temporal workflow proof：production workflow + virtual ports + explicit traces；可穷举/有界交错）
3. Adapter（Single physical adapter contract：恰好一个物理边界）
4. Long Stroke（恰好 1 个真实 OpenCode E2E，恰好 1 次 OpenCode lifetime）
5. Release（一次确定性 full proof + build/package/packing）
```

第 0 层是纯文本与文件系统检查，永远不依赖编译产物，因此在任何阶段都可运行。第 1–3 层的语言与入口由 VERIFY-008 规定。

### G4R One World 事实（post-G4R-4）

上表即现行唯一权威。Cutover 事实：`tests/e2e/cases/**` 已删除（gone）；唯一 Long Stroke 入口为 `tests/e2e/entry.test.mjs`；`E2E_CASE_CEILING = 0`。旧 multi-canary / Fake Host 轨迹 / 三轮 shuffle harness **不得**回潮，亦不得与上表双真。

第 0 层永久执行 `scripts/checks/g4r-freeze.mjs`（永久 ratchet，非迁移期临时门；天花板只降不升）：

```text
E2E case 数量不得越过冻结天花板（当前 0；只降不升）
time-budget.js 命名预算不得抬高
禁止 per-basename / per-case canary timeout map
顶层 E2E entry 必须恰好为 tests/e2e/entry.test.mjs（唯一 Long Stroke）
```

不得以新增 E2E、抬 timeout、retry-until-pass、恢复 multi-canary 拓扑、或精修已删除 scenario choreography 作为修复路径。Race 证明在显式 temporal algebra；物理组合证明是唯一 Long Stroke。

## VERIFY-002：五级晋级阶梯

不允许跨级。阶梯（与 VERIFY-001 对齐；唯一 Long Stroke，无 multi-canary 晋级通道）：

```text
1. Pure law
2. Deterministic temporal trace
3. Single physical adapter contract
4. One Long Stroke
5. Release
```

禁止把 semantic branch 直接晋级到 E2E。禁止把语义命题「晋级」成并行 multi-canary / 多进程 fan-out 冒充覆盖。

若某分支声称「必须由 OpenCode 才能验证」，作者必须先回答：

```text
它到底依赖哪个不可模拟的 physical contract？
```

答不出 → REVISE（降回 Pure law / Temporal / Adapter，不得以 E2E 顶替）。不得用「场景复杂」「历史如此」「先挂一个 canary 再说」代替物理契约论证。

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

前缀缓存不变量（ARCH-004）的合法例外只有三处，且都有正式定义：

```text
COMPANION-009  Epoch 切换：新 SealRoot，允许一次显式 prefix rebase
FALLBACK-014   Fallback 换边：只改 EffectiveAgent；SessionPersona / SessionProviderLanguage / Wanxiangshu-owned system prompt 不变（Gate D）
AGENT-031      NEEDHELP 换边：fast→deep 只改隐藏 ExecutionBinding；Authority / Persona / tools / transcript 不变
```

历史例外 `StudentLearn → StudentCompile`（AGENT-020 / PROMPT-012）：**G3 已删除（absent）**，
不得再作冷边界或 scenario 声明位。

上述三处必须由 scenario 显式声明发生位置。禁止由 mock 嗅探请求形状推断（例如「tools 与 system 未变即视为 epoch 切换」）——那会放过在不该切换处发生的切换。

Gate D 冻结的是 Wanxiangshu-owned system prompt 语义。OpenCode 会在最终 provider system message 中写入当前 physical model id；Fallback/NEEDHELP 的 ExecutionBinding 切换允许**仅该 Host-owned model identity 字样**随 model 改变，不允许其它 system、tools 或历史消息改写。mock 必须先验证该替换，再用生产 `ProviderProjection.isAppendOnlyPrefix` 校验剩余 transcript；不得把这条规则扩大成「model 变了所以 system 任意可变」。

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

原理必须继承并发扬，任何重构不得丢弃或降级：**因果进展、semantic watchdog、死前诊断、时间注入、禁止固定 sleep、禁止 timeout padding、禁止 repeat-until-pass** 永久保留。

G4R One World 目标另增：One Physical World、OpenCode Spawn Exactly Once、Semantic Race 的 deterministic trace、Temporal 用 Virtual Time、No Wall-Clock Semantic Assertion、Long Stroke 只观察公开/可持久语义（见下文各节）。

旧 multi-canary 的 scenario parallelism / startup width / bark chain / shuffle / 三轮 E2E repeat / one process per canary：**superseded，target-delete**——不得再写成目标形态；迁移期若旧 harness 仍在，只许收敛、不许加固。

现有落点包括 `tests/e2e/support/watchdog.js`、`tests/e2e/support/readiness.js`、`tests/e2e/support/stability-checker.js`、`tests/unit/support/run-inner.mjs` 与 `tests/unit/support/verdict-feed.mjs`；实现不因先存在而获得权威。

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
          idle-after-activity）、Host 重启的各个阶段、
          waitFact 的目标事实计数增长、waitFact.renewOn 声明的中间事实计数增长
不算进展：原始 SSE、provider HTTP 流量、session.created 之类的生命周期噪声、
          waitFact 等待期间其它 journal 写入（仅 advance(blocking=false) 记录）、
          任何「有字节在动」的证据
```

`waitFact` 的续期依据由剧本显式归因，不从「journal 有任何写入」反推因果：目标
`name` 计数增长恒为阻塞进展；`renewOn` 中任一已声明事实计数增长为阻塞进展；其余
journal 增长只记录背景 activity 而不续期静默窗口。`renewOn` 是 proof 剧本声明，不
进入生产事实、Journal envelope 或运行时配置。

区分标准是：该事件是否证明被测因果链前进了一步。传输层有动静不等于语义有进展——一个反复重连的 SSE 读者能永久续期一个错误的 watchdog。

背景进展（非阻塞车道，例如 blogger sidecar）必须被记录但不得续期：

```text
advance(blocking = true)   → 重置静默计时器
advance(blocking = false)  → 只记录，不重置
```

case 文件不得直接调用 `watchdog.advance`。自定义观测统一经 `support/causal-observation.js`：只有 observation token 相对上次真实变化才允许 blocking advance；同值 poll 只等待，不续命。`scripts/checks/e2e-watchdog-feed.mjs` 对 `tests/e2e/cases/**` 与顶层 e2e test fail closed。

超时时先转储诊断（事件尾部、待命中的剧本步、最后一次进展的 reason 与 lane、最后一次背景进展距今多久），再退出非零。诊断必须包含「最后一次进展是什么」，否则 watchdog 只是一个更快的超时。

计时器必须不持有事件循环：所有其它句柄关闭后进程自然退出，watchdog 只在仍有东西（挂死的 SSE 读者、泄漏的 server）维持事件循环时开火。这样它不会把干净结束的 scenario 拖到静默窗口结束。

`ITimerPort`：delay/cancel/dispose + ref/unref policy；生产=Node timer，测试=虚拟时钟注入；watchdog/deadline 判定与真实 timer 等价，cancel 后零回调触发。SSE 心跳与 reconnect 经 ITimerPort 注入（生产=nodeTimerPort，测试=virtualTimerPort）。

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

### 事件交错启动（superseded / migration — target-delete）

> **G4R One World：本节描述的 multi-canary 并行池 / startup-width bark chain 是旧拓扑事故机制，目标态删除。**  
> 迁移完成前，若旧 harness 仍在运行，下列纪律仍约束它，防止退化成固定 sleep；cutover 后本节整体作废，由下方 One Physical World / Spawn Once 取代。

旧形态（target-delete）：E2E canary 全并行运行在一个池里；canary N+1 的启动条件是 canary N 输出了精确的就绪标记，而不是固定 sleep。

```text
（迁移期仍有效，目标删除）必须：因果 bark（就绪标记出现即启动下一个）
（迁移期仍有效，目标删除）禁止：按序号计算的固定延迟
（目标删除）MAX_PARALLEL / CANARY_STARTUP_WIDTH / bark stagger / parallel worker pool
```

配套的两个门禁（迁移期仍有效，随 multi-canary 拓扑一并 target-delete）：

```text
就绪门禁：未在有限窗口内输出就绪标记 → 该 canary 失败（不是放行）
早退门禁：在输出就绪标记之前退出 → 该 canary 失败
```

### Release gate（superseded / migration — target-delete）

> **G4R One World：三轮 × shuffle × 多 canary 的 Release 形状已 superseded。**  
> 目标 Release = 一次确定性 full proof + build/package/packing（见 VERIFY-001 第 5 层）。  
> 「禁止 repeat-until-pass」作为因果纪律永久保留；「恰好 3 轮 / 每轮 shuffle」随旧拓扑 target-delete。

旧形态（superseded，不得再当作目标）：

```text
（superseded）恰好 3 轮，不是「最多 3 轮」也不是「直到通过」
（superseded / target-delete）每轮独立 shuffle 启动顺序
（永久保留）禁止 repeat-until-pass
```

清单单一事实来源仍永久成立：用于日志或断言的数量常量必须从清单派生，不得独立维护。目标态下清单收敛为唯一 Long Stroke（及静态/纯时序入口），而不是多-canary 枚举。

### One Physical World（目标）

```text
VERIFY-004 One Physical World
  证明世界至多一个：不得用并行 multi-canary / multi-process fan-out 冒充覆盖面
  语义命题在 Pure / Temporal 层证明；物理世界只承担不可模拟的组合契约
```

### OpenCode Spawn Exactly Once（目标）

```text
VERIFY-004 OpenCode Spawn Exactly Once
  Long Stroke 全程恰好一次 OpenCode lifetime / spawn site
  禁止 per-canary 一进程、禁止为「再跑一遍」二次拉起真实 Host
```

### Semantic Race Has Deterministic Trace Proof（目标）

```text
VERIFY-004 Semantic Race Has Deterministic Trace Proof
  race / 交错命题必须有显式 deterministic trace（virtual ports + 有界交错）
  禁止把真实 scheduler 碰巧顺序当作 race 证明
```

### Temporal Tests Use Virtual Time（目标）

```text
VERIFY-004 Temporal Tests Use Virtual Time
  Temporal 层测试必须走虚拟时间 / 注入时钟，不得依赖真实墙钟推进语义
  生产墙钟只允许经声明的 physical time / timer adapter
```

### No Wall-Clock Semantic Assertion（目标）

```text
VERIFY-004 No Wall-Clock Semantic Assertion
  禁止用 wall-clock 时长、固定 sleep、或「等够 N 秒」断言语义成立
  wall-clock 只可作挂死兜底，不可作语义判据（与因果 watchdog 同构）
```

### Long Stroke Observes Public/Durable Semantics（目标）

```text
VERIFY-004 Long Stroke Observes Public/Durable Semantics
  Long Stroke 只观察公开/可持久语义面（journal、投影、契约出口）
  禁止导演 production 私有状态或断言内部 helper 时序
```

### 泄漏检查

每个 scenario dispose 后检查全空：

```text
PID / 端口 / session / worktree / 临时目录 / lock / runtime journal
```

每个 scenario 独占 workspace、HOME/XDG、Provider、端口、Journal、spool、进程组、diagnostics。

### 静态门禁必须命中真实路径

harness 内的静态检查（禁止固定 sleep 等）其路径判据必须与实际目录一致。指向不存在目录的检查恒为通过，是伪门禁，等同于没有检查。

### 禁止退化清单

以下任一出现即为门禁退化，等同于 VERIFY-006 的 No-Go。

机器解析锚点（下列 ```text 块内条目文本不得无故改写；解析器按整行绑定 id）。其中「因果 bark 交错启动 / 就绪早退 / Release 轮次」条目在迁移期仍约束旧 multi-canary harness；cutover 后随该拓扑删除，不得借改写条目把退化合法化——应删拓扑，不改禁令语义为「允许 until-pass」。

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

One World 目标态下，下列形状同样视为退化（与上文各目标节同义；待静态 ratchet 落地后可并入机器锚点，现以条款正文为准）：

```text
并行 multi-canary / worker pool 冒充证明覆盖面（违反 One Physical World）
Long Stroke 内二次 spawn OpenCode（违反 Spawn Exactly Once）
用真实 scheduler 碰巧顺序证明 semantic race（缺少 deterministic trace）
Temporal 测试依赖真实墙钟推进语义（违反 Virtual Time）
用 wall-clock / sleep 断言语义成立（违反 No Wall-Clock Semantic Assertion）
Long Stroke 断言 production 私有状态或内部 helper 时序
```

## VERIFY-005：Architecture Gates

Gate 只阻断语义违规，不阻断尺寸。

### 必须阻断的违规

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

### 性质决定证明载体

能从源码文本或文件图直接判定的性质属于第 0 层静态门禁；依赖调用轨迹、构造可达性或 fold 行为的性质必须由第 1–3 层契约测试证明，不能用正则假装完整。当前自动化所有权如下：

| 载体 | 当前覆盖 |
|------|----------|
| `scripts/checks/spec.mjs` | 条款唯一/引用/前缀/导航、Change 禁止定义正式条款、废止路径与实现依赖禁令 |
| `scripts/checks/architecture.mjs` | 源码根、fsproj 完整性、Kernel/Domain 依赖、资源读取、无 `.gen.fs`、旧路径、recovery ownership |
| `scripts/checks/dsl-ownership.mjs` | 第二运行时、业务 Interpreter、程序计数器模式、mutable 与 infrastructure-open 边界 |
| `scripts/checks/p0-recovery-join.mjs` | Agent false finality、recovery/join 特定单一 owner 与正负模式 |
| `scripts/checks/student-teacher-absence.mjs` | Playbook §24.1 Symbol：生产无 Student/Teacher Role、fast/deep-student/teacher、StudentLearn/Compile/QaStore/StudentTeacherRuntime、SatelliteKind.Teacher/Replica；`tests/unit/verify/student-teacher-absence.test.mjs` 钉 token 集与 scanEntries |
| `scripts/checks/session-ownership-ratchet.mjs` | Playbook §24.4 问卷（Companion / SyncInspector / SyncCoder / Bookkeeper / hidden Reviewer / StrengthReplica / fork agent / Executor child）；G9 ownership Exit |
| `scripts/checks/capability-isomorphism-gate.mjs` | Playbook §24.3 Agent×RequestKind×AttemptExecutionProfile 五层同构静态门；`tests/unit/verify/capability-isomorphism-gate.test.mjs` |
| `scripts/checks/unified-store-gate.mjs` | Playbook §24.1/24.2 Storage：feature-owned `refs/wanxiang/*`、Casebook custom ref、legacy Journal/Blob reader、dual-write、student-qa revival |
| `scripts/checks/js-surface-gate.mjs` | G3 rebase：无 js-student/js-teacher、无手写 per-role js-* |
| `scripts/checks/enforcer-rulebook-gate.mjs` | mechanical A37/A38（`check.mjs` 以 `--require-headings --strict` 接线）= G7 machine Exit；HUMAN_ONLY（paired-history 120 / A39 / A40）为目录质量过程，不伪造、不阻断 Gate |
| `scripts/checks/tool-referential-integrity.mjs` | **Gate A**（ARCH-016）：same tool name → 唯一 schema + 唯一 semantic contract；pin `tests/unit/verify/tool-referential-integrity.test.mjs`（code phase 新建） |
| `scripts/checks/provider-leak-gate.mjs` | **Gate B**（ARCH-016）：provider 输出禁 leak vocabulary；pin `tests/unit/verify/provider-leak-gate.test.mjs`（code phase 新建） |
| `scripts/checks/language-parity-gate.mjs` | **Gate C**（ARCH-016 / HOST-026）：∀ provider semantic resource EN + zh-CN；pin `tests/unit/verify/language-parity-gate.test.mjs`（code phase 新建） |
| `tests/unit/invariants/prompt-stability.test.mjs` | **Gate D**（ARCH-016 / FALLBACK-014）：同 session fallback/T1/review/reanchor/Strength → system prompt 字节相同（无静态宿主；code phase 新建） |
| `tests/unit/**` | Fallback/Prompt/PTY/Review 的可达构造、唯一入口与完整行为 |

### Gate B leak vocabulary（权威禁令表）

provider 输出 / schema / fixed prose **不得**含：

```text
SessionId / AgentId / ManagerJobId / PtyId / FissionGroupId
lane_index / worktree / fallback offset
fast- / deep- binding 冒充身份 / spool path
status / code / error 泛型 DTO（Join/horizon）
agent_id / pty_id / session_id（horizon 字段）
```

技术标识（tool 名、argument、wire field、enum、path、command、`exit_code`）在 EN/ZH **同形**（Gate C）；localizable 散文才翻译。

### 旧 substring inventory — 删除

下列以源码/fixture **子串匹配**锁旧 provider ontology 的证明形态 **作废**，不得再作门禁权威（Phase 19 / §17）：

```text
tdd="red"|"green" / TddPhase
list() DTO / agent_id / fork-manager / verdict（工具名）/ blog（工具名）
Opening task / Work log / Uncompressed tail / Final output
parent_work_record / original_user_requirement
edit-qa / Meditator / Role.Executor 作为现行表面
```

代表删除/改写目标（code phase；本文件不声称已绿）：
`tests/unit/execution/tdd-phase.test.mjs`、
`tests/unit/verify/fork-child-payload-tdd-contract.test.mjs`、
`tests/unit/verify/orchestrator-reuse-contract.test.mjs`、
以及 `manager-tool-contract` / `*-tool` / LWR heading substring 套件。

替代：上表 Gate A–D + `tests/unit/invariants/*` 语义不变量。禁止用新的宽松 substring 清单假装覆盖。

`npm run lint` 绿色只证明上述静态覆盖，不得宣称已经证明跨文件语义一致、所有 `Result` 穷尽处理或所有算法 owner 唯一。新增静态门禁必须有故意破坏后变红的 fixture；新增行为门禁必须走发布产物测试。

## Fail-Closed 校验与破坏性回归测试指南

为了确保系统遇到数据损坏、版本不兼容或边界失配时能够安全 Fail-Closed，而不是崩溃或吞掉上下文，第 1–3 层测试中必须包含以下破坏性回归测试集：

1. **Envelope 字节损坏回归测试**：在 Journal 反序列化入口传入 0x04–0xFF 的非法 `FallbackOffset` 字节，验证系统返回 typed `Result.Error` 并拒绝加载损坏 envelope，绝对不抛出未捕获的 `invalidOp`，也不构造只属于 Append 的 `CommitUnknown`。
2. **裸文本/未绑定 ID 拒绝测试**：传入未带 SessionBinding 或包含未知来源字符串的 `PromptAbandonReason`，验证 PromptDispatcher 正确拒绝并维持前置安全状态。
3. **Intent 互斥组合测试**：同时声明 `keepPhysicalPrefix` 与 `activatePrefixEpoch`，验证 Planner 准确捕获并产出 `ProjectionConflict`，安全挂起当前 Attempt。

## VERIFY-006：No-Go（出现任一项不得发布）

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
Blogger 或 Distiller 名称进入 LLM tool schema（工具须为动词：chronicle / run …）
旧工具名 alias（fork-manager / list / verdict / blog / edit-qa / executor / return / tdd）仍可调用
Blogger 不是从 fast-blogger 开始
Fallback / Strength 改变 SessionPersona 或 system prompt 字节（FALLBACK-014 / Gate D）
provider 输出泄漏 Gate B vocabulary
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
tests/unit/support/domain.mjs   唯一允许知道 Fable 输出形状的文件
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

## VERIFY-009：单元测试覆盖率门禁

`tests/unit` 的节点:test 运行必须同时产出整体行覆盖率并接受阈值门禁。

```text
门槛：整体行覆盖率 ≥ 80%（dist 全部生产模块，排除 fable_modules）
入口：npm run test:coverage（= node tests/unit/run.mjs --coverage）
产物：artifacts/coverage/coverage-summary.json
```

### 分母必须是整体

node:test 的 V8 覆盖率只统计被加载的文件；未加载模块在报告里凭空消失，分母缩水会让百分比虚高。覆盖率运行先预导入 `dist` 全部生产模块（`fable_modules` 除外），一个没被任何测试触碰的模块以 0% 计入分母，而不是从账本上消失。预导入失败即 fail closed——部分世界算出的百分比不是覆盖率。

### 只统计生产字节

排除项固定为 `**/node_modules/**`、`**/fable_modules/**`、`**/tests/**`：测试自身、support facade、Fable 运行时与第三方包都不是被测对象。

### 低于门槛即红

阈值由 `COVERAGE_LINE_THRESHOLD` 持有（默认 80），在 inner runner 内判罚：低于门槛 exit 1，监督器继承退出码，套件整体失败。不允许豁免通道——任何豁免都会成为伪门（VERIFY-004 同款逻辑：没锁的门不是门）。

### 提升手段约束

覆盖率只许通过增加测试达成。禁止：为测试新增生产 export、放宽可见性、改写生产代码结构以压缩行数（VERIFY-008 例外条款同样适用）。
