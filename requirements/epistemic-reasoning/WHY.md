# epistemic-reasoning — WHY

> 本页回答：这个包为什么必须独立存在？它防止哪一类世界破坏？哪些被拒方案曾经存在？
> 规范见 `WHAT.md`；实现见 `HOW.md`。本页不新增 normative 命题。

## 1. 不可替代的存在理由

### 1.1 认识状态是 sufficient state，不是 transcript / 问题树

完整执行历史 `h=(q0,a1,y1,…,at,yt)` 中大量表面差异对未来认知决策无影响。把历史直接当状态，
会让「多说一遍」伪装成「多知道一点」。真正维护的是历史关于未来认知决策的**充分统计量**
`S=[h]~`；日志可以 append-only，推理状态不应该 append-only。搜索树、posterior、frontier、
MCTS stats 都是 representation，不是 ontology（archive/changes/completed/Sphinx.md 第 1 篇 §2/§3）。

### 1.2 Proposal 与 Evidence 必须物理分槽（No Free Information）

LLM 能生成解释、候选、价值估计、综合文案，但这些能力本质是计算与提案。若 Candidates /
Synthesis 能直接增加 evidence mass，递归十轮就能把同一批信息「说成」更高置信度——系统会
奖励自我说服。Finding/Evidence 分槽 + explicit Source/DependencyKey 把 No Free Information
变成**类型与状态规则**，而不是 prompt 自觉。

### 1.3 QuestionForm 不能 argmax，也不能 bind-once

「为什么程序卡住」可能同时包含 Explanation 与 Plan；「白银会涨吗」也可能同时要求 Judgment
与 Credence。硬标签会让 0.51/0.49 与 0.99/0.01 变成同一个控制状态；开局绑定一次也会把后续
「原来用户真正想修复」的语义证据丢掉。保留 `Q_t(Form)` 分布，并允许 Investigation 返回
control-only semanticAssessment 后，方法激活与答案契约能随后续语义观测平滑变化；这种变化
仍不增加世界 Evidence。

### 1.4 gateway value 必须进入动作价值

一步信息增益会系统性低估「先问这个，才能知道接下来该问什么」的门户问题。`GatewayGain` 是
Bellman 未来搜索价值的低阶近似：它只影响 policy，不冒充 evidence。这样既避免一步 EIG 短视，
也不需要 V1 就 exact solve POMDP。

### 1.5 posterior 要资格检查

LLM 说「我觉得 0.8」不是 likelihood model。正式 posterior 只接受显式 hypotheses + 覆盖完整
`[0,1]` 的 likelihood + `numericQualified` + dependency group；否则宁可给 qualitative/uncertain
answer，也不生成伪精确数值。同源证据若重复相乘会制造虚假独立性：V1 每 DependencyKey 只取
一个规范代表进入 product，是保守但明显正确的边界；更复杂相关结构必须升级成显式 factor
model，不能暗中猜 independence。

### 1.6 A* / Bayes / MCTS 必须是真退化，而不是名字

「有 priority queue」不等于 A*；「有 visits」不等于 MCTS；「归一几个数字」不等于合格
Bayesian inference。经典算法的价值在于它们提供强可验证子模型：固定条件后，Sphinx 必须表现得
像真正 graph A*、固定 likelihood Bayes、selection/rollout/backup MCTS。通过退化测试能证明母
模型没有被错误抽象设计窄。

### 1.7 等价必须显式且 dependency-aware

文本相同不代表未来决策等价。「同一个问题分别问两个独立来源」价值恰恰来自独立性。只按
semantic key 判重会把 source triangulation 自己删掉；反过来，让 LLM 自报 `equivalenceKey`
又会把 ontology 权交回语义 oracle。因此默认 identity 包含 dependency；只有 Kernel 自己的
canonicalization/rewrite 写入内部 EquivalenceKey，或 semantic+dependency 同时相同，才进入
同一类。类内再做逐维 Pareto dominance，不拿单一净分数吞掉信息/成本 trade-off。

### 1.8 continuation 只属于 Kernel

若 LLM 可以自行说「我已经够了」或跳到另一方法，Closure、预算、依赖去重、Stop 都退化成
提示词建议。固定 PendingRequest ↔ Observation 契约后，LLM 每轮只回答 Kernel 当前请求；错型
不前进状态。这样 co-yield 才是有控制器的 coroutine，而不是两个平权生成器聊天。

### 1.9 handle 有状态（当前机制）

无 handle 的单次工具只能把 continuation 偷渡回 transcript。进程内 handle 把权威
EpistemicState 留在 Sphinx；调用方只持钥匙。V1 不做 durable journal，避免把认识内核与 Host
Session / EventStore 生命周期绑死——「认识内核是否 durable」是独立实现选择。

### 1.10 为什么统一 F# → Fable JS（当前实现）

仓库生产语义本来由 F# 类型系统与统一 Fable build 守门。平行 `src/sphinx/*.js` 绕开这条边防，
导致 Observation、Evidence、Candidate、posterior 都靠运行时对象猜形；更严重的是 build 直接
copy，使「编译通过」无法证明 Sphinx。现在 Sphinx 位于 `src/Wanxiangshu/Sphinx/*.fs`，ADT/
record 把非法组合压出内核；raw JS 只停在 Codec，MCP SDK 只停在 McpServer。Fable 输出仍是
Node MCP，没有牺牲 Host 集成，却恢复了仓库唯一实现语言与编译门禁。

### 1.11 同工程不等于同领域

`Wanxiangshu.Sphinx` 与 Host 同在一个 fsproj 是构建事实，不是所有权合并。Sphinx core 不依赖
Agent/Host/Journal；Host 只知道入口与权限。这样既消除第二编译系统，又保留认识机可独立推导、
测试与替换的语义边界。

## 2. 失败模式（世界 RED 长什么样）

| 失败 | 破坏 | 对应命题 |
|---|---|---|
| Candidates/Synthesis 增加 evidence mass | 递归自我说服，系统奖励重复思考 | EPI-005 |
| 同源证据重复相乘 | 虚假独立性 inflate posterior | EPI-006/009 |
| LLM 自报 posterior/confidence 进对象层 | 伪精确数值冒充合格推断 | EPI-009 |
| LLM 自选下一步或自封 answered | closure/预算/去重退化成提示词建议 | EPI-002/004 |
| 用 transcript 复原权威状态 | 历史噪声伪装成认识状态 | EPI-001 |
| 按 semantic key 误判重 | 删掉 source triangulation 的独立来源价值 | EPI-011 |
| 用 argmax/bind-once 硬标签 | 0.51/0.49 与 0.99/0.01 变同一控制状态 | EPI-007 |
| 算法只挂名不退化 | 无法验证母模型被错误抽象设计窄 | EPI-010 |

历史修正第一手考古：`archive/changes/completed/Sphinx.md`「Corrective outcome — 2026-08-12」——旧
完成声明曾引入 `evidenceMass` 伪置信度、primary argmax、bind-once、wire equivalenceKey、
LLM 自报 confidence、开局一次性生成候选，全部在 corrective round 逐条修正为本包命题。

## 3. 明确拒绝的方向（考古，不构成 WHAT）

- **`evidenceMass` 伪置信度**：删除；SemanticAssessment / Candidates / Synthesis 不增加世界
  证据（EPI-005）。
- **transcript/问题树当状态本体**：状态是 sufficient statistic（EPI-001）。
- **primary argmax 硬分类 / bind-once 标签**：QuestionForm 保留分布、可更新（EPI-007）。
- **LLM 自报 `equivalenceKey` 判重**：wire 无判重权（EPI-011）。
- **LLM 自报 posterior / confidence 写入对象层**：只接受 SPHINX-008 资格门后的数值
  （EPI-009）。
- **开局一次性生成候选**：每次 Investigation 后 `NeedsGeneration=true`，方法库递归生长
  （EPI-007/008 的 HOW 侧）。
- **A*/Bayes/MCTS 挂名**：必须是严格退化，且 solver 统计不冒充认识证据（EPI-010）。
- **Host 内嵌 Closure/Stop**：MCP/Host 层只转发，不复制认识判断（EPI-002，HOW 侧）。

## 4. 边界：什么**不归**本包

- repository evidence acquisition → `repository-investigation`。
- external web/browser acquisition → `external-investigation`。
- Sphinx MCP/handle/F# 文件布局、当前 start/resume wire protocol → HOW；wire 身份 →
  `host-boundary`。
- Inquiry office authority（谁能发起 inquiry / `sphinx_*` 权限）→ `office-capability`/
  `capability-enforcement`。
- durable host/session lifecycle；epistemic kernel 是否 durable → 独立实现选择
  （`managed-session-lifecycle` 相关）。
- 组件名 Sphinx、A*/Bayes/MCTS 算法、MCP 工具名 → HOW，不进 WHAT。

边界卡片：`requirements-design/18-optimization-epistemics.md`。
