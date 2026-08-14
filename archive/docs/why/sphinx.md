# Sphinx — 理由

可观察语义见 `what/sphinx.md`。本页只解释不可替代的设计取舍。

## 为什么是认识状态，不是 transcript / 问题树

历史包含大量对未来决策无影响的表面差异；把它直接当 state 会让“多说一遍”伪装成“多知道一点”。Sphinx 只保留 RootContract、认识基底、依赖、可行动作与预算等充分量。搜索树、posterior、frontier、MCTS stats 都是 representation，不是 ontology（SPHINX-001、004）。

## 为什么 Proposal 与 Evidence 必须物理分槽

LLM 能生成解释、候选、价值估计、综合文案，但这些能力本质是计算与提案。若 Candidates / Synthesis 能直接增加 evidence mass，递归十轮就能把同一批信息“说成”更高置信度；系统会奖励自我说服。Finding / Evidence 分槽 + explicit Source / DependencyKey 把 No Free Information 变成类型与状态规则，而不是 prompt 自觉（SPHINX-006）。

## 为什么 QuestionForm 不能 argmax，也不能 bind-once

“为什么程序卡住”可能同时包含 Explanation 与 Plan；“白银会涨吗”也可能同时要求 Judgment 与 Credence。硬标签会让 0.51/0.49 与 0.99/0.01 变成同一个控制状态；开局绑定一次也会把后续“原来用户真正想修复”的语义证据丢掉。保留 `Q_t(Form)` 分布，并允许 Investigation 返回 control-only semanticAssessment 后，方法激活与答案契约能随后续语义观测平滑变化；这种变化仍不增加世界 Evidence（SPHINX-007）。

## 为什么 gateway value 必须进入动作价值

一步信息增益会系统性低估“先问这个，才能知道接下来该问什么”的门户问题。`GatewayGain` 是 Bellman 未来搜索价值的低阶近似：它只影响 policy，不冒充 evidence。这样既避免一步 EIG 短视，也不需要 V1 就 exact solve POMDP（SPHINX-007）。

## 为什么 posterior 要资格检查

LLM 说“我觉得 0.8”不是 likelihood model。正式 posterior 只接受显式 hypotheses + 覆盖完整的 `[0,1]` likelihood + `numericQualified` + dependency group。否则宁可给 qualitative/uncertain answer，也不生成伪精确数值（SPHINX-008）。

同源证据若重复相乘会制造虚假独立性。V1 每 DependencyKey 只取一个规范代表进入 product，是保守但明显正确的边界；更复杂相关结构必须升级成显式 factor model，不能暗中猜 independence。

## 为什么 A* / Bayes / MCTS 必须是真退化，而不是名字

“有 priority queue”不等于 A*；“有 visits”不等于 MCTS；“归一几个数字”不等于合格 Bayesian inference。经典算法的价值在于它们提供强可验证子模型：固定条件后，Sphinx 必须表现得像真正 graph A*、固定 likelihood Bayes、selection/rollout/backup MCTS。通过退化测试能证明母模型没有被错误抽象设计窄（SPHINX-009）。

## 为什么等价必须显式且 dependency-aware

文本相同不代表未来决策等价。“同一个问题分别问两个独立来源”价值恰恰来自独立性。只按 semantic key 判重会把 source triangulation 自己删掉；反过来，让 LLM 自报 `equivalenceKey` 又会把 ontology 权交回语义 oracle。因此默认 identity 包含 dependency；只有 Kernel 自己的 canonicalization/rewrite 写入内部 EquivalenceKey，或 semantic+dependency 同时相同，才进入同一类。类内再做逐维 Pareto dominance，不拿单一净分数吞掉信息/成本 trade-off（SPHINX-010）。

## 为什么 continuation 只属于 Kernel

若 LLM 可以自行说“我已经够了”或跳到另一方法，Closure、预算、依赖去重、Stop 都退化成提示词建议。固定 PendingRequest ↔ Observation 契约后，LLM 每轮只回答 Kernel 当前请求；错型不前进状态。这样 co-yield 才是有控制器的 coroutine，而不是两个平权生成器聊天（SPHINX-001、003）。

## 为什么 handle 有状态

无 handle 的单次工具只能把 continuation 偷渡回 transcript。进程内 handle 把权威 EpistemicState 留在 Sphinx；调用方只持钥匙。V1 不做 durable journal，避免把认识内核与 Host Session / EventStore 生命周期绑死（SPHINX-002）。

## 为什么改成 Wanxiangshu.Sphinx F# → Fable JS

仓库生产语义本来由 F# 类型系统与统一 Fable build 守门。平行 `src/sphinx/*.js` 绕开这条边防，导致 Observation、Evidence、Candidate、posterior 都靠运行时对象猜形；更严重的是 build 直接 copy，使“编译通过”无法证明 Sphinx。

现在 Sphinx 位于 `src/Wanxiangshu/Sphinx/*.fs`，使用 ADT/record 把非法组合压出内核；raw JS 只停在 Codec，MCP SDK 只停在 McpServer。Fable 输出仍是 Node MCP，所以没有牺牲 Host 集成，却恢复了仓库唯一实现语言与编译门禁（SPHINX-005）。

## 为什么同工程不等于同领域

`Wanxiangshu.Sphinx` 与 Host 同在一个 fsproj 是构建事实，不是所有权合并。Sphinx core 不依赖 Agent/Host/Journal；Host 只知道入口与权限。这样既消除第二编译系统，又保留认识机可独立推导、测试与替换的语义边界（SPHINX-005）。
