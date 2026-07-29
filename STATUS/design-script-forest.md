# 剧本森林第一性原理分析

针对 `testkit/opencode/strict-mock-*.js` + `scripts/*.json`。
状态：设计定稿，未实施。实施属休克三之后的独立工作包（包 K）。

---

## 一、唯一要解决的问题

canary 需要一个可重放的 provider。真实 provider 对插件而言是：

```text
HTTP POST /v1/chat/completions  →  SSE 响应
```

无状态：每次调用独立，Host 不携带「这是第几次」给 provider。因此确定性替身的本质形态只能是纯函数：

```text
mock : Request → Response
```

剧本森林的设计意图正是这个，代码注释写得很清楚：

> Runtime identity for a *request* is provider-visible full seal (idempotent).
> No numbering to pick different responds for the same prefix.

当前实现违反了自己声明的每一条。 下面先证明冲突真实存在，再证明它是可解的，且解法唯一。

---

## 二、公理冲突

三条需求同时为真：

| # | 需求 | 来源 |
|---|------|------|
| A1 | mock 是请求的纯函数，同请求同响应 | VERIFY-003 幂等 |
| A2 | 同一 provider 请求必须能先失败后成功 | FALLBACK 全部条款要证明 A/A/B/B |
| A3 | Host 重试重发字节相同的请求 | Host 事实，`prompt.ts` 主循环不改 messages |

A1 ∧ A2 ∧ A3 在 `Request → Response` 这个签名下确实矛盾。同一输入必须产出两种输出。

当前实现选择放弃 A1，且是偷偷放弃的：

```js
// strict-mock-forest.js  consumeExpectation
const respondType = edge.respond?.type
if (respondType !== 'error' && respondType !== 'disconnect') {
  state.sealToEdgeId.set(match.key, edge.id)      // 成功：幂等缓存
} else {
  state.sealToEdgeId.delete(match.key)            // 失败：主动破坏幂等
}
```

注释自认：

> Error responds are intentionally non-idempotent for Host retry tests.

于是需要 `pathCursor` 决定「同一前缀这次给哪条边」。`fallback.json` 的实证形态：

```json
{ "id": "round1-failure", "lane": { "turn": 1 },
  "match": { "user": "Round 1 fallback attempt.", "requiredTools": ["write"] },
  "respond": { "type": "error", "status": 500 } }

{ "id": "round1-retry",   "lane": { "turn": 2 },
  "match": { "user": "Round 1 fallback attempt.", "requiredTools": ["write"] },
  "respond": { "type": "text", "text": "round1 retry completed." } }
```

`match` 逐字节相同。区分二者的唯一依据是 `turn` 排序后的游标。

这是队列，穿着森林的外衣。 而 `script-loader.js` 的文档说：

> lane.turn is authoring metadata only (not a runtime match key).

`indexPathEdge` 按 `lane.turn` 排序，`pathHead` 按游标放行。turn 就是运行时匹配键。文档与实现分岔。

---

## 三、冲突的真正根源

矛盾不在 A1，而在签名写错了。

「这次请求失败」不是内容事实，是传输事实。它与 messages 无关，与 provider 打算说什么无关。它属于另一个域：

```text
内容域：这段对话前缀，模型会回答什么
传输域：这次物理投递，成功还是失败
```

`Request` 只承载内容域的信息。用它当传输域的键，必然要求内容域变得有状态。

关键观察：物理投递次数是世界上真实可数的东西。 每次 HTTP POST 是独立 TCP 连接，Host 侧对应独立的 assistant message（`prompt.ts:1186` 每个 provider step 新建一条）。它天然带序号，不需要从内容推断。

因此正确分解是两个独立函数：

```text
content   : SemanticPrefix   → Content         纯函数，全序，幂等，无计数
transport : PhysicalAttempt  → Delivery        允许计数，有限，前置声明
```

`Delivery = Ok | ProviderError(status) | Disconnect(atChunk) | Stall(ms) | NeverEnd`

A1 在 content 上成立。A2 由 transport 表达。A3 无需妥协：重试请求的语义前缀不变，content 返回同一内容，transport 第二次判定为 Ok。

`fallback.json` 重建后：

```json
"content": [
  { "id": "round1", "prefix": { "user": "Round 1 fallback attempt." },
    "respond": { "type": "text", "text": "round1 completed." } }
],
"faults": [
  { "on": "round1", "attempts": [1], "delivery": "provider-error-500" }
]
```

8 条边坍缩为 4 条内容 + 4 条故障声明。`turn` 消失，`pathCursor` 消失，seal 缓存删除消失。

这一步同时解释了腐坏的传播路径：把传输事实塞进内容域后，内容域被迫有状态；有状态之后需要游标；游标需要 turn；turn 需要排序；排序需要豁免（pathless / reusable）；豁免需要别名合并；别名合并需要双计数器等待。六层机制全部是第一个错误的下游。

---

## 四、第二缺陷：谓词合取伪装成前缀

VERIFY-003 说键是语义投影的完整前缀。实现的键是谓词合取：

```js
match: { user, userRegex, containsText[], requiredTools[], forbiddenTools[],
         messageCount, model, sessionId, requestKind, role, afterToolResult }
```

谓词集合可以同时命中多条边。于是需要打分消歧：

```js
function specificity(edge) {
  let n = 0
  if (m.user) n += String(m.user).length          // 子串长度当优先级
  if (m.userRegex) n += String(m.userRegex).length // 正则源码长度当优先级
  ...
  if (m.afterToolResult === true) n += 50          // 魔数
  if (m.afterToolResult === false) n += 40
}
```

按子串字符数和魔数决定「哪条更像」。规范要求命中 ≥2 条 fail closed，实现改为排序取第一。

真前缀不可能歧义。前缀的最长匹配唯一——要么请求的语义投影以边 E 声明的前缀开头，要么不。多条命中时最长者唯一确定，这是结构性质，不需要打分。

改为真前缀匹配后：

```text
specificity 消失
ambiguous-prefix 从「打分打平」变成「作者声明了两条同长度冲突前缀」= 真作者错误
「分叉只允许在不同 user 内容上」从口号变成字面事实
```

`afterToolResult` 这个谓词也随之消失：tool result 本身就在语义前缀里，是不是「工具结果之后」由前缀形状决定，不需要额外布尔。

但前缀树只能是运行时索引，不能是书写形式。 见第九节：人读剧本要看见对话，不是看见一棵树。

---

## 五、第三缺陷：mock 重新推导领域概念

`requestRoleOf()` 从 wire 形状反推 CanonicalRole：

```js
if (lastUser.includes('You are the blogger')) return 'blogger'
const roleCanary = lastUser.match(/Role canary: (executor|inspector|reviewer)\b/)
if (tools.includes('verdict'))   return 'reviewer'
if (tools.includes('fork-pty'))  return 'devops'
if (tools.includes('executor'))  return 'inspector'
if (tools.includes('write') || tools.includes('edit')) return 'coder'
if (tools.includes('fork') && tools.includes('join') && tools.includes('list')) return 'manager'
```

三重问题：

一，重复知识。角色由 `AttemptExecutionProfile` 唯一决定（PROMPT-008）。mock 里这段是第二份 role 推导实现，且逻辑完全不同（按工具集合猜）。生产改工具矩阵，mock 静默错判。

二，倒因为果。`tools.includes('executor') → inspector` 假设「有 executor 工具的一定是 inspector」。AGENT-006 里 DevOps 也有 executor，所以必须把 `fork-pty` 判断排在前面——注释自己写着这个顺序依赖。这是把权限矩阵的推论硬编码成匹配顺序。

三，测试标记污染 prompt。`Role canary: (executor|inspector|reviewer)` 说明某处 prompt 里埋了仅供 mock 识别的文本。fixture 要求生产在 provider 可见内容里植入测试标记，这直接违反「测试不塑造生产」。

正确原则：mock 只能知道 wire 上真实存在的东西，且不得对身份做二次推断。 需要区分两条 lane 时，区分依据必须是语义前缀本身的差异。如果两条 lane 在 wire 上不可区分，fixture 是欠定的——作者错误，不是 mock 该猜的事。

同类残留：`NUDGE_MARKERS` 六条硬编码 prose，其中

```text
'You are in loop mode. You must call the submit_review tool'
'A background runner task is still active'
'command: with-review'
'You must immediately force an emergency stop to all work'
```

在万象术 SSOT 里不存在任何对应概念。这是从别的产品带过来的死启发式，永不命中，但参与每次分类判断。

---

## 六、第四缺陷：out-of-band 身份泄漏进内容匹配

```js
export function requestSessionOf(body) {
  return body?.sessionId
    || body?.sessionID
    || body?.__testkitHeaders?.['x-session-affinity']
    || body?.__testkitHeaders?.['x-session-id']
    || null
}
```

真实 provider 收不到 session ID。嗅探自定义 HTTP header 是 harness 特权观察。

harness 需要 session 身份本身没错——前缀缓存不变量必须按 session 分别验证。错的是它流进了内容选择：`matchesExpectation` 里 `match.sessionId`、`sessionBindings`、`expectedParentSessionID` 都参与命中判断。

必须分层：

```text
内容匹配    只读 provider-visible bytes。无 session id，无 header，无 alias
harness 记账 可用 out-of-band session id（前缀封印、诊断、路由）
```

两者之间单向：记账可以观察内容，内容不可观察记账。当前是双向的。

---

## 七、第五缺陷：标志爆炸

`pushExpectation` 里四个布尔加派生规则：

```js
const pathless = opts.pathless === true || lane.requestKind === 'title' || opts.neverEnd === true
const reusable = opts.reusable === true || pathless
blocking: opts.blocking !== false && opts.neverEnd !== true && !pathless
```

`orchestrator-restart-publish-conflict.json`：27 条边里 13 条 `reusable`、2 条 `neverEnd`、1 条 `pathless`、3 条 `blocking:false`。作者必须理解四个标志的交互才能写对一条边。

按两轴分解后逐个消解：

| 标志 | 现在的作用 | 分解后 |
|------|-----------|--------|
| `reusable` | 豁免游标推进 | 消失。纯函数天然可复用无限次 |
| `pathless` | 豁免游标 + 模板去重 | 消失。无游标可豁免 |
| `neverEnd` | 不发 SSE done | 迁到 transport：`delivery: never-end` |
| `blocking` | 该边是否必须被命中才算通过 | 迁到断言：与匹配无关，是 scenario 的完成条件 |

四个标志归零。`title` 请求不再需要特殊 `pathless` 处理——它就是一条普通内容边，其语义前缀恰好可以跨 session 复现，这本来就是纯函数的正常行为。

连带消失：`templateFingerprint` 模板去重、`aliasToEdge` 别名映射、`strict-mock-signals.js` 的 `matchCount`/`claimCount` 双计数器。后者存在的唯一原因是别名合并后要让每次 wait 认领一次匹配：

> Alias-merged reusable templates (perfect-3 → perfect-1) wait on the primary edge.

双 PERFECT 被合并成一条边再靠计数区分。而两次 PERFECT 的请求本来就不同——第二次包含第一次的 challenge tool result（REVIEW-010）。它们在语义前缀上是两个真实分支。合并是匹配粒度太粗导致的，不是它们真的相同。真前缀匹配下这个问题自动消失，而且顺带把 REVIEW-003 的因果关系变成剧本结构本身可见的东西。

---

## 八、第六缺陷：动态加载

三个 scenario 在 flow 中途换剧本：

```json
{ "restart": true },
{ "loadScripts": "orchestrator-restart-publish-recovery.json" }
```

其中 `orchestrator-restart-publish-conflict-recovery.json` 的全部内容是：

```json
{ "scenario": "orch-restart-publish", "scripts": [] }
```

加载一个空集合。机制已退化成残骸，但仍在 flow 里执行。

### 为什么会引入

重启后同一条 user 文本会再次出现。旧边已被游标推过或已被 seal 缓存，于是要么错命中要么不命中。动态加载是绕过这个的手段：换一批边，重置匹配空间。

### 为什么它是错的

它把「剧本是什么」变成时间的函数。后果：

```text
剧本不可静态审阅——读一个文件看不到全部可能响应
剧本不可静态校验——冲突、欠定、不可达只能在运行时暴露
匹配空间随 flow 演化——同一请求在不同时刻合法性不同
调试时无法回答「这个请求本应命中哪一步」——取决于走到第几步
```

且与纯函数模型直接冲突：若 mock 是请求的纯函数，加载顺序不可能影响结果；能影响结果，说明它不是纯函数。

### 正确解法：重启不改变剧本

一个 scenario = 一个文件 = 一份完整剧本，Host 启动前一次性静态加载。

重启后的请求不需要新边，因为它们本来就是不同的请求：

```text
重启前：Host 从 transcript 读到 N 条消息
重启后：插件 Boot Fold 恢复领域事实；Host 从同一 transcript 读到 N 条消息
        随后的 continuation / guard / recovery prompt 追加第 N+1 条
```

语义前缀不同，因此命中不同的对话步。这是同一份剧本上的自然延续。

若某 canary 在重启后确实产生逐字节相同的请求却期望不同响应，那么它在用隐藏状态区分因果——这正是要消灭的缺陷，不是要支持的特性。

`host-restart` + `host-restart-after`（12 + 3 条边）合并为单文件；两个 orchestrator recovery 文件同样合并。合并后总边数下降，因为重复声明的 title / blogger sidecar 会坍缩。

### 静态加载换来载入期校验

一次性加载后，以下检查全部前移到载入期，无需运行 Host：

```text
同一 (turn, step) 声明两个不同响应   → 冲突，拒绝载入
两条同长度前缀在同一 turn 下冲突      → 欠定，拒绝载入
fault 引用不存在的 turn 或 attempt   → 悬空引用，拒绝载入
epoch 引用不存在的步                 → 悬空引用，拒绝载入
must 引用不存在的步                  → 悬空引用，拒绝载入
声明了但任何 flow 都到不了的步         → 死边，拒绝载入
```

最后一条只有静态全集才可能检查。这与 `ssot-lint.mjs`、`shock-audit.mjs` 同属 VERIFY-001 第 0 层：不需要产物、不需要 Host、任何阶段可运行。

---

## 九、书写形式必须是对话

前缀索引是运行时结构，绝不能是人写的东西。

若让作者手写前缀，语义完全不可读：

```json
{ "prefix": ["system:...", "user:Fix the bug", "assistant:tool_call fork-agent",
             "tool:child-1 completed", "assistant:tool_call join"],
  "respond": { "type": "text", "text": "done" } }
```

每条边重复前面所有轮次。加一步要改所有下游边。读者看不出这是一段对话。

因此分两层：

```text
书写形式（对话，人读）
  ↓ 载入期编译，一次性
运行时索引（前缀 → 响应，机器查）
```

编译产物可 dump 供调试，但不是源。与生产侧同一原理：SSOT 条款是人读的规范，Fold 是机器执行的投影，两者不共用形式。

### 运行时键的三个成分

全部是请求的纯函数，无外部状态：

```text
lane  最长匹配的 head 判别式（可选；缺省单 lane）
turn  最后一条 user 消息的语义内容（前缀匹配）
step  该 user 消息之后的 assistant 消息条数
```

`step` 是关键：它把「同一轮对话的第几步」从游标变成请求内容的可数属性。Host 每个 provider step 追加一条 assistant message（`prompt.ts:1186` 已证），所以 step 在请求里客观存在，不需要 mock 记账。

这同时解释了 `pathCursor` 从一开始就不必要：它记的东西请求里本来就有。

---

## 十、TOML schema

JSON 不适合人读剧本：无注释、引号噪声、多行文本必须转义、深层嵌套强制大量括号。TOML 恰好解决这四项。

短期成本是写一个 schema 校验器与编译器；长期收益是剧本可被人直接审阅——而剧本是这套架构里唯一描述「模型会怎么回应」的地方，可读性直接决定它能否被信任。

依赖已在 `package.json`：`smol-toml@1.7.0`。

### 完整示例

```toml
scenario = "orchestrator-publish"
description = "ORCH-005 短 CAS 发布；ORCH-007 target 未变时 ff-only"

# 根级键必须全部位于任何表头之前（TOML 语法要求，见下方风险）
must = ["publish", "review.second-perfect"]

flow = [
  { prompt = { agent = "fast-orchestrator", text = "Ship the parser fix." } },
  { wait = "orch.fork-manager" },
  { wait = "review.second-perfect" },
  { wait = "publish" },
  { assertFacts = { name = "Published", eq = 1 } },
]

# ═══ 对话 ═══════════════════════════════════════════════
# 唯一内容来源。(turn, step) → 响应，纯函数

[[turn]]
id    = "orch"
user  = "Ship the parser fix."
tools = ["fork-manager", "join"]

  [[turn.step]]
  id   = "fork-manager"
  tool = "fork-manager"
  args = { agent = "fast-manager", prompt = "Fix the parser and publish." }

  [[turn.step]]
  tool = "join"

  [[turn.step]]
  text = "Published."


[[turn]]
id    = "manager"
user  = "Fix the parser and publish."
tools = ["fork-agent", "join", "list"]

  [[turn.step]]
  tool = "fork-agent"
  args = { agent = "fast-coder", prompt = "Fix the off-by-one in tokenize()." }

  [[turn.step]]
  tool = "join"

  [[turn.step]]
  tool = "fork-agent"
  args = { agent = "fast-reviewer", prompt = "Review the candidate." }

  [[turn.step]]
  tool = "join"

  [[turn.step]]
  text = "Candidate ready for publish."


[[turn]]
id   = "review"
user = "Review the candidate."

  # REVIEW-003：两次 PERFECT。第二次的前缀必然包含第一次的
  # challenge tool result，因此是不同的 step，不是「同一条边第二次」。
  [[turn.step]]
  id   = "first-perfect"
  tool = "verdict"
  args = { verdict = "PERFECT" }

  [[turn.step]]
  id   = "second-perfect"
  when = { lastToolResultContains = "re-evaluate" }
  tool = "verdict"
  args = { verdict = "PERFECT" }

  [[turn.step]]
  text = "Review complete."


# ═══ 故障 ═══════════════════════════════════════════════
# 传输层，与内容正交。允许计数，因为物理投递次数是真实可数的。

[[fault]]
turn     = "manager"
step     = "fork-agent"
attempts = [1, 2]
delivery = "provider-error"
status   = 500

# ═══ 冷边界 ═════════════════════════════════════════════
# COMPANION-009 epoch 切换；FALLBACK-004 fallback 换边。
# 显式声明，不由 mock 嗅探推断。

[[epoch]]
session = "manager"
after   = "manager.join"
reason  = "epoch-switch"
```

### 设计要点

多行 user 文本零转义：

```toml
[[turn]]
id = "long-task"
user = """
Read AGENTS.md and report the architecture constraints.
Then fix the failing test in src/parser.
"""
```

内联表用于固定形状的小结构（`args`、`when`、`flow` 步骤），表头用于可变长序列（`turn`、`step`、`fault`、`epoch`）。这条区分让缩进恰好反映嵌套深度，而 JSON 里两者都是括号。

注释承载条款引用。这是 TOML 相对 JSON 最实质的收益：`# REVIEW-003：两次 PERFECT...` 就写在它约束的那两步旁边。当前 JSON 剧本里这类知识只能存在于 `.js` 匹配器的注释中，离 fixture 很远。

`id` 只在被 `flow`/`must`/`fault`/`epoch` 引用时才需要。未被引用的 step 不写 id——当前 JSON 强制每条边有 id，产生 `round1-failure` / `round1-retry` 这类为编号而生的名字。

### 已实证的排版风险

TOML 规定：根级键值对必须出现在任何表头之前。否则它们会被静默归属到最后一个表。

实测：把 `flow = [...]` 放在 `[[epoch]]` 之后，解析结果是 `epoch[0].flow`——不报错，语义已错。

因此 schema 校验器必须硬检查：

```text
根级 scenario / description / must / flow 必须先于第一个 [[turn]]
[[fault]] / [[epoch]] 之后不得再出现根级键
```

这是 TOML 唯一显著的可读性陷阱，必须由载入器堵住而不是靠作者记住。

### 缩进不是语法

`[[turn.step]]` 前的两格缩进对 TOML 无意义，纯粹为人眼分组。因此需要一个 formatter 保证全部剧本缩进一致，否则会漂移。归入 `scripts/` 的格式化工具，与 `pre-commit-formatter.mjs` 同层。

---

## 十一、正确形态汇总

```text
剧本 = 一个 TOML 文件 = 四份独立声明，静态一次性加载

1. turn    对话。人读形式；载入期编译为前缀索引
2. fault   有限故障计划。(turn, step, attempt 序号) → Delivery
3. epoch   显式冷边界。(session, step) → 允许重新封印
4. must    通过所需被命中的步集合（断言，不参与匹配）
```

不变量：

```text
静态全集。载入后不再变化，无 loadScripts
turn 无状态。删掉 fault 与 epoch 后，同一请求序列必得同一内容序列
运行时键 = (lane, turn, step)，三者皆为请求的纯函数
fault 有限且前置。运行期不新增、不推断
epoch 显式。未声明处前缀断裂即失败
匹配只读 provider-visible bytes
歧义由载入期校验排除，不由运行时打分排除
mock 不推导 Role / Agent / tier
prompt 中无测试专用标记
书写形式是对话，前缀索引是编译产物
```

---

## 十二、瀑布流重建顺序

不允许边改边跑。顺序固定，每步产出可独立审阅：

```text
K1  ProviderSemanticProjection 规范化函数（与生产 VERIFY-007 同一定义，两侧对拍）
K2  运行时键提取：(lane, turn, step) 三个纯函数 + 前缀索引查询
K3  Delivery 与 fault 计划求值（纯函数）
K4  epoch 冷边界声明与前缀封印验证
K5  TOML schema + 载入期编译器 + 六项载入期校验 + 根键顺序硬检查
K6  TOML formatter（缩进一致性）
K7  旧字段拒绝器：turn 编号 / reusable / pathless / blocking / loadScripts / specificity
K8  逐个 canary 手工重写：从 flow 反推对话；合并 restart 前后文件；JSON → TOML
K9  删除 strict-mock-forest.js / strict-mock-matches.js 旧匹配路径
K10 gate-testkit 森林自检：纯函数性、索引无冲突、fault 有限、无死边
```

K8 是唯一大量手工劳动。22 个 script 文件、约 250 条边，合并后预计降至 19 个文件。必须手工，不得脚本批量转换——旧边的 `match` 谓词与新对话步不是机械对应，`fallback.json` 那 8 条实为 4 步对话加 4 条故障，机械转换会保留错误结构。

---

## 十三、验收

```text
静态性
[ ] 无 loadScripts，flow 中无剧本变更动作
[ ] host-restart 与 host-restart-after 已合并
[ ] 两个 orchestrator recovery 文件已合并
[ ] 载入期六项校验各有失败测试
[ ] 根键顺序检查有失败测试（键在表头之后必须报错）

纯函数性
[ ] 无 pathCursor / turn 编号运行时使用
[ ] 无 seal 缓存删除
[ ] 无 reusable / pathless / blocking 匹配标志
[ ] 森林自检：同请求序列 → 同内容序列

匹配纪律
[ ] strict-mock-matches.js 无 requestRoleOf
[ ] 无 specificity 打分
[ ] 无 NUDGE_MARKERS 等 prose 常量
[ ] 无 __testkitHeaders 参与内容匹配
[ ] 无 extractLastUserMsg 截断（2000 字符）
[ ] 生产 prompt 中无 "Role canary" 类测试标记

可读性
[ ] scripts/*.toml，无 .json 剧本残留
[ ] 一段对话可自上而下读完，无手写前缀数组
[ ] 条款引用以注释形式写在被约束的步旁边
[ ] formatter 幂等：格式化两次结果相同
[ ] 前缀索引仅为编译产物，可 dump 但不是源

行为
[ ] 所有 canary 单轮通过
[ ] 关键 canary（fallback / review / orchestrator publish / restart recovery）12 轮通过
```

---

## 十四、为什么会腐坏

每个补丁单独看都合理：

```text
retry 需要不同响应        → 对 error 删除 seal 缓存
两次 PERFECT 看起来一样   → 合并模板 + 计数等待
重启后同一 prompt 复现    → flow 中途换剧本
epoch 切换破坏前缀        → tools+system 相同则放行
fallback 改了 system      → model 变化则放行
分不清 coder 和 inspector → 嗅探工具集合
判断顺序有依赖            → 硬编码优先级
```

七次局部修补，七次都打在错误的层。全局模型（内容是纯函数）从未错，错的是把四类不属于内容域的东西压进了内容域：传输事实、身份推断、冷边界声明、时间演化。

代价不是「有点乱」。代价是mock 现在能对错误的生产行为给出绿灯：

```text
epochCold      放过不该发生的 epoch 切换（tools+system 未变即通过）
specificity    两条边同时命中时静默选一条
requestRoleOf  与生产 role 推导不一致时按自己那套判
loadScripts    重启后匹配空间被换掉，原本该暴露的错命中消失
```

一个能对错误实现给出绿灯的验证装置，比没有验证装置更危险。

剧本作为 mock 的压缩表示法本身是这套架构的亮点——压缩率高、可审阅、确定性可证。要保住这个亮点，压缩必须无损：压掉重复的对话前缀，不压掉语义。
