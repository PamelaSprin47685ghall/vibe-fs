本文件是需求，也是台账。每解决一部分，就编辑本文件改成一部分完成时，然后 git commit。要并发工作，不需要按次序工作。

---

你这个方向是对的，而且从这份仓库看，已经到了一个非常适合做 **“Refactor Closure / 旧世界清仓”** 的节点。

更关键的是：你们仓库自己的设计原则其实已经写出了这次行动的理论依据——**兼容性必须有明确债权人；说不出谁会 break、持有什么旧 contract、何时退出，就不应该永久存在。**  另一个规则甚至已经把完成态定义得很漂亮：**新架构不再是 preferred architecture，而 simply is the architecture。** 

所以我不建议再发起一轮普通“重构”。这次应该给它一个完全不同的项目性质：

# Operation Clean Slate：把重构“收口”

目标不是继续改善设计，而是：

> **把 transition architecture 删除掉。**
>
> Git 保存过去；working tree 只描述现在。
> Compatibility 默认判死刑，举证后才能缓刑。

我看了你上传的完整仓库打包文件；下面直接给你一套可以交给工程师逐 PR 执行的 roadmap。

---

## 一、第一条规则先反过来：从“证明可以删”改成“证明必须留”

这是整个行动能不能成功的关键。

现在工程师脑内的规则大概是：

> “不知道删了会不会出问题，所以先留。”

改成：

> **“不知道为什么还需要，所以删。”**

唯一允许留下 compatibility 的四类理由：

| 类别                                   | 可以留下吗 | 要求                                   |
| ------------------------------------ | ----: | ------------------------------------ |
| 当前 repository 自己还在调用旧接口              |     ❌ | 迁调用者，然后删                             |
| “也许外面有人用”                            |     ❌ | 没有 named consumer = 没有 contract      |
| 真实 external consumer                 |  ✅ 暂时 | consumer + contract + exit condition |
| 历史 durable data 必须读取                 |  ✅ 暂时 | **decode-only ingress**，禁止旧 writer   |
| rolling deployment / rollback window |  ✅ 暂时 | convergence condition，达成即删           |
| “以后可能用”                              |     ❌ | Git history                          |
| “删了不好找回来”                            |     ❌ | Git history                          |
| “已经写了，留着成本不高”                        |     ❌ | 每条 path 都增加 state space              |

你们仓库其实已经精确写出了这个原则：historical durable data 可以只在 persistence ingress decode；current write 必须只有一种 canonical form；没有 named consumer / real old data 就连 compatibility test 一起删。

建议把这句话直接变成此次 cleanup 的最高规则：

> **Name the creditor. Name the exit. Or delete the debt.**

---

# 二、不要先删代码：先建立一张 Compatibility Ledger

第一批 PR **不改行为**。

创建一个临时文件，比如：

```text
cleanup/legacy-ledger.md
```

注意，这是此次行动的临时工作台，**cleanup 完成后它自己也必须删除**。

每发现一项旧痕迹，只允许登记以下字段：

| 字段                | 含义                                                          |
| ----------------- | ----------------------------------------------------------- |
| ID                | `LEGACY-001`                                                |
| Surface           | 旧字段 / alias / adapter / parser / writer / test / gate / doc |
| Current owner     | 当前模块                                                        |
| Old world         | 它在兼容什么                                                      |
| Current consumer  | 谁今天真的需要它                                                    |
| Consumer evidence | callsite / durable sample / external contract / deployment  |
| Writer alive?     | 是否还能制造旧数据                                                   |
| Reader alive?     | 是否还能接受旧数据                                                   |
| Classification    | DELETE / MIGRATE / BOUNDED-COMPAT                           |
| Exit condition    | **什么事实成立后它必须消失**                                            |
| Owner             | 谁负责删                                                        |
| Removal PR        | 最终删除 PR                                                     |

有一条非常重要：

**不允许 `UNKNOWN → KEEP`。**

只能：

```text
UNKNOWN → investigate → DELETE
UNKNOWN → investigate → BOUNDED-COMPAT
```

如果没有证据，就是 DELETE。

这可以彻底逆转团队心理。

---

# 三、我建议你们按 6 个“尸体类型”扫仓库，而不是按目录扫

这是我认为最重要的执行方式。

不要：

```text
今天清 Mission/
明天清 Execution/
后天清 Persistence/
```

这样很容易漏掉跨层 transition。

应该按**旧世界形态**一次杀穿全仓。

---

## Wave 1：死壳 / no-op / 已经没有调用者的 transition API

这是风险最低、收益最高的一批。

你们现在已经有一个非常漂亮的靶子：

`ManagerActivation` 自己明确写着：

* legacy Activation vocabulary；
* production 不再发送 `ManagerWorkActivation`；
* `WorkActivated` 只剩 inert legacy decode；
* production Activation path 已删除。

更值得注意的是，我对整个打包仓库搜索 `ManagerActivation.ensureAccepted`，**只有两个命中，而且都在 HOW 文档里，没有生产调用点。** 

这就是非常典型的：

> “功能已经没了，但旧 architecture vocabulary 还站在那里。”

### 这里不要“简化 ManagerActivation”。

直接做：

```text
ManagerActivation.ensureAccepted
        ↓
确认无生产调用
        ↓
删除 ManagerActivation module
        ↓
删除 EnsureAcceptedResult
        ↓
删除 architecture whitelist / dependency
        ↓
删除测试
        ↓
修 HOW
```

**不要留下：**

```fsharp
[<Obsolete>]
module ManagerActivation
```

也不要：

```fsharp
let ensureAccepted ... = Ready ...
```

更不要改名：

```text
LegacyManagerActivation
```

都属于给尸体换棺材。

### Wave 1 Done

搜索：

```bash
rg 'ManagerActivation|ManagerWorkActivation'
```

允许出现的位置应该最多只剩：

```text
CHANGELOG / historical ADR
```

如果连历史说明都没有持续价值，**零命中更好。**

---

# 四、Wave 2：内部 compatibility adapter —— 这是最大头

这一类通常是“舍不得删”的核心。

你们代码里已经存在非常明确的例子：

```fsharp
/// Compatibility single-result join ...
/// Projects JoinItem → RunCompletion for callers that still need agent Outcome.
let join ...
```

也就是说，新世界已经有 `JoinItem`，但还保留 `RunCompletion` compatibility projection 给“still need”的内部调用者。

这正是本轮应该重点追杀的对象。

做法不是删 adapter 看测试炸。

而是：

```text
Compatibility adapter
        ↓
枚举所有 caller
        ↓
逐 caller 判断“为什么还需要 old representation”
        ↓
把 caller 改成直接消费 canonical representation
        ↓
adapter 调用数 → 0
        ↓
删 adapter
        ↓
删 adapter tests
        ↓
删旧类型（如果无其它职责）
```

你的指标不是：

> compatibility code 少了多少。

而是：

> **compatibility adapter 的 first-party caller 数必须单调下降到 0。**

### 每个 PR 都要求一个数字

例如：

```text
JoinItem → RunCompletion compatibility callers

before: 11
after:   7
remaining: 7
```

下一 PR：

```text
7 → 3
```

最终：

```text
3 → 0
delete adapter
```

这比“感觉代码干净了很多”强得多。

---

# 五、Wave 3：Deprecated 字段——最容易永生的一类

我建议把所有 `DEPRECATED` 直接当 P1 defect，而不是技术债。

仓库里已经有明确实例：

`RunCompletion.AgentId` 被标记为：

> DEPRECATED；为了 HostFork backward compatibility 保留；新代码应该使用 Map key 或 AgentName。

这就是标准 cleanup ticket。

不要继续问：

> “删 AgentId 会不会影响哪里？”

换一个问题：

> **“谁今天还消费 RunCompletion.AgentId？”**

然后把答案做成 call graph。

你目前至少还能看到 compatibility projection 仍在制造这个字段，例如 PTY → `RunCompletion` 时继续填写 `AgentId`。

所以正确顺序是：

```text
1. 找 read sites
2. 替换 read sites
3. 禁止 new code read deprecated field
4. field 变 write-only
5. 删除 writer
6. 删除 field
7. 删除 codec / fixture / test 中对应形状
```

### 特别推荐增加一个临时 gate

不是：

```text
禁止 AgentId 出现
```

因为 AgentId 本身可能是合法概念。

而是针对精确 AST/type surface：

```text
RunCompletion.AgentId forbidden
```

这样 migration 有棘轮效应：

```text
12 callers → 8 → 4 → 0
```

不会被下一个工程师重新加回来。

最终删除字段时，**这个临时 gate 也一起删除**。

不要留下纪念碑。

---

# 六、Wave 4：Persistence compatibility —— 这里绝不能简单“一刀全删”

这一层要最谨慎。

因为你的仓库目前实际上同时存在两种非常不同的 legacy 行为。

### A. 正确的 clean break

`FactCodec` 对一些无法无损解释的旧 journal 明确拒绝：

```text
pre-0.5.0 → reject
ScoreVectorRef-era → reject
unanchored PairProgrammingGuideline → reject
```

这是健康的。

因为代码不是“兼容旧世界”，而是在**拒绝把旧世界解释成当前世界**。

而 durable-events 甚至已经明确规定旧物理 store：

> 不读、不迁、不 reset、不双写；禁止 legacy importer、migrator、fallback-to-old-store shim。

**这种 refusal boundary 不属于兼容债。**

可以保留。

甚至应该比“智能兼容”更偏爱它。

---

### B. 真正还活着的 migration code

但同一个 `FactCodec` 里也还有：

```fsharp
migrateHandleCompleted
migrateHandleOwnership
migrateHandleByname
migrateManagerJobByname
rewriteLegacyObservationTags
```

而最终 `deserializeFact` 确实依次运行这些 migration。

例如 `HandleCompleted` 旧记录缺字段时，目前会自动注入 `null`。

这类不能因为名字叫 migrate 就直接删。

每一个都必须回答：

```text
还有没有真实 durable sample？
这些 sample 最晚可能活到什么时候？
用户是否承诺升级可跨越这个版本？
是否已有 retention horizon？
```

然后分三类：

```text
有真实旧数据 + 必须支持
    → KEEP decode-only + exit condition

无真实旧数据
    → DELETE

无法知道
    → instrumentation / fixture inventory
      不允许直接 KEEP forever
```

### 一个关键原则

允许：

```text
OLD bytes
  ↓
one decoder
  ↓
CURRENT domain
```

禁止：

```text
OLD bytes ↔ OLD model ↔ adapter ↔ CURRENT model
                       ↕
                   new writer
```

你们自己的 rulebook 已经规定了这个 asymmetry：historical durable compatibility 如果需要，可以 decode-only；不要留下旧 writer。

---

# 七、Wave 5：明确有“债权人”的 compatibility —— 不删，但关进隔离区

这是这次 cleanup 非常容易误伤的一类。

例如你们现在有：

> `Host TodoTable compatibility sink`

而且 HOW 明确说：

* 它服务当前 Host V1；
* canonical truth 不依赖它；
* compatibility 不属于永久需求；
* 未来 sink 可以整体替换。

WHAT 也已经把架构画得很正确：

```text
MagicTodoProjection / Journal facts = canonical truth
Host TodoTable                       = compatibility sink only
```

并禁止 sink 反推 canonical。

**这个不要现在硬删。**

因为它目前至少有一个具名债权人：

```text
OpenCode Host V1 TodoTable
```

但现在缺的应该是：

```text
EXIT CONDITION
```

把它改造成显式的：

```text
COMPAT-001

Creditor:
  OpenCode Host V1 TodoTable

Ingress/Egress:
  canonical obligation → V1 projection only

Forbidden:
  V1 → canonical reconstruction

Exit:
  Host V1 TodoTable no longer part of supported host contract

Owner:
  host-boundary

Removal:
  delete Surface.CompatibilityTodoRow
  delete obligationsToCompatibilityRows
  delete V1 canaries
```

这样 compatibility 不再是：

> “最好别动。”

而变成：

> **“这个东西已经被判死刑，只是执行日期由某个客观条件决定。”**

---

# 八、Wave 6：迁移代码比兼容代码更危险——尤其是“修复历史错误”的 runtime migration

你们还有一类非常典型：

`JoinDrain` 中存在：

```text
migrateRetiredFalseAbort
tryMigrateRetiredFalseAbort
migrateOutcomeToUnit
```

而注释直接说明这是：

> “Retired legacy false abort: deterministic replacement + correction”。

另外还存在：

> “Execute replacement migration when blob identity is known.” 

这一类值得单独做 **Migration Amnesty Review**。

因为迁移逻辑经常是最难删除的代码：

```text
“还有没有人处于迁移前状态？”
        ↓
“不知道”
        ↓
“那先留”
        ↓
永久 runtime architecture
```

对每个 runtime migration 强制问：

```text
它修复的是哪个版本以前制造的数据？

新版本还会制造坏数据吗？

坏数据有没有有限集合？

能否改成：
  离线一次性 repair
而不是：
  runtime 永远懂 repair？

有没有 observable evidence 表明坏数据已经为零？
```

如果系统允许 shock cut / archive-and-restart，那么很多 migration 可以进一步直接变成：

```text
detect → refuse
```

而不是：

```text
detect → reconstruct old semantics → rewrite → continue
```

这会让代码量和 state space 大幅下降。

---

# 九、第二轮不是删 production，而是删“防尸体复活的尸体”

这一步很多团队不会做。

重构之后经常会产生大量：

```text
FORBIDDEN_OLD_THING
LEGACY_TOKEN_GATE
NO_OLD_X
NO_V1_Y
absence-ratchet
```

它们在 migration 期间是对的。

**但它们不是永久 architecture。**

你们仓库已经出现这种情况。

例如 `js-surface-gate` 里还明确保存：

```text
js-student
js-teacher
JsStudent
JsTeacher
StudentCompileJs
...
```

作为 `FORBIDDEN_TOKENS`，目的只是确保旧 Student/Teacher world 不复活。

而 requirement 自己已经把这类东西标成：

> GARBAGE；`FORBIDDEN_TOKENS` 是 absence ratchet，**新世界基线稳定后可删**。

这句话非常重要。

### cleanup 的成熟度有三个阶段

```text
阶段 1
旧世界存在

阶段 2
旧世界删除
+ gate 禁止它复活

阶段 3
设计本身使旧世界不可表达
+ 旧名字已经失去文化记忆
+ 删除针对旧名字的 gate
```

你现在应该开始从 2 → 3。

也就是说：

不要永远维护：

```text
NO_STUDENT_TEACHER_REANIMATION_GATE
```

而应该最终靠：

```text
capability ownership rule
role projection rule
type system
positive architecture gate
```

使其无法重新产生。

---

# 十、`unified-store-gate` 是另一个值得“去考古化”的对象

它现在还记得不少历史：

* Student QA revival；
* no-migrator；
* legacy importer；
* dual-write；
* 甚至注释里写着某个旧 ratchet 已于 **2026-08-14 retired**。

这在迁移期非常有价值。

但最终建议把它拆成：

```text
历史 token gate
        ↓
逐步淘汰

永久 architecture invariant
        ↓
保留
```

例如：

不要永久检查：

```text
LegacyMigrator
LegacyImporter
JournalToEventStore
StudentQaMigrator
```

而检查真正永久的性质：

```text
production durable writer ownership = exactly one

runtime store roots ∈ allowed roots

all writes enter canonical EventStore

business modules cannot own durable backends
```

**Positive invariant > blacklist of historical mistakes。**

因为 blacklist 本身也会让未来工程师不停看到已经死亡的 ontology。

---

# 十一、然后做“墓碑文档清理”

你们现在的 HOW/WHY 中有不少：

```text
GARBAGE
历史与弃权
被拒方案
旧 XXX
```

在设计形成阶段非常有用。

但如果最终目标是：

> working tree 描述现在，

就应该开始区分两种历史知识。

### 必须保留

解释**当前奇怪设计为什么必须如此**的 rationale。

例如：

```text
为什么 historical ambiguous record 必须 fail closed
```

这是现在仍然有效的知识。

### 应该删除/归档

只是记录：

```text
我们以前有 A
后来删了 A
A 还有 A1/A2/A3 字段
曾有工具 FooOld
```

而这些信息对理解当前设计已经没有贡献。

这些应该：

```text
Git history
或 ADR archive
```

而不是继续出现在 active HOW。

最终应该努力让：

```text
WHAT = 永久 contract
HOW  = 今天怎么实现
WHY  = 今天为什么这样设计
```

而不是：

```text
HOW = 今天 + 前三朝考古现场
```

---

# 十二、建议具体按下面的 PR train 做

这是我会实际采用的提交顺序。

| PR        | 内容                                                        | 风险 |
| --------- | --------------------------------------------------------- | -: |
| CLN-00    | 建 legacy ledger + cleanup policy                          | ✅ 完成 |
| CLN-01    | 清死代码、无 caller module、commented implementation             | 极低 |
| CLN-02    | 删除 `ManagerActivation` no-op vocabulary + stale HOW/tests | ✅ 完成 |
| CLN-03    | `RunCompletion.AgentId` caller migration                  | ✅ 完成 |
| CLN-04    | 删除 deprecated `RunCompletion.AgentId`                     | ✅ 完成 |
| CLN-05    | Join single-result compatibility caller migration         |  ✅ |
| CLN-06    | 删除 `JoinItem → RunCompletion` internal compatibility path |  ✅ |
| CLN-07    | FactCodec legacy migration inventory，只分类不删                |  ✅ |
| CLN-08..N | 每种 durable decode 单独裁决（LEGACY-013 已删除，LEGACY-010/011/012/014 BOUNDED-COMPAT 保持） | 中高 |
| CLN-X     | `false abort` runtime migration retirement                |  高 |
| CLN-Y     | Host V1 compatibility sink 加 creditor + exit contract     |  低 |
| CLN-Z     | retire historical absence ratchets                        |  ✅ |
| CLN-Z2    | active HOW/WHY historical tombstone cleanup               |  ✅ |
| FINAL     | 删除 legacy ledger 自身 + permanent architecture gates        |  低 |

注意：

**一个 PR 尽量只消灭一种 old-world concept。**

不要搞：

```text
cleanup legacy stuff
-143 files
```

那样 reviewers 最后一定因为不敢承担风险，把很多东西重新保回来。

---

# 十三、每个删除 PR 强制用同一个五步模板

这是“保姆级”的核心工作流。

```text
STEP 1 — ACCUSE
指出为什么它是 legacy：
“X exists only to support Y.”

STEP 2 — PROVE NO CREDITOR
搜索：
caller
writer
reader
test
fixture
public API
durable sample
deployment consumer

STEP 3 — MIGRATE
如果还有 repository-owned caller，
先迁 caller，不碰 compatibility implementation。

STEP 4 — DELETE
一次删除：
implementation
types
aliases
tests
fixtures
docs
flags
special cases

STEP 5 — ABSENCE PROOF
rg old-name
build
target tests
integration tests
architecture gate
```

尤其 STEP 4：

**不要只删 implementation。**

例如删除 `LegacyFoo` 时，目标是：

```text
LegacyFoo.fs              delete
LegacyFooTests             delete
LegacyFooFixture           delete
LegacyFooAdapter           delete
LegacyFooConfig            delete
LegacyFoo terminology      delete
LegacyFoo docs             delete
LegacyFoo TODO             delete
```

否则旧世界的“幽灵 ontology”还在。

---

# 十四、每个 compatibility survivor 都必须长这样

以后 review 里看到 compatibility，没有下面四句话就不准 merge：

```text
Compatibility creditor:
  <谁>

Old contract:
  <什么>

Boundary:
  <只允许在哪一层存在>

Exit condition:
  <什么可观察事实成立时删除>
```

例如：

```text
Compatibility creditor:
  OpenCode Host V1 TodoTable

Old contract:
  todos[{content,status,priority}]

Boundary:
  Mission/Obligation/Todo/Surface only

Exit condition:
  Host V1 TodoTable is removed from supported host contract.
```

严禁：

```text
// Keep for backwards compatibility.
```

这句话以后应该视为 lint error。

因为它什么信息都没提供。

---

# 十五、建立一个“删除预算”，不要建立“技术债 backlog”

我甚至建议每轮 cleanup 设 **negative LOC objective**。

不是 KPI 式盲删，而是方向性约束：

```text
本轮允许：
+ 100 行证明/architecture gate

但要求：
- 1000 行 transitional machinery
```

特别记录下面这些指标：

| Metric                               |         方向 |
| ------------------------------------ | ---------: |
| deprecated production fields         |        → 0 |
| internal compatibility adapters      |        → 0 |
| compatibility first-party callers    |        → 0 |
| runtime migrations                   |       → 极少 |
| dual representations                 |        → 0 |
| legacy aliases                       |        → 0 |
| old writers                          |        → 0 |
| compatibility without exit condition | → **绝对 0** |
| historical token blacklist           |          ↓ |
| GARBAGE/tombstone active docs        |          ↓ |
| canonical writers per semantic fact  |        → 1 |

真正重要的不是总代码行数。

而是：

> **一个 semantic fact 有几个 live representation / writer / path？**

目标永远是：

```text
1
```

---

# 十六、专门制定“奥卡姆剃刀 review 问句”

以后 code review 里不要问：

> 这个兼容代码有没有害？

问下面这些问题：

```text
如果把它删掉，具体谁会失败？

能给我 consumer 名字吗？

能给我真实 persisted sample 吗？

这是 read compatibility 还是 write compatibility？

为什么 current code 还能制造 old representation？

为什么 compatibility 不在 boundary？

为什么 repository-owned caller 不能迁？

这个 adapter 的 retirement condition 是什么？

如果三个月后没人记得它，代码自己能说明为什么还存在吗？

如果以后真需要它，为什么不能从 Git 找回来？
```

最后一问尤其重要。

因为你最开始说的那个心理：

> “怕删了找不回来”

在 Git repository 里，本质上是一个**错误的风险模型**。

删除的成本通常是：

```text
git log / git show / revert
```

保留的成本却是：

```text
每个新人阅读
× 每次搜索
× 每次重构
× 每次测试
× 每次设计
× 永久
```

---

# 十七、但一定要防止“奥卡姆剃刀”变成“大爆炸式删库”

这点我反而建议你很克制。

你们仓库已经明确提醒：

> anti-cruft 不是破坏真实 contract 的许可证。

所以不要下命令：

> “把所有 legacy、compat、migration 全删掉。”

正确命令是：

> **“所有 legacy、compat、migration 全部重新接受审判。”**

默认 verdict 是 DELETE。

但下面三种必须无罪：

```text
真实 public compatibility
真实 durable decode
真实 deployment overlap
```

区别在于它们不再拥有“永久居留权”。

只是：

```text
bounded exception
```

---

# 十八、我认为你这个仓库现在最值得先砍的四刀

根据当前代码，我会按这个顺序开工。

### 第一刀：`ManagerActivation`

这是最漂亮的 starter PR。

源码自己承认 production path 已删除、模块只剩 no-op vocabulary；而全仓精确搜索 `ManagerActivation.ensureAccepted` 只有 HOW 文档命中。

**目标：0 source occurrence。**

这刀可以给团队建立“真的可以删，而且删完世界没有塌”的信心。

### 第二刀：`RunCompletion.AgentId`

源码已经明确标 `DEPRECATED`、只因 backward compatibility 保留。

把所有 first-party read site 迁掉，然后删除字段。

这是训练团队：

> deprecated ≠ 永久供奉

的最好案例。

### 第三刀：single-result Join compatibility

`JoinItem` 已经是新 representation，但代码还明确给 “callers that still need agent Outcome” 做 `RunCompletion` projection。

迁完这些 caller，然后删 compatibility API。

这一刀开始真正降低 architecture state space。

### 第四刀：FactCodec compatibility census

**先不删。**

把：

```text
migrateHandleCompleted
migrateHandleOwnership
migrateHandleByname
migrateManagerJobByname
rewriteLegacyObservationTags
```

每项单独建立 creditor / durable-sample / exit-condition。

因为这些是最可能既包含真需求、又包含历史恐惧的地方。当前 deserialize pipeline 明确仍会调用它们。

这刀会告诉你真正还剩多少“必须背负的过去”。

---

# 十九、最终完成态不是“没有 legacy 这个单词”

真正的最终态应该是：

```text
Production
    一个 canonical ontology
    一个 authoritative writer
    一个正常 execution path

Compatibility
    只在物理 boundary
    只服务 named creditor
    通常 decode/project one-way
    每条有 exit condition

Tests
    验 current behavior
    验 permanent architecture invariant
    不供奉已删除 ontology

Docs
    描述当前 system
    rationale 保留
    尸体清走

History
    Git 负责
```

这恰好就是你们仓库已经写出的 invariant：

> **Current code has one canonical model; compatibility exists only at boundaries where a real supported past still touches the present.** 

以及我认为最适合成为此次工程结束语的那一句：

> **The migration machinery has nothing left to arbitrate.
> The new architecture is not “preferred.” It is simply the architecture.** 

如果按这个 roadmap 执行，我建议内部不要把它叫“代码清理”或者“第五轮重构”。

叫 **Refactor Closure** 更准确。

因为前几轮是在建设新世界；**这一轮是在宣布旧世界不再享有公民权。**

---

我们把它定成一次**从“Fable 测试适配”迁移到“JS-native semantic architecture”**的系统改造。

终态不是“测试更好写”，而是：

> **所有测试都是 JS；所有值得测试的语义都有正式、稳定、JS-native 的边界；实现细节没有边界，因此 JS 根本无法依赖。**

这和仓库已有的测试哲学完全一致：测试应落到 supported input / observable result / durable state / contractual interaction，并允许内部 rename、inline、换数据结构而不受影响。

---

# 0. 先冻结“宪法”

在动代码之前，先把以下六条写进新的 requirement，例如：

```text
requirements/js-semantic-surface/
  README.md
  WHAT.md
  WHY.md
  HOW.md
  PROOF.md
```

内容不要写成“解决 mangled name”，那只是 symptom。

写成：

1. **所有 automated tests 使用 JavaScript。**
2. **JS semantic tests 只能调用正式 semantic surface。**
3. **值得独立测试的 law 必须有独立 semantic owner + JS surface。**
4. **不拥有独立 law 的 helper 不直接测试。**
5. **semantic data 跨边界必须是 JS-native representation。**
6. **Fable runtime representation 不属于 semantic contract。**

再加一句非常重要的：

> A surface exists because a semantic component owns a contract, never because a test needs access.

### JS-native 的定义

普通数据只允许：

```text
string
number
boolean
null / undefined
array
plain object
Promise
JS function/callback
```

必要时可以有：

```text
bigint
opaque resource handle
```

但 opaque handle 只能：

```text
create → pass back → dispose
```

JS 不得读它的 fields/prototype。

禁止作为 semantic data 暴露：

```text
FSharpList
FSharpMap
FSharpSet
FSharpOption
FSharpResult
F# DU instance
F# record runtime class
tag
fields
cases()
Fable DateTimeOffset encoding
curried F# function
mangled instance method
```

---

# 1. 先做 inventory，暂时不改行为

第一步不是写新 API。

先弄清现在 JS 测试到底获得了多少“不该有的权力”。

新增一个临时 inventory script，例如：

```text
scripts/test-surface-inventory.mjs
```

扫描全部：

```text
requirements/**/tests/**/*.mjs
```

记录五类债务。

### A. deep production import

例如：

```js
import '../../../dist/Execution/Session/...'
import '../../../dist/Foundation/...'
import '../../../dist/OpenCode/...'
```

### B. Fable export discovery

例如：

```js
Object.keys(mod)
Object.entries(mod)
startsWith('Foo__Bar_')
endsWith('_Baz')
```

你仓库已经有明确实例：`SessionQuiescenceGate` 测试直接扫描 mangled methods。

### C. Fable representation knowledge

搜索：

```text
.tag
.fields
.cases()
FSharpList
FSharpMap
fable_modules
```

### D. legacy interop authority

搜索：

```text
member(
bind(
fableInstanceMethod(
prod(
toList(
caseOf(
payloadOf(
resultOf(
```

现有 `interop.mjs` 明确承担了 emitted-name resolution、Fable mechanics，而且集中加载大量内部 production modules。 

### E. 合法的 compiler/build verification

**不要误杀。**

例如现有：

```text
VERIFY_008_every_emitted_module_actually_loads
```

故意 import 所有 emitted JS 来证明 Fable build 真能 link。这个测试的 subject 就是编译产物，因此它有资格知道 `dist`。

把这种测试明确分类成：

```text
compiler/build verification
```

而不是 semantic test。

---

# 2. 立刻加“只减不增” gate

inventory 完成后，**马上阻止债务继续增长**。

不要等迁完才加 gate。

建立：

```text
requirements/verification-system/tests/js-boundary-gate.test.mjs
```

规则：

```text
新 semantic test:
    禁止新增 deep dist import
    禁止新增 mangled-name lookup
    禁止新增 Fable representation knowledge
    禁止新增 interop.mjs dependency
```

现存违规先进入临时 baseline：

```text
requirements/verification-system/tests/fixtures/
  legacy-js-boundary-debt.json
```

原则：

```text
baseline 可以删
baseline 不可以加
```

每迁一个测试，就删一个 baseline entry。

### 为什么先做这个？

否则你迁 30 个，别人又新增 20 个。

仓库自己的 boundary rule 已经明确提出应该机械扫描 dependency：foreign layer 只能指向正式 supported entry，禁止 deep path / generated detail。

---

# 3. 定义“surface”是什么，不是什么

这一步尤其重要，否则很快就会造出第二代 `domain.mjs`。

## 错误设计

```text
src/Wanxiangshu/TestApi.fs
```

里面：

```fsharp
let callJoinDrain = Internal.JoinDrain.drainFromJournal
let makeFact = ...
let internalState = ...
let callPrivateThing = ...
```

这是 **test facade**。

禁止。

同样禁止：

```text
PublicFacade
    = re-export everything internal
```

仓库现有规则也明确把这种做法列为假修复。

---

## 正确设计

surface 跟着 semantic owner 走。

例如：

```text
Host/Quiescence/
  Model.fs
  Policy.fs
  Surface.fs

Participant/Provider/Attempt/
  ...
  Surface.fs

Context/Prefix/
  ...
  Surface.fs
```

不是一定必须叫 `Surface.fs`。

也可以叫：

```text
Api.fs
Contract.fs
```

重点是：

> 它属于这个 subsystem，不属于 Tests。

并且它不是简单 forwarding。

它负责：

```text
JS representation
        ↓
semantic input
        ↓
owner
        ↓
semantic output
        ↓
JS representation
```

---

# 4. 先迁一个“纯语义 pilot”

不要第一枪就挑最复杂 Host runtime。

先选一个：

* 输入清晰；
* 输出清晰；
* 没有 resource lifecycle；
* 现在却通过 `domain.mjs` / Fable representation 测试；

的 pure component。

目标形式：

```js
const result = component.operation({
  ...
})

assert.deepEqual(result, {
  ...
})
```

而不是：

```js
const input = toList(...)
const result = resultOf(...)
assert.equal(caseOf(result), ...)
```

---

## pilot 的工作步骤

假设原测试是：

```js
const result = resultOf(
  InternalModule.someFunction(
    sessionId('s1'),
    toList(items)
  )
)

assert.equal(caseOf(result.error), 'Conflict')
```

### 第一步：先写 promise

不用看实现，写：

> Given X, when Y happens, the component rejects it as a conflict.

如果这句话写不出来，先别设计 API。

### 第二步：设计 JS representation

目标：

```js
const result = component.someOperation({
  sessionId: 's1',
  items: [...]
})

assert.deepEqual(result, {
  ok: false,
  error: {
    kind: 'conflict'
  }
})
```

### 第三步：F# 内部继续保持 F# idiom

内部完全可以还是：

```fsharp
SessionId
Item list
Result<'a, Conflict>
Map<...>
DU
```

不要为了 JS 把 domain 污染成 primitive soup。

### 第四步：surface translation

逻辑上：

```text
"s1"
 ↓
SessionId.create

JS array
 ↓
Array.toList

Result<_, DU>
 ↓
{ ok, value/error }
```

转换发生在 owner boundary。

### 第五步：删测试里的 interop helpers

完成后，这个 test 不得再出现：

```text
sessionId()
toList()
resultOf()
caseOf()
```

---

# 5. 给 surface 本身建立 contract test

每建立一个正式 surface，都要有一个非常小的 API contract test。

你仓库已有 `guide-contract.test.mjs` 的机制可以复用：它会检查 emitted surface 的函数是否存在，甚至 pin exact surface。

例如：

```js
import * as quiescence from '...stable surface...'

assert.deepEqual(
  Object.keys(quiescence).sort(),
  [
    'beginAttempt',
    'create',
    'dropSession',
    'observeIdle',
    'revoke',
    'tryConsume',
  ]
)
```

注意：

**只有正式 contract surface 才 pin 名字。**

内部 module 的 emitted names 不 pin。

这正是我们需要的区别：

```text
internal rename
    → irrelevant

public surface rename
    → breaking contract
```

---

# 6. 第二个 pilot：专门攻克 stateful abstraction

接下来迁 `SessionQuiescenceGate` 这类东西。

这是很好的代表，因为现在测试实际上知道：

```text
SessionQuiescenceGate
BeginProviderAttempt
ObserveIdle
TryConsume
RevokeCurrentAttempt
DropSession
```

并通过 mangled method discovery 调用。

而 production implementation 内部实际上维护 `serials` 和 `activities` 两张 mutable map。

这些 state **JS 不应该知道**。

---

## surface 可以长成

```js
const gate = quiescence.create()

quiescence.beginAttempt(gate, 's1')

const permit =
  quiescence.observeIdle(gate, 's1')

assert.equal(
  quiescence.tryConsume(gate, permit),
  true
)

assert.equal(
  quiescence.tryConsume(gate, permit),
  false
)
```

这里：

```text
gate
permit
```

可以定义为 **opaque handle**。

测试只能：

```text
拿到
传回
```

不能：

```js
gate.serials
permit.fields
permit.tag
```

这样将来内部：

```text
Map → Dictionary
serial → generation token
class → actor
mutable state → immutable state + cell
```

JS 测试完全不变。

当前 gate 本身的语义已经非常清楚：新 provider attempt 使旧 permit 失效；idle 产生 permit；permit 只能消费一次；drop/revoke 使旧 permit 无效。

这就是应该发布的 law。

而不是它当前由哪两张 Map 实现。

---

# 7. 建立统一的 representation rules

两个 pilot 完成后，不要继续自由发挥。

把经验固化成规则。

建议建立一个非常小的测试 helper：

```text
requirements/verification-system/tests/support/
  js-contract.mjs
```

它**不是 domain facade**。

它只检查 representation：

```js
assertJsData(value)
assertOpaque(value)
```

比如递归拒绝：

```text
.cases()
.fields + numeric tag union shape
FSharpList tail/head representation
FSharpMap runtime object
Fable reflection metadata
```

最好进一步规定：

> 除 opaque resource handle / callback / Promise 外，semantic values 必须是 JSON-shaped。

那就非常容易理解：

```js
JSON.stringify(result)
```

理论上应该工作。

### 时间也建议归一

不要让 JS boundary 收到 Fable DateTimeOffset。

优先：

```text
ISO-8601 string
epoch milliseconds
```

内部再转换。

现有 facade 专门验证过裸 JS `Date` 与 Fable DateTimeOffset 可以产生 silent timezone bug。

终态不应该是教每个测试正确构造 Fable DateTimeOffset。

终态应该是：

> JS 根本构造不了 Fable DateTimeOffset。

---

# 8. 开始 Wave A：纯函数 / algebra / projection

这是最大批、也是最容易批量迁的部分。

优先迁：

```text
decision
classification
projection
codec
rendering
validation
selection
planning
ordering
```

每个 test 严格套同一个模板。

## 单测试迁移 SOP

### 1. 读测试名和 requirement clause

先别看 helper。

问：

> 它究竟要证明哪句话？

---

### 2. 写成 Given / When / Then

例如：

```text
Given an old permit
When a new provider attempt begins
Then the old permit cannot authorize continuation
```

---

### 3. 圈出真正输入

不是：

```text
FSharpMap
DU tag
InternalProjection
```

而是：

```text
events
commands
identity
policy configuration
```

---

### 4. 圈出真正 observable

例如：

```text
decision
rendered output
durable facts
allowed/rejected
next semantic state
effect request
```

---

### 5. 删掉草稿里的 implementation nouns

如果测试设计里出现：

```text
private field
helper function
module emitted name
cache implementation
Map key layout
DU ordinal
```

重新设计。

---

### 6. 判断是否真的存在独立 law

如果没有：

**不要开 surface。**

改测它的 owner。

---

### 7. 如果存在，找到 semantic owner

把 boundary 放 owner 旁边。

不要塞进中央：

```text
TestApi
DomainFacade
InteropEverything
```

---

### 8. 设计 JS representation

先写理想 JS：

```js
const actual = capability(input)
```

再去写 F#。

不要从现有 F# type 倒推 JS shape。

---

### 9. 写 surface contract test

证明：

```text
名字稳定
参数语义稳定
输出 JS-native
```

---

### 10. 重写原 behavior test

此时测试中 Fable vocabulary 应归零。

---

### 11. 做 positive canary

故意：

```text
rename helper
inline helper
change internal collection
reorder pure calculations
```

测试仍 green。

---

### 12. 做 negative canary

故意：

```text
return wrong decision
publish twice
accept stale permit
swap identity
```

测试必须 red。

这就是你仓库规则要求的“双向验证”。

---

### 13. 删 legacy dependency

删除：

```text
domain.mjs import
interop helper usage
direct dist import
baseline entry
```

**一个测试完成迁移的定义就是 baseline 少一项。**

---

# 9. Wave B：state machine / resource

接着处理：

```text
SessionQuiescenceGate
AttachedSessionRuntime
CompletionMailbox
ForkRuntime
process lifecycle
shared runtime resources
```

这些不要暴露 internal state snapshot。

优先 surface 成：

```text
create/open
command
observe
dispose
```

例如：

```js
const runtime = runtimeApi.create(config)

await runtimeApi.start(runtime, input)

const result =
  await runtimeApi.join(runtime)

runtimeApi.dispose(runtime)
```

opaque handle 不属于 semantic data。

它只是 capability token。

测试不能 inspect。

---

# 10. Wave C：effects

有副作用的 subsystem 尽量拆成：

```text
semantic decision
      ↓
effect request
      ↓
host interpreter
      ↓
effect result
      ↓
semantic transition
```

例如：

```js
const action = policy.decide(input)

assert.deepEqual(action, {
  kind: 'kill-process',
  processId: 'p1'
})
```

这部分可以大量 pure JS behavior tests。

然后单独：

```js
const actual =
  await processHost.execute(action)
```

测真实 effect boundary。

这样就不会为了测试 policy 而 mock 一大坨 Host。

---

# 11. Wave D：integration / plugin / e2e

这些本来就在真正的 external boundary 上，改动反而可能最小。

原则仍一样：

```text
发送真实 supported input
观察真实 supported output/effect
```

不通过内部 state 验证。

如果 E2E 失败需要 diagnostics：

diagnostics 可以存在，但必须是**正式 diagnostics contract**，而不是：

```text
__getPrivateStateForTests
```

---

# 12. 每完成一个 Wave，就收紧 gate

不要最后统一清理。

假设开始时：

```text
legacy violations = 180
```

Wave A 后：

```text
120
```

就把 baseline 永久降到 120。

Wave B：

```text
60
```

继续降。

直到：

```text
0
```

然后删除 baseline 机制本身。

最终 gate 直接：

```text
任何 semantic test deep-import internal dist
→ fail

任何 semantic test 使用 Fable representation
→ fail
```

---

# 13. `domain.mjs` 的退场路线

不要直接删除，因为现在它还是大量测试的 anti-corruption boundary。

当前设计本身很清楚：`domain.mjs` 是 transition entry，真正 Fable mechanics 在 `domain/interop.mjs`，family adapters 建在它上面。

所以分四步。

## 第一步

冻结：

> No new imports from `domain.mjs`.

## 第二步

每迁一个 family：

```text
identity
journal
context
execution
orchestrator
...
```

减少其 exports。

## 第三步

当普通 semantic tests 不再依赖 representation helpers 时，删除：

```text
bind
member
fableInstanceMethod
unionCase
prod
```

## 第四步

最后删除普通测试可见的：

```text
caseOf
payloadOf
toList
listItems
mapEntries
resultOf
unwrapOption
```

注意：

不是因为这些 helper 写得不好。

相反，它们现在非常有价值，甚至保护了真实 silent hazards。现有 meta tests 已经证明 JS array/FSharpList、DU ordinal、DateTimeOffset 等问题确实会产生静默错误。

删除它们意味着：

> **它们成功完成了迁移任务，以后普通测试已经到不了危险区域。**

---

# 14. 保留一个非常小的 Fable quarantine

这里不要走到另一种 dogma。

最终仍然可以有：

```text
requirements/verification-system/tests/compiler-interop/
```

这种测试专门验证：

```text
Fable output links
package emitted correctly
public JS surface exports correctly
compiler/runtime versions compatible
```

这些测试**有资格知道 Fable**。

因为被测对象就是 Fable build。

例如现有“every emitted module actually loads”应该保留。

最终边界应该是：

```text
99% semantic tests
    know zero Fable

tiny compiler/build suite
    explicitly knows Fable
```

而不是假装整个 repository 连 build verification 都不知道编译器存在。

---

# 15. 给 code review 一个固定判定树

以后 PR 新增测试时 reviewer 只问这几步：

```text
这个测试在证明一个独立 semantic law 吗？
              │
      ┌───────┴───────┐
      no              yes
      │                │
测 owner behavior    law 的 owner 是谁？
                       │
                 已有 JS surface？
                  │          │
                 yes         no
                  │          │
               使用它      设计正式 surface
                              │
                       是 JS-native 吗？
                         │        │
                        yes       no
                         │        │
                        done    修 representation
```

永远没有：

```text
“测试需要，所以 export internal”
```

这个分支。

---

# 16. 一组非常具体的 forbidden patterns

终态 architecture gate 可以扫描 semantic tests 并拒绝：

```js
value.tag
value.fields
value.cases()

Object.keys(fsharpModule)
Object.entries(fsharpModule)

startsWith('SomeType__')
endsWith('_someMethod')

import '.../fable_modules/...'

import '../../../dist/<internal-path>.js'
```

以及：

```text
member
bind
fableInstanceMethod
unionCase
```

甚至可以针对名字拒绝新增：

```text
ForTests
TestOnly
UnsafeForTest
DebugState
InternalFacade
TestFacade
```

不是说字符串永远非法，而是任何出现都要求 architecture review。

---

# 17. 不要做的五种“捷径”

### ① 自动把 `domain.mjs` 翻译成 F#

这是失败。

只是：

```text
JS test facade
→ F# test facade
```

问题没变。

---

### ② 给每个 F# module 都生成 JS wrapper

也是失败。

你会得到：

```text
1 implementation module
=
1 JS API
```

这仍然把 decomposition 变成 contract。

---

### ③ 为了测试暴露完整 state

例如：

```js
runtime.snapshotForTests()
```

返回：

```text
all private maps
all internal phases
all cursors
```

也是 white-box test，只是序列化了一层。

---

### ④ 为了 JS 把 F# domain 全 primitive 化

不要。

内部继续：

```text
DU
typed IDs
Map
Option
Result
records
```

强类型越丰富越好。

只在 boundary translate。

---

### ⑤ 建一个超级 `PublicApi.fs`

会逐渐变成 god module。

仓库自己对 cosmetic facade 的警告也适用于这里：facade 不能替 subsystem 制造虚假的 coherent ownership。

surface 应跟着 semantic owner 分布。

---

# 18. 我建议的实际迁移顺序

按这个顺序做，不要按目录字母序：

### P0 — Architecture charter

写六条宪法 + JS representation contract。

**完成条件：**以后什么算合法 surface 已无歧义。

### P1 — Inventory

列出所有 deep imports / Fable knowledge / interop usage。

**完成条件：**债务有有限集合。

### P2 — Ratchet gate

现存债务 baseline，新债务禁止产生。

**完成条件：**数字只会下降。

### P3 — Pure pilot ✅

迁一个 pure semantic component。**完成条件：**证明 JSON-shaped contract 可行。**达成：**`ForkChildPayloadSurface`（注册 surface #1，JSON-shaped 输入输出，assertJsData 证明）。

### P4 — Stateful pilot ✅

迁 `SessionQuiescenceGate` 一类 abstraction。**完成条件：**证明 opaque capability + behavior surface 可行。**达成：**`QuiescenceSurface`（gate/permit opaque handle，8 个 HOST-004 law）。

### P5 — Representation gate ✅

建立统一 JS-native validator。**完成条件：**Fable runtime value 无法意外穿过新 surface。**达成：**`js-contract.mjs`（assertJsData/assertOpaque）+ charter「注册 surface 必有契约测试」门禁。

### P6 — Pure/algebra wave ✅（首批）

大量迁 projection/decision/codec/policy tests。**完成条件：**`domain.mjs` 使用量明显下降。**达成：**6 个注册 surface（ForkChildPayload/SyntheticToml/BloggerToml/Quiescence/DelegatedToolEstimate），13 个测试文件迁移，债务 3185→3171、文件 316→312。

### P7 — Resource/runtime wave ✅（首波）

迁 stateful runtime。

**完成条件：**普通测试不再扫描 instance mangling。
**达成：**`RolesSurface`（第 7 个注册 surface，Role/ToolPermission 以 string 跨界，default-deny）；label 唯一表示上移 `Roles.fs`，ManagedAgentCatalog 委托；5 个测试文件（agent-permission-gate/inquiry-permissions/prompt-semantic-depth/js-surface/manager-finality-disposition）迁移；`Roles_isAllowed` 与 `roles.permissions` 用法清零，债务 3171→3169。

### P8 — Effect/integration wave ✅（增量）

迁 Host/effect tests。

**完成条件：**contractual effect 成为 observable，而不是 private choreography。
**达成：**CLN-08 执行 census 裁决——删除 `FactCodec.migrateManagerJobByname`（零测试零真实数据，decode 链私有步骤退役）；剩余 101 处 `roles.of('X')` 传 Fable API 属 P10 API 层翻译范围。

### P9 — Delete legacy adapters ✅（增量）

逐 family 删除 `domain/*` adapters。

**完成条件：**semantic tests 不再 import `domain.mjs`。
**达成：**删除六个零引用零依赖死 adapter（forkChildPayload/processEstimate/packageResources/orchestratorProgram/setCount/setContains），-106 行；328 文件仍 import domain.mjs（后续 wave）。

### P10 — Quarantine Fable

只剩 compiler/build verification 可以理解 Fable。

**完成条件：**Fable upgrade 不影响 semantic tests。

### P11 — Remove baseline

违规数为零，删 baseline。

**完成条件：**architecture gate 从 ratchet 变成 absolute prohibition。

---

# 19. 最终 Definition of Done

这次 migration 只有同时满足下面这些才算结束：

```text
[ ] 所有 semantic automated tests 是 JS

[ ] semantic tests 中没有 Fable mangled-name knowledge

[ ] semantic tests 中没有 .tag/.fields/.cases()

[ ] semantic tests 中没有 FSharpList/FSharpMap/FSharpResult 等 runtime knowledge

[ ] semantic tests 不 import fable_modules

[ ] semantic tests 不 deep-import internal dist modules

[ ] domain.mjs / domain/interop.mjs 不再作为 semantic test gateway

[ ] 每个被独立测试的 semantic law 都能说出明确 owner

[ ] 每个 JS surface 都能用业务/系统语义描述，而不是用 F# module 名解释

[ ] semantic data 是 JS-native / JSON-shaped

[ ] effectful resource 只以 opaque capability 暴露

[ ] 内部 rename/inline/restructure 不要求修改 JS tests

[ ] 同样的 internal structure 下破坏真实 promise 会让 JS tests 失败

[ ] Fable-specific knowledge 只存在于 compiler/build verification quarantine

[ ] legacy boundary baseline 已删除
```

最终的依赖关系就非常简单：

```text
                     ┌──────────────────────┐
JS semantic tests ──▶│ Stable JS semantics │
                     └──────────┬───────────┘
                                │
                         representation
                           translation
                                │
                     ┌──────────▼───────────┐
                     │   F# semantic owner  │
                     └──────────┬───────────┘
                                │
                     private decomposition
                                │
                  helpers / DU / Map / runtime
```

而：

```text
compiler/build tests
        │
        └──────────▶ Fable emitted artifacts
```

是另一条明确隔离的线。

**我会把整个 migration 的成功指标压缩成一句：**

> 改造前，JS 测试在问“F# 是怎么实现的？”；改造后，JS 测试只能问“这个 semantic component 承诺什么，以及它有没有做到？”

到那一步，mangled name 不再是“被解决的问题”——它已经变成**测试世界里不存在的概念**。

现在已经明显不一样了。**我认为“大规模重排目录”这件事已经基本完成，可以停止继续折腾顶层树了。** 这一版已经从“新旧两棵树并存”进入了“ownership tree 基本成立，只剩少数错误根和依赖边需要校正”的阶段。

最关键的变化是，`Domain / Application / Session / Infrastructure` 这些历史技术层已经不再出现在生产编译路径里；现在真正存在的是 `Change / Context / Enforcer / Execution / Foundation / Interaction / Mission / Participant / Persistence / Repository / Strength / OpenCode ...`。目录树已经能直接读出业务所有权。  `.fsproj` 也已经实际按这棵新树组织，而不是目录只改了名字、编译关系仍沿用旧层。比如 `Kernel/Fact` 已经变成 `Composition/Durable/Fact`，`CausalWait` 进入 `Execution/Session/Wait`，SyncDelegate 进入 `Execution/Delegation`，PromptAuthority 进入 `Interaction/Authority`。 

而且 capability-specific adapter 的“下旋”已经做得相当漂亮了。现在 Fork 自己拥有 `Fork/OpenCode/{Tool,JoinTool,JoinGuard,JoinResultRenderer}`，Fission、Finality、Review、Todo、Strength、Casebook、Js 也开始把自己的 OpenCode 接口收回自己的子树。  这就是我们之前说的：

> 物理世界是依赖对象，不自动获得业务代码的 ownership。

现在最值得做的不是“第三次整体排布”，而是下面 **5 个局部旋转**。

1. **最大的剩余错误根是 `Composition/Durable/Fact.fs`。** 文件虽然从 `Kernel` 搬出来了，但实际上还没有完成我们说的那次旋转：`PromptFactCases`、`ReviewFactCases` 等业务 fact family 仍然定义在这个中央文件里。   也就是说现在是：

   ```text
   Composition/Durable/Fact
      ├── Prompt facts
      ├── Review facts
      ├── Execution facts
      ├── ...
   ```

   我仍然建议最终旋成：

   ```text
   Interaction/Authority/Facts.fs
   Participant/Provider/Attempt/Fallback/Facts.fs
   Mission/Review/Facts.fs
   Execution/Delegation/Facts.fs
   Context/Companion/Facts.fs
   Execution/Fission/Facts.fs
   Change/Facts.fs
            \   |   /
       Composition/Durable/Fact.fs
   ```

   `Composition/Durable/Fact.fs` 最终只应该做 outer union / routing vocabulary。**Composition 可以认识所有人，但不应该替所有人定义自己的语言。** 这是当前最有价值的一刀。

2. **`Foundation` 里还有两三个“假基础”。** 最明显的是 `Foundation/Flow.fs`。它里面有 `InvalidFork`、`ParentCancelled`、`CompanionError`、`CompanionContext`——这些显然不是宇宙级 primitive，而是 Execution/Companion 语义。 我会把它拆掉，而不是保留一个叫 `Flow` 的杂交根。比如 `InvalidFork/ParentCancelled` 靠近 `Execution`，`CompanionError/CompanionContext` 靠近 `Context/Companion`。相比之下 `Identity/Roles/Temporal/Parallel/TaskResult` 留 Foundation 很合理。

   `McpLaunch` 我倒没那么介意。它确实只是一个非常小的共享 launch vocabulary：`Disabled | Fixture | Uvx`。 它可以以后再判断是否值得变成 `Host/Mcp/Launch`，不是当前重点。

3. **`Composition/Durable/GuidelineProjection.fs` 应该再旋出去。** 它不是 composition；它定义的是非常具体的 `PairProgrammingGuideline` durable state、ordinal、call/result transcript gap 及其 fold invariant。 我更倾向：

   ```text
   Host/
     PairProgramming/
       GuidelineProjection.fs
   ```

   或者如果你认为 cognitive environment 才是 owner，就在那里建对应节点。

   相反 `Composition/Durable/HostFactFold.fs` 留下是合理的。它本来就是一个认识很多 bounded contexts 的汇合/router，当前 imports 几乎覆盖整棵树，这在 Composition 节点反而符合职责。

4. **现在最需要修的其实已经不是目录，而是 architecture gate。** `HOST_BOUNDARY_OPEN_BASENAMES` 仍然活着，而且已经膨胀到非常危险的程度：除了旧名字以外，现在连 `Host.fs`、`Runtime.fs`、`Workflow.fs`、`Types.fs`、`Recovery.fs`、`Repair.fs` 等通用 basename 都被放进去了；而 `isHostBoundaryOpenPath` 判断时真的只取 basename。 这意味着理论上：

   ```text
   Whatever/Runtime.fs
   ```

   仅仅因为叫 `Runtime.fs`，就可能获得本来不该有的物理边界权限。

   这和现在已经形成的 ownership tree 是冲突的。第二轮以后应该反过来按**路径语义**授权，例如：

   ```text
   **/OpenCode/**
   **/Host/**
   OpenCode/**
   Process/**
   ```

   再对极少数 bridge 精确列路径，而不是列 basename。

   更明显的是，`dsl-ownership-ratchet-baseline.json` 还保存着上一轮的 `Feedback/Enforcer`、`OpenCode/Contract`、旧 `Composition` 等路径。 但你自己的 structured-workflow 文档已经把这个 ratchet 明确标记为 **cutover 后 DELETE**，只留下 `--threshold=0` 的 positive gate。 而现在 `check.mjs` 已经确实把主门设成了 `--threshold=0`。 所以这里应该收尾：**删掉 migration ratchet 和旧 baseline，而不是继续维护它。**

5. **最后做一次“假依赖边清理”，再决定是否继续旋转。** 例如 `Foundation/SyntheticToml.fs` 一开头竟然 `open` 了 Composition、Context、Enforcer、Execution、Host、Mission、Participant 等大量上层 subtree。 可它自己的注释却明确说它“knows nothing about Blogger, forks, or any local schema”，只拥有 canonical TOML string/layout rules。 从实际实现看，前面的 `normalizeNewlines / renderString / comment / field / tableEntry` 也确实是纯格式算法。 

   所以这里很可能不是根真的错，而是机械迁移后留下了一堆 unused `open`。**先清 unused imports，再画依赖图。** 否则我们会根据幽灵依赖做错误旋转。清完以后，如果 SyntheticToml 真只依赖 `System`，它放 Foundation 虽然我个人更喜欢 `Participant/Provider/Wire/Toml.fs`，但已经属于命名品味问题，不再是架构问题。

我现在对整体结构的判断可以浓缩成：

```text
上一版：
旧 layer tree + 新 ownership tree
→ 需要继续砍旧根

这一版：
ownership tree 已经成立
→ 不要再大改树
→ 拆中央 Fact
→ 清 Foundation 假基础
→ 把具体 Projection 从 Composition 移走
→ 删除迁移 whitelist / baseline
→ 清 unused imports
→ 再根据真实 cross-tree edges 做少量 rotation
```

还有一点我会特别强调：**现在不要因为 `OpenCode`、`Execution`、`Mission` 文件多，就试图“平衡文件数量”。** 平衡树思想在这里应该平衡的是“语义路径和跨树依赖代价”，不是节点个数。当前不同子树大小明显不一样，但这已经开始像自然生长出来的依赖树，而不是人为铺平的 taxonomy。 

所以如果问我“现在还乱不乱”，我的判断是：**生产目录本身已经不乱了。现在的主要架构债已经转移成了边界门禁、中央 Fact ownership 和少数假 Foundation 节点。** 这是一个好信号——说明目录重构基本可以收工，接下来应该治理依赖边，而不是继续搬树。

以上判断基于你刚上传的最新完整仓库快照。

这个方向值得做，而且我建议把它做成**比“覆盖率”更基础的一条仓库不变量**。

你现在已经有：

```text
WHAT ──→ PROOF ──→ test file
```

当前 `meta-verifier` 会枚举 WHAT proposition，检查 PROOF 有对应行，并检查 PROOF 引用的测试文件存在。 

但缺的是反方向：

```text
test case ──→ WHAT
```

这其实正好落实你已经写下的 `REQUIREMENT-SYSTEM-004`：proof ownership 是 **assertion 级，不是文件级**，每条 executable assertion 必须有唯一 owner。

我建议最终把关系做成一个数学上很简单的闭环：

```text
                 PROOF.md
                /        \
               /          \
              v            v
          WHAT-xxx  <───  test()

必须同时满足：

∀ test: exactly one primary WHAT
∀ WHAT: at least one active test
test → WHAT 必须存在
WHAT → test 必须存在
PROOF 中记录的边必须真实存在

skip / todo ≠ proof
```

换句话说，**active tests → WHAT 是一个 total + surjective mapping**。

而且我赞成你想要的压力：找不到 WHAT 的测试不允许用 `N/A` 糊弄过去。

---

## 我推荐的最终写法

不要靠目录推断，也不要靠文件顶部一行注释推断。直接让**每个 test 名自己携带 WHAT ID**：

```js
test(
  'WHAT[PROVIDER-LANGUAGE-005] system transform localizes only Wanxiangshu-owned segment',
  async () => {
    // ...
  },
)
```

动态 case 也一样：

```js
for (const bad of badSignals) {
  test(`WHAT[PROCESS-EXECUTION-003] rejects unsupported signal ${bad}`, () => {
    // ...
  })
}
```

机器合同只认：

```text
WHAT[<CURRENT-WHAT-ID>]
```

不认历史 `PROMPT_017`、`REVIEW_007` 之类 ID，不认文件路径隐式 ownership，也不认注释里的“看起来差不多”。

这有一个非常好的副作用：**CI 报错和本地 test output 本身就回答了“这个测试为什么存在”。**

你现在其实已经有很多人工版雏形。例如 `provider-system-transform.test.mjs` 文件头已经花了一段话解释它属于 `provider-language`，对应 `PROVIDER-LANGUAGE-001/005`，而不是另外几个相邻 owner。 以后这种判断直接进入机器关系，不需要靠考古。

---

# 保姆级 Roadmap

1. **先写新 WHAT，不要先写 gate。** 在 `requirements/requirement-system/WHAT.md` 新增一条，我建议叫 `REQUIREMENT-SYSTEM-018：可执行证明双向可追溯`，不要修改现有 004 的含义。004 继续负责“每个 executable assertion 恰一个 package owner”；018 负责更严格的“每个 test case 恰一个 current WHAT proposition”。你现在已经声明 WHAT 是唯一 normative contract，WHY/HOW/PROOF 都不是 normative，所以这条新规则必须先落 WHAT。

   我建议规范陈述直接写成接近这样：

   > `requirements/**/tests/**/*.test.mjs` 中的每个可执行 test case 必须显式声明恰一个当前 WHAT proposition ID；该 ID 必须存在于唯一 owner package 的 WHAT.md。每个当前 WHAT proposition 必须至少被一个非 skip、非 todo 的 test case 证明。test 与 WHAT 之间不存在无归属、悬空、多 primary 或仅依赖路径推断的关系。

   边界里再明确：helper、fixture、`beforeEach`、普通 `assert` 不是独立 proof case；粒度以 `test()/t.test()` 为准。一个 test 不允许 primary 到两个 WHAT。

2. **把“一个测试只能回答一个 WHAT”定死。** 这是我建议你比现在再严格一步的地方。当前 PROOF 里已经有一个 test anchor 同时服务多个 proposition 的情况，例如 interaction-authority 的表里存在 `001/002` 合并关系。 新规则下不要写：

   ```js
   test('WHAT[A-001,A-002] ...')
   ```

   而应该拆成：

   ```js
   test('WHAT[A-001] receipt cannot become authority root', ...)
   test('WHAT[A-002] only physical message may establish root', ...)
   ```

   两个测试可以共享 setup、helper，甚至共享一次昂贵的物理运行结果，但**failure meaning 必须只有一个**。如果两条命题根本无法分别测试，优先回头问 WHAT 是否其实应该是一条命题。这正是你要的文档反哺。

3. **定义测试宇宙，避免 denominator 偷漏。** 第一版严格限定：

   ```text
   requirements/**/tests/**/*.test.mjs
   ```

   里面所有真正的 `test()`、`test.skip()`、`test.todo()`、nested `t.test()` 都必须被 scanner 看到。`*.fixture.mjs`、support helper、`before/after` 不算 test。`skip/todo` 可以要求带 WHAT 标签，但**不能算作 WHAT 已有 proof**。

   这一条非常重要，否则以后很容易出现一个漂亮的 gate，却漏掉某类 integration/eval/e2e tests。

4. **不要继续把逻辑塞进现在的 meta-verifier；抽一个 trace graph。** 当前 `meta-verifier` 已经同时负责包树、依赖骨架、WHAT ID、PROOF 文件存在等结构检查。 再往里面直接加 JS test AST 解析，会很快变成 god verifier。

   我建议增加：

   ```text
   scripts/lib/requirement-trace.mjs
   scripts/checks/requirement-trace.mjs
   requirements/requirement-system/tests/requirement-trace.test.mjs
   ```

   `requirement-trace.mjs` 只构建一个纯数据图：

   ```text
   WhatNode {
     id
     package
     file
     heading
   }

   TestNode {
     file
     line
     title
     state: active | skip | todo
     whatId
   }

   Edge {
     test
     what
   }
   ```

   `meta-verifier` 后面可以复用这个 graph，而不是各自重新 regex。

5. **test source 用 AST/token parser 扫，不要用粗 regex。** Gate 必须能区分字符串里的 `test(`、注释、`test.beforeEach`、alias、template title、nested test 等情况。这个项目已经很重视 fail-closed gate，我不建议为了省一个轻量 parser 而造一个未来必漏的正则扫描器。

   Scanner 至少要能报这些错误：

   ```text
   TRACE_ORPHAN_TEST
   foo.test.mjs:42
   "rejects invalid carrier"
   has no WHAT[...] owner

   TRACE_UNKNOWN_WHAT
   foo.test.mjs:81
   references WHAT[FOO-999], but that proposition does not exist

   TRACE_MULTI_PRIMARY
   test declares more than one primary WHAT

   TRACE_UNPROVED_WHAT
   FOO-007 has zero active executable tests

   TRACE_PROOF_MISSING
   FOO-003 points to this test, but PROOF.md does not expose the relation

   TRACE_DANGLING_PROOF
   PROOF.md names a test anchor that no longer exists
   ```

6. **在真正迁移前先做 report-only inventory。** 命令建议设计成：

   ```bash
   node scripts/checks/requirement-trace.mjs --report
   node scripts/checks/requirement-trace.mjs --package=provider-language
   node scripts/checks/requirement-trace.mjs --explain=path/to/test.mjs:42
   ```

   第一次不要红 CI，只生成类似：

   ```text
   package                   WHAT   active tests   orphan tests   unproved WHAT
   provider-language            5             12              3               0
   interaction-authority       16             31              8               1
   ...
   ```

   `--explain` 最终应该成为非常好用的维护工具：

   ```text
   test
     requirements/provider-language/tests/provider-system-transform.test.mjs:27

   proves
     PROVIDER-LANGUAGE-005

   normative source
     requirements/provider-language/WHAT.md
     ## PROVIDER-LANGUAGE-005 ...

   proof index
     requirements/provider-language/PROOF.md
   ```

   这样“为什么有这个测试”不再需要 grep。

7. **迁移时绝对不要让脚本自动生成 WHAT。** 脚本可以根据当前位置、现有 PROOF、历史 ID、文件头注释给出 candidate，但只能建议：

   ```text
   likely WHAT:
     PROVIDER-LANGUAGE-005  0.92
     PROVIDER-LANGUAGE-001  0.71
   ```

   人必须做最终裁决。每碰到一个 orphan test，只允许四种处理：映射到现有 WHAT；发现文档遗漏，先补一个真正的 WHAT 再映射；发现测试钉的是 HOW 细节，重写成能够证明现有 WHAT 的行为测试；发现它没有独立 failure meaning，删除或并入别的测试。

   **第四种和第二种就是整个机制最值钱的地方。**

8. **按 package 小批量迁，不要全仓一次机械加标签。** 先 dogfood `requirement-system` 和 `verification-system`，因为它们负责规则本身；然后迁 owner 很清晰的小包；最后再处理 structured-workflow、host-boundary、capability-enforcement 这些交叉很多的包。

   每一个 package 都重复同一个闭环：

   ```text
   inventory tests
        ↓
   给每个 test 找 WHAT
        ↓
   找不到 → 文档 / 测试裁决
        ↓
   WHAT[...] 写入 test title
        ↓
   PROOF exact anchor 对齐
        ↓
   package trace = 100%
        ↓
   package 进入 hard mode
   ```

   不建议在这个阶段顺手大规模重构 production。一次 commit 尽量只做一个 package 的 trace closure，这样 review 能真正判断映射有没有作弊。

9. **迁移期可以有 ratchet，但必须从出生起就写 DELETE 条件。** 你刚刚才清理了一批历史 migration baseline，所以这次不要再造永久白名单。可以临时生成：

   ```text
   scripts/checks/requirement-trace-migration.json
   ```

   里面列当前仍未认领的 test anchor。规则只能：

   ```text
   新 orphan = RED
   已认领项不得重新进入 baseline
   baseline 数量只降不升
   ```

   然后逐包 hard：

   ```text
   strict:
     requirement-system
     verification-system
     provider-language
     ...
   ```

   当最后一个 package 进入 strict，**同一个提交删除 migration file 和 compatibility branch**。不要留下 `--allow-unmapped`。

10. **Hard cutover 时再把 PROOF 从“文件存在”升级到“精确边闭合”。** 你当前 parser 对 PROOF 的检查实际上只提取落点文件 token，然后确认文件存在。 这还不够，因为：

```text
PROOF says foo.test.mjs
```

并不能证明里面真的还有那个 test。

目标应升级成：

```text
WHAT[FOO-003]
    ↕
PROOF.md exact test anchor
    ↕
foo.test.mjs exact test case
```

你的 PROOF 文档已经大量写了“文件 + test/describe 锚点”，所以这是自然强化，不是换模型。

11. **最后我甚至建议让 PROOF 的 executable 部分半生成。** WHAT 必须坚持手写，因为它是 normative authority；test 的 WHAT tag 也必须人工裁决。PROOF 本身是 non-normative evidence index，没有必要让人重复抄几百个 anchor。

可以变成：

```markdown
## Executable proof index

<!-- BEGIN GENERATED TRACE -->

| WHAT | Active test cases |
|---|---|
| PROVIDER-LANGUAGE-001 | ... |
| PROVIDER-LANGUAGE-005 | ... |

<!-- END GENERATED TRACE -->

## Manual / physical evidence

...人工维护...
```

这样真正的 source of truth 是：

```text
WHAT.md          人写：系统必须是什么
test() WHAT tag  人裁决：这个 test 为什么存在
PROOF.md         生成：当前 evidence graph 长什么样
```

不会出现三个地方手工复制同一事实然后互相漂移。

12. **Full hard mode 后，把规则接进最前面的 cheap checks。** 我会把 `requirement-trace` 放在 build/test 之前：

```text
spec
requirement-trace
architecture
build
tests
...
```

新人写：

```js
test('some regression', ...)
```

应该在几十毫秒到几秒的静态门阶段直接收到：

```text
This test has no normative reason to exist.
Choose exactly one:
  1. reference an existing WHAT
  2. add a missing WHAT first
  3. rewrite the test so it proves an existing WHAT
  4. delete the test
```

这比等 code review 问“这个测试到底在测什么”有效得多。

---

## 我会再加一个防“文档作弊”的小机制

否则开发者可能学会这样过 gate：

```markdown
## FOO-999：其它行为

**规范陈述**：系统其它行为必须正确。
```

然后一百个测试全挂 `FOO-999`。

机器无法真正判断散文质量，但至少可以把作弊成本提高。既然你现在 WHAT 已经采用“规范陈述 + 含义/动机 + 边界 + 证据指针”的结构，例如现有 WHAT 就明确把这些组成看成 proposition 的完整表达。

所以 trace gate 在解析被 test 引用的 WHAT 时，还应该要求这些字段**非空存在**：

```text
标题
规范陈述
含义/动机
边界
```

不要用“至少 5 行”“至少 100 字”这种垃圾 heuristic；只检查结构存在。语义是否真的够具体仍交给 review。

同时提供非阻塞统计：

```text
WHAT fan-in:

FOO-001     3 tests
FOO-002     6 tests
FOO-003    47 tests  ← review hint, NOT automatic RED
```

47 个测试指向一个 WHAT 不一定错，但 reviewer 会马上知道应该检查是不是 catch-all。

---

## 关于低层 unit test，我建议你狠一点

以后如果看到：

```js
test('PtyId roundtrips its value', ...)
```

第一反应不要是“给它随便找一个 process WHAT”。

先问：

> **如果这个实现从 wrapper class 换成别的表示，这个 test 仍然应该成立吗？**

如果答案是否，那它很可能只是在 pin HOW。

此时应该考虑把它改成真正的 contract test，或者删除，而不是把 implementation detail 升格成 WHAT。

这会让你的测试数量可能有所下降，但测试的**信息密度会明显提高**：

```text
以前：
代码存在 → 顺手写 test

以后：
WHAT 存在
  ↓
需要 executable evidence
  ↓
test 存在
```

反方向：

```text
发现值得长期保留的 regression test
  ↓
找不到 WHAT
  ↓
说明：
  文档漏了 invariant
  或
  这个 regression 并不是产品合同
```

这正是你要建立的反馈回路。

---

## Cutover 的最终验收标准

到最后，仓库应该能机械证明：

```text
orphan active test                    = 0
test with unknown WHAT                = 0
test with multiple primary WHAT       = 0
WHAT with zero active test            = 0
PROOF anchor missing                  = 0
PROOF dangling anchor                 = 0
temporary trace migration exceptions  = 0
```

并且任意一个 test，你都能得到：

```text
这个测试为什么存在？
        ↓
WHAT[XXX-NNN]
        ↓
requirements/<owner>/WHAT.md
        ↓
这条当前系统真理是什么？
```

我认为这会比传统的“requirements coverage = 100%”强很多。传统 coverage 只能证明**文档没有漏测**；你这个双向闭环还能证明**测试没有偷偷创造第二套需求体系**。而你现有 requirement-system 已经把“WHAT 是唯一合同”和“executable assertion 有唯一 owner”铺好了，实际上只差把这条反向边机器化。 

