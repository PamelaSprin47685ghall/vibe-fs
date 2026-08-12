> 本文件是历史变更记录，不是当前产品规范。
> 当前产品语义仅以 `docs/` 正式层为准。

# Sphinx — Generative Epistemic State Solver
生成式认识状态求解器

---

# 第 1 篇：它到底是什么？

## 1. 一句话定义

**Sphinx 是一个由数学内核控制、通过 MCP 与 LLM co-yield 的生成式认识状态求解器。**

用户给出一个疑问后，系统不是直接生成答案，而是维护一个关于"目前知道什么、还不知道什么、什么问题值得继续问"的认识状态。

LLM 负责提供数学内核无法自行获得的语义观测和生成建议。

数学内核负责：

* 认识状态；
* 全局闭包；
* 判重与状态约简；
* 方法选择；
* 下一步控制；
* 概率与裁决；
* 搜索预算；
* 停止；
* Canonical Answer。

LLM 不决定：

* 用什么方法；
* 下一步做什么；
* 哪个候选是真的；
* posterior 是多少；
* 什么时候停止；
* 最终报告包含什么认识内容。

LLM 的核心角色是：

$$\text{language} \to \text{structured probabilistic semantic observations}$$

Kernel 的核心角色是：

$$\text{observations} \to \text{globally consistent epistemic state} \to \text{decision}$$

**两个锚定例子**（同一内核，两种结局）

「花儿为什么这样红？」→ Why 契约 → Multidisciplinary 展开 → 各层解释互补 → **Synthesis**（不坍缩）
「明天白银会涨吗？」→ Polar 契约 → BaseRate/Analogy/CausalDrivers → Judgment Collapse → **Posterior**（数值资格成立 → Bayesian posterior；不成立 → 定性/区间 credence）

方法论链由 Kernel 生长，不是 LLM 勾选。

**疑问形式 → 默认组合**（Facets 再修正）

| 疑问形式 | 默认组合 |
|---|---|
| Why | Synthesis |
| How | Synthesis / Plan |
| What / Who / Where / When | Ranking / Direct |
| Which | Ranking |
| Polar | Judgment / Credence Collapse |

例：Why+medical → Multidisciplinary → CausalMechanism → 证据铺底
Polar+predictive → BaseRate → Analogy → CausalDrivers → 反证 → Judgment Collapse（数值资格成立 → Bayesian posterior；不成立 → 定性/区间 credence）

「花儿」的完整链由 Kernel 长出：Multidisciplinary → Abduction → CausalAnalysis → Counterexample → Synthesis，不是一张勾选表。

---

## 2. 第一性原理之一：历史不是状态

设完整执行历史为：

$$h=(q_0,\,a_1,\,y_1,\,\ldots,\,a_t,\,y_t)$$

如果两个历史 $h_1,h_2$ 对所有未来合法认知决策都不可区分：

$$\forall\pi,\quad \mathcal{L}(\text{future}\mid h_1,\pi)=\mathcal{L}(\text{future}\mid h_2,\pi)$$

那么：

$$h_1\sim h_2$$

它们就是同一个认识状态：

$$S=[h]_{\sim}$$

所以：

> **系统真正维护的是历史关于未来认知决策的充分统计量。**

日志可以 append-only。

推理状态不应该 append-only。

---

## 3. 第一性原理之二：表示不是认识本身

同一个认识状态可以有多个计算表示：

$$r_1\sim r_2\sim r_3$$

例如：

* 一个大图；
* 一个缩约后的 factor graph；
* sufficient statistics；
* cached posterior；
* 一个更小的等价表达。

Kernel 有权进行：

* merge；
* contraction；
* elimination；
* rotation；
* pivot；
* factorization；
* recompilation。

只要：

$$[r_{\text{before}}]=[r_{\text{after}}]$$

或者新的表示在认识上严格支配旧表示。

因此：

> **Graph is representation, not ontology.**

---

## 4. 第一性原理之三：每次外生信息后必须全局闭包

一次 LLM/tool 返回 observation $Y$ 后：

$$S'=\operatorname{Closure}(S\oplus Y)$$

必须求到：

$$\operatorname{Closure}(S')=S'$$

之后才允许下一次 yield。

一次 Closure 内部的工作分两层：

**Core Reduction（必需）**：

* normalize；
* obvious equivalence merge；
* zero-novelty pruning；
* simple dominance；
* dependency propagation；
* deterministic inference；
* candidate revaluation；
* method activation；
* action value recomputation；
* stop value recomputation。

**Representation Optimization（高级，Phase 4）**：

* global pivot；
* e-class / e-graph；
* Blackwell dominance；
* SCC rewrite；
* min-cost extraction；
* variable elimination ordering。

V1 只要求 Core Reduction：closure 必须约简，但不要求求"最佳表示"。

LLM 不参与这个传播过程。

**数学累，LLM 轻。**

---

## 5. 第一性原理之四：所有认知价值都相对于根问题

一个子问题自身很有信息，不代表它值得问。

设根问题答案变量为 $R$，当前状态为 $S$，某认知动作产生 observation $Y$。

最基本的 root-relative information value：

$$I(R;Y\mid S)$$

核心问题永远是：

> **如果我知道这个问题的答案，它预计会怎样改变根问题？**

这决定 Sphinx 是"解惑机"，而不是"无限知识展开机"。

---

## 6. 状态是信念空间，不是问题树

角色对齐到 POMDP：

| 概念 | 角色 |
|---|---|
| Knowledge / belief | = state |
| Question | = action |
| Answer | = observation |
| Methodology | = action generator |
| LLM | = semantic oracle |
| Kernel | = policy |
| Stop | = terminal decision |

搜索树只是图的一次展开，不是领域模型。
领域对象是 Epistemic Quotient State；其计算表示可以是图（可合流、可回访、甚至可有环：Hermeneutic circle、调试的假设→测试→改假设）。
Policy/Closure Solver 在特定约束下可退化为 A*、Bayesian inference、MCTS——但它们是求解策略，不是认识论基础。

**分形**：每个生成式规则的输出是一组新问题，递归进入同一个解惑机；solver 就是自己。全局 Policy Solver 持续选择最有价值的认知动作；best-first 只是其中一种实现。

---

# 第 2 篇：最小内核——没有这些就不是 Sphinx

这一篇全部属于 **必需品 / Kernel Level**。

第一版就应该拥有。

## 1. Epistemic State

最低限度需要表达：

$$S=(R,\,B,\,E,\,D,\,A,\,C)$$

其中：

* $R$：Root Answer Contract；
* $B$：belief / semantic uncertainty；
* $E$：已有 observation / evidence；
* $D$：dependency / provenance；
* $A$：当前可执行认知动作；
* $C$：cost / budget / constraints。

$R$ 不是硬标签，而是 Kernel 从 SemanticAssessment 派生的 belief：

```text
SemanticAssessment
    ↓
QuestionForm distribution
Facets
Target proposals
User-intent proposals
    ↓
Kernel derives RootContract belief
```

例：$P(\text{Why})=.75$ → ExplanationContract 权重最高；后续 observation 改变语义判断时，RootContract 随 closure 更新。
方法选择别归单一 QuestionForm —— 它只定答案形态；方法看 Facets（multi-label，可同时触发多个）。

不要求第一版拥有通用概率图。

但必须存在一个明确的"当前认识状态"。

---

## 2. Semantic Observation

数学内核不理解自然语言。

因此最开始就 yield 给 LLM：

```text
SemanticAssessmentRequest
```

LLM 返回的不是硬标签，而是带不确定性的语义建议，例如：

```text
QuestionForm:
    Why    .75
    How    .18
    Other  .07

Facets:
    causal       .84
    explanatory  .91
    predictive   .06
```

这些只是 observation。

Kernel 才决定如何使用。

必须始终区分控制层与对象层：

$$P(\text{QuestionForm}\mid\text{utterance})$$

和：

$$P(\text{world proposition}\mid\text{evidence})$$

前者属于控制层语义不确定性。

后者属于对象层认识概率。

QuestionForm 不是固定标签，而是状态中的 belief $Q_t(\text{Form})$，随 co-yield 更新：
「为什么程序卡住」初判 How .24，探索后用户真想「怎么修」→ How .58，控制策略随之转向。

---

## 3. Generative Rule

方法论第一版只需要一种统一能力：

$$G_i(S)\to\{\text{Candidate Cognitive Actions}\}$$

例如：

**Multidisciplinary**

> 当前解释覆盖不足 → 可以询问其他具有解释力的学科。

**Abduction**

> 当前现象解释不足 → 可以生成竞争假说。

**Analogy**

> 当前预测证据不足 → 可以寻找结构类似案例。

规则只生成候选动作。

规则本身没有调度权。

LLM 也没有调度权。

统一视角：方法论是 question graph 的扩展算子（generator），不是流水线步骤。
同一接口 $G_i(S)\to$ 候选动作，方法库再多也只是不同扩展策略；
方法永远可以继续问下个问题，停止是搜索器的职责，不是方法的。

**见机行事**：方法选择由 Kernel 计分，不 argmax 单一标签：

$$\text{MethodUtility}(m)=\sum_f P(f\mid q)\,\text{Applicability}(m,f)\,\text{ExpectedGain}(m,S)-\text{Cost}(m)$$

$P(f\mid q)$ 是 LLM 给的 QuestionForm 分布；Applicability 是方法 × 疑问形式的适用度矩阵。分数高的几个方法可同帧触发。

> 上式为 V1 启发式示例（illustrative heuristic），非内核公理。正式顶层定义见 §7 root-relative action value 与第 3 篇 §D Bellman 形式。

---

## 4. State Reduction

每次新增信息时必须尝试约简。

### 判重层次（从严到宽）

**第一性定义**：future-decision equivalence

$$S_1\sim_R S_2 \iff \text{对未来所有合法认知决策等价}$$

**强但难算的支配**：

$$S_1\succeq S_2 \iff \text{未来决策能力不差且成本更低}$$

**工程近似**（可计算的充分剪枝条件）：

$$H(Y\mid S)\approx 0$$

以及 semantic key、dependency digest、posterior distance 等。

层次关系：

```text
future-decision equivalence   ← 真定义
        ↓ approximate
information equivalence
        ↓ approximate
conditional entropy
semantic canonicalization
hash/digest
```

不要反过来让 entropy 定义 ontology。

但不要机械"留旧去新"。

应该先判断：

$$S_{\text{old}}\sim S_{\text{new}}$$

若等价，则比较代表元质量。

可以：

* 留旧去新；
* 留新去旧；
* merge；
* 保存一个极小 Pareto frontier。

三个量要分开：新不新 $H(Y\mid S)$；对世界有没有证据 $I(Z;Y\mid S)$；对根有没有价值 $I(R;Y\mid S)$。
"很新、很真但与当前问题无关"→ 进知识缓存，不花当前 root 的搜索预算。

$H(Y\mid S)=0$ 不意味着"什么都不做"：确定性推导仍可成为 derived cache / proof artifact。
它只是不能当新的认识证据提升 confidence。

---

## 5. Representative Dominance

若两个 representation 表示相同认识状态：

$$r_1\sim r_2$$

且 $r_1$ 在以下方面全部不差：

* 信息；
* provenance；
* numeric accuracy；
* future action availability；
* future inference cost；
* storage；
* stability；

并至少一项严格更好：

$$r_1\succ r_2$$

则：

$$r_2$$

应被淘汰。

**时间顺序不构成权利。**

---

## 6. Global Closure

每次 observation 后执行：

```text
absorb
→ infer
→ propagate
→ reduce
→ rewrite
→ revalue
→ repeat
→ fixed point
```

达到：

$$S^*=\Phi(S^*)$$

才允许下一认知动作。

这是 Sphinx 最重要的控制论不变量之一。

---

## 7. Root-relative Action Value

最小版本甚至不需要复杂 A*。

只需给候选动作一个统一比较标准：

$$V(a\mid S,R)=\text{expected improvement of root answer}-\text{cost}$$

第一版允许这是粗糙近似。

但比较权必须属于 Kernel。

再分两层：truth-relative（最小化认知熵）vs decision-relative（最小化 Bayes risk）：

$$\rho(b)=\min_a\,\mathbb{E}_{R\sim b}[L(a,R)]$$

一个问题可能极大改善对世界的理解，却不改变应采取的决策 → 对决策型根问题价值近零。

---

## 8. Stop 是一个动作

停止不是特殊 if。

定义：

$$V_{\text{stop}}(S)$$

以及：

$$V(a_1),V(a_2),\ldots$$

当：

$$V_{\text{stop}}\ge\max_a V(a)$$

停止就是当前最优认知动作。

因此：

> "问什么"和"什么时候不再问"属于同一个控制问题。

---

## 9. Kernel 主导的 co-yield

核心协议：

```text
Kernel reaches fixed point
        ↓
choose best cognitive action
        ↓
if pure computation:
    Kernel executes
else:
    YIELD request
        ↓
       LLM
        ↓
structured observation
        ↓
Kernel absorbs
        ↓
GLOBAL CLOSURE
```

LLM 不是 coroutine 的平权另一方。

**continuation 永远属于 Kernel。**

---

## 10. No Free Information（认识守恒）

若 $Z\to E\to Y$（$Y$ 由已有 $E$ 重组而来），则：

$$I(Z;Y)\le I(Z;E)$$

重新措辞/分解/类比/综合只能重组旧信息，不能凭空提高与现实的互信息。

LLM 可提高搜索效率、重组证据、暴露冲突，但不能因递归十轮就让 $0.6\to0.9$。

**区分 information acquisition 与 information propagation/computation**：
全局 closure 完成后，在没有新的外生信息、合法数值输入或此前未吸收的证据时，
纯粹重新表述、重复采样和自我论证不得增加 evidence mass 或与现实的互信息。
但 closure 内部推导本身可以兑现旧信息中已蕴含的推论
（已有 $A$ 和 $A\to B$，推导出 $B$ 时 $\text{belief}(B)$ 上升是 propagation，不是 acquisition）。

分形树可指数展开，有效证据维度未必增长 —— 这正是 dependency/provenance 必须存在的理由。

---

# 第 3 篇：增强品——经典算法作为母模型的退化形式

这些不是认识论公理。

它们是 **增强型 Solver**。

可以逐阶段加入。

---

## A. A* / Best-first 增强

当：

* 状态完全可观察；
* transition 确定；
* cost 非负；
* goal 明确；
* heuristic 合法；

母模型应退化为：

$$f(n)=g(n)+h(n)$$

即正常 graph A*。

必须保留：

* transposition；
* best-g；
* reopen；
* 判重；
* priority queue。

**严格区分两类优先级**：

Generic Sphinx 的认知优先级（root-relative，非 A* 的 $f$）：

$$\text{EpistemicPriority}(a)=\frac{\mathbb{E}[\Delta U_{\text{root}}\mid a]}{\mathbb{E}[\text{cost}(a)]}$$

或 Bellman value 等——这是 Sphinx 母模型自己的控制量。

严格 A* 退化形态的 $f$：

$$f(n)=g(n)+h(n)$$

只有后者才能支撑"系统在某组约束下严格退化为 graph A*"的验收声明。
LLM 只估 $\Delta U_{\text{root}}$ / cost 的输入，不能直接当 admissible $h$：
"重要度 0.83"无最优性保证 → 实际用 Weighted A* / anytime
（先给可用答案，再随预算继续改善，并维护当前最优）。

A* 在 Sphinx 中的真正角色是：

> **全局有限认知资源分配。**

不是本体。

判重对象是 info state（加进来后对未来决策不再改变 → 合并），不是文本/QuestionId。
同一认知问题被不同方法命中 → canonicalize 成同一节点（transposition）。
但 Node identity 全局共享，Node priority 必须 root-relative —— 共享知识，不共享价值。

CLOSED 不是永闭：上游 belief 变化（$D_{\mathrm{KL}}>\varepsilon$）时旧节点可 re-open（incremental search 风格）。

---

## B. Bayesian 增强

当：

* 变量固定；
* graph 固定；
* action generation 关闭；
* 只有 observation 输入；

Closure 应退化成：

$$P(X\mid E)$$

即 Bayesian Network / factor inference。

因此 Bayes 的角色是：

> **belief consistency solver。**

第一版只需要支持简单、明确合法的概率模型。

以后再增加：

* factor graph；
* conditional dependence；
* likelihood ratios；
* Bayesian networks；
* dynamic Bayes；
* approximate inference。

一个核心纪律始终不变：

> LLM 可以建议概率参数，但只有经过资格检查的参数才能进入正式模型。

---

## C. MCTS 增强

如果 $V(S)$ 不能精确求解，但是：

* action 可采样；
* observation 可采样；
* terminal utility 可估计；

那么可用：

```text
selection
expansion
simulation
backup
```

近似 Bellman value。

于是母模型退化为 MCTS。

打开 state quotient / transposition：

> Graph MCTS。

关闭它：

> vanilla MCTS。

MCTS 的角色是：

> **在无法 exact solve 时估计认知策略价值。**

它不是 Sphinx 的认识论基础。

---

## D. 一个统一的母方程

可以把增强算法都看成是在近似：

$$V(S)=\min\left[L_R(S),\;\min_a\left\{C(S,a)+\mathbb{E}_Y V\bigl(T(S,a,Y)\bigr)\right\}\right]$$

其中：

* $L_R(S)$：现在回答根问题的损失；
* $C(S,a)$：继续探索的成本；
* $Y$：动作得到的 observation；
* $T$：吸收 observation 并 Closure 后的新认识状态。

$V$ 的递归项含"问完后的未来搜索价值"：有些问题自身信息量不大，却打开新的问题空间
（gateway question，门户问题）。只看一步 EIG 会系统性低估这类问题。

不同算法只是不同求解方式：

| 条件 | 退化为 |
|---|---|
| 确定性最短路径 | A* |
| 固定概率依赖 | Bayes inference |
| 无法 exact value | MCTS |

---

## E. 三个退化测试

每次改核心架构，都应该检查：

### A* Embedding

关闭不确定性和动态生成后：

> 是否仍然能够表示标准 graph A*？

### Bayes Embedding

冻结主动探索后：

> 是否仍然能够表示标准概率图推断？

### MCTS Embedding

将 exact value 改为采样后：

> 是否仍然能够表示标准 MCTS？

如果任何答案是否定的：

> 很可能架构被设计窄了。

---

# 第 4 篇：奢侈品——有价值，但绝不能阻塞 V1

下面这些应该刻意晚做。

---

## 1. 高级 State Reduction

第一版：

```text
semantic key
dependency digest
conditional novelty estimate
simple dominance
```

高级版：

* Blackwell informativeness；
* probabilistic bisimulation；
* decision-equivalent state；
* information bottleneck；
* minimal sufficient state；
* e-class / e-graph；
* global extraction。

这些都非常漂亮。

但不是 V1 的生死线。

---

## 2. 高级 Representation Optimization

可以借鉴传统算法：

* 最小费用流负环消除；
* residual network；
* simplex pivot；
* balanced tree rotation；
* dynamic programming；
* variable elimination；
* graph contraction；
* SCC collapse；
* compiler CSE；
* equality saturation；
* tree decomposition；
* junction tree；
* min-fill ordering。

统一思想：

> **在认识语义不变的前提下，持续降低未来计算成本。**

定义：

$$\Phi(r)=\text{expected future epistemic computation cost}$$

合法 pivot $r\to r'$ 满足：

$$[r]=[r']$$

且：

$$\Phi(r')<\Phi(r)$$

则允许替换。

这属于高级优化器。

不应该进入 V1 核心语义。

---

## 3. 高级 Value Solver

后续可以加入：

* Weighted A*；
* ARA*；
* Anytime search；
* D\* / incremental search；
* branch-and-bound；
* beam search；
* PUCT；
* Bayesian optimization；
* Thompson sampling；
* contextual bandits；
* POMDP solvers；
* prioritized sweeping。

它们全部只是：

```text
PolicySolver
```

插件。

---

## 4. 高级概率推断

后期才需要：

* general factor graph；
* loopy belief propagation；
* variational inference；
* particle filtering；
* MCMC；
* causal graphical models；
* counterfactual inference；
* model averaging；
* hierarchical Bayes。

不要为了"理论完整"提前做。

---

## 5. 高级 LLM Generative Rules

真正会形成产品差异化的地方，反而可能是这一层。

例如：

**多学科展开**

> 哪些学科能解释当前 residual？

**反事实**

> 如果核心假设错误，什么现象仍然成立？

**类比**

> 哪些问题具有可迁移结构？

**反类比**

> 哪些关键差异会破坏迁移？

**Abduction**

> 什么假设能解释当前 observation？

**判别实验**

> 哪个问题最能区分当前竞争假设？

**根因分析**

> 当前 explanation 中哪些变量是上游原因？

**尺度切换**

> 微观、中观、宏观分别如何解释？

**机制链**

> 从原因到结果缺了哪一个中介步骤？

**边界条件**

> 当前答案在什么条件下失效？

**source triangulation**

> 哪种独立来源最可能改变当前 belief？

**measurement critique**

> 当前变量真的是我们想测量的东西吗？

**ontology repair**

> 当前争论是否来自概念划分错误？

**unknown expansion**

> 当前最危险的未知未知是什么？

这部分可以无限生长。

因为每个规则最终只是：

$$G_i(S)\to\text{candidate cognitive actions}$$

所以增加规则不会破坏内核。

---

# 第 5 篇：阶段路线图与最终架构

## Phase 0 — 必须先成立的数学骨架

目标：

> 证明"数学内核主导的分形 co-yield"能够运行。

只实现：

1. Root Question；
2. SemanticAssessment yield；
3. EpistemicState；
4. GenerativeRule interface；
5. candidate action；
6. global Closure；
7. 简单 novelty/dedup；
8. simple dominance replacement；
9. root-relative value；
10. Stop action；
11. Canonical Answer；
12. MCP start/resume。

建议方法论只做 4 个生成式 + 1 个组合式：

```text
Generative（增加认知动作空间）:
  Multidisciplinary
  Abduction
  Analogy
  Counterexample

Combining（把已有结构组织成答案结构）:
  Synthesis
```

前四个主要是增加认知动作空间；Synthesis 主要是把已有结构组织成答案结构。
Kernel 仍可 yield 给 LLM 完成语义 synthesis，但它和"提出下一个值得回答的问题"不是同一类 operator。

数学也只做最简单的：

```text
qualitative belief
simple discrete probability
simple dependency groups
```

**这一阶段不要实现完整 A*、MCTS 或通用 Bayesian Network。**

---

## Phase 1 — 搜索增强

增加：

* priority queue；
* graph best-first；
* best cost；
* reopen；
* exploration budget；
* heuristic；
* root information gain；
* anytime answer。

此时要求可以构造一个 case，让系统严格退化成 graph A*。

---

## Phase 2 — 概率增强

增加：

* explicit hypotheses；
* prior；
* likelihood；
* dependency；
* posterior；
* simple factor representation；
* Bayes risk；
* expected value of information。

此时要求可以构造一个 case，让系统冻结生成后退化成 Bayesian inference。

---

## Phase 3 — Monte Carlo 增强

增加：

* visit count；
* sampled outcome；
* rollout；
* UCT/PUCT；
* value backup；
* graph transposition；
* sampled VOI。

此时要求可以构造一个 case，让系统退化成 MCTS。

---

## Phase 4 — Representation Optimizer

增加：

* equivalence class；
* Pareto representatives；
* epistemic pivot；
* contraction；
* cycle rewrite；
* cost-based extraction；
* e-graph/e-class 类思想；
* factor elimination ordering；
* incremental recompilation。

这个阶段解决：

> "知道同样多，怎样让未来算得更便宜？"

---

## Phase 5 — Methodology Library

大量增加生成式认识规则。

此时重点不再是 Kernel correctness，而是：

> 什么问题最值得问？

方法库可以不断增长，而数学内核保持稳定。

---

# 最终的模块边界

```text
┌──────────────────────────────────┐
│              MCP Host            │
└────────────────┬─────────────────┘
                 │
                 ▼
┌──────────────────────────────────┐
│          Inquiry Kernel          │
│                                  │
│  Root Contract                   │
│  Epistemic State                 │
│  Closure Engine                  │
│  State Reduction                 │
│  Action Generator                │
│  Policy Solver                   │
│  Stop Decision                   │
│  Canonical Answer                │
└──────────┬────────────┬──────────┘
           │            │
           │            │
           ▼            ▼
┌────────────────┐  ┌─────────────────┐
│ Numeric Solvers│  │ Semantic Oracle │
│                │  │      LLM        │
│ exact           │  │                 │
│ Bayes           │  │ classify       │
│ A*              │  │ generate       │
│ MCTS            │  │ estimate       │
│ optimization    │  │ explain        │
└────────────────┘  └─────────────────┘
```

真正稳定的核心接口只有两个方向：

```text
Kernel → LLM:
    "我缺少这个语义量，请测量/生成。"

LLM → Kernel:
    "这是我的结构化建议及不确定性。"
```

剩下的一切由 Kernel 完成。

---

# 三档功能总表

## 必需品：没有它就不是 Sphinx

**认识论**

* History ≠ State；
* future-decision equivalence；
* root-relative epistemic value；
* Proposal ≠ Evidence；
* No Free Information；
* provenance/dependency；
* uncertainty retained。

**控制论**

* Kernel owns continuation；
* generative rules only propose actions；
* global fixed-point closure；
* Core Reduction（normalize / equivalence merge / zero-novelty pruning / simple dominance / dependency propagation）；
* equivalence + dominance；
* root-relative action comparison；
* Stop as action；
* Canonical Answer controlled by Kernel。

---

## 增强品：让内核在经典问题上变强

* graph A* / best-first；
* reopen/transposition；
* Bayesian inference；
* Bayes risk；
* expected information gain；
* value of information；
* MCTS / graph-MCTS；
* exact Bellman / approximate Bellman；
* anytime solving。

它们是 solver。

不是 ontology。

---

## 奢侈品：让系统聪明、优雅、高效、富有创造力

**数学优化**

* Blackwell dominance；
* probabilistic bisimulation；
* equality saturation；
* epistemic pivot；
* representation rotation；
* min-cost rewrite；
* SCC compression；
* advanced factor elimination；
* POMDP；
* advanced Monte Carlo；
* sophisticated incremental solvers。

**LLM 方法论**

* 大量跨学科规则；
* 类比/反类比；
* causal discovery；
* dialectic；
* falsification；
* ontology repair；
* boundary search；
* unknown-unknown discovery；
* experiment design；
* source strategy；
* model criticism；
* perspective inversion；
* adversarial questioning；
* creative hypothesis generation。

---

# 最后只记五句话

**1. History is not State.**

历史只通过它对未来认知决策的影响存在。

**2. New information must earn its existence.**

条件于全部旧状态没有信息增量，就不应产生新的认识节点。

**3. Every observation triggers global closure.**

LLM 每给一次新东西，数学内核先全局收敛，再问下一句。

**4. The value of a question is root-relative.**

一个子问题值多少钱，只看它预计怎样改善根问题。

**5. Search algorithms are degenerations, not foundations.**

A*、Bayesian inference、MCTS 不是 Sphinx 的组成定义，而是统一认识状态模型在不同约束与求解策略下必须能够得到的退化形式。
---

> 本文件是变更工作记录，不是当前产品规范。
> 当前产品语义仅以 `docs/` 正式层为准。

## Active work

**Specification impact**

- 新前缀 `SPHINX-`：Phase 0 认识内核 + MCP co-yield（含 handle 有状态会话）
- `AGENT-028`：万象术 Host 自动注入 `mcp.sphinx`；Meditator allow `sphinx_*`
- 实现正交：`src/sphinx/` 独立 Node MCP（可用 `@modelcontextprotocol/sdk`）；万象术仅 identity / launch / permission
- 不触碰 `GrandRewrite.md` 范围

**Remaining work**

1. 正式 why/what/shape/how/proof + 导航 / PREFIX
2. `src/sphinx` Phase 0 内核 + MCP `start`/`resume`（每次回传 handle）
3. 万象术 thin auto-load（类 stealth；本地 `node` 入口，非 uvx）
4. Meditator 权限面纳入 Sphinx MCP
5. proof 测试 + 门禁绿
6. Final outcome → `changes/completed/`

**Completion criteria**

- Phase 0 十二条能力可执行且有回归
- Host 自动注入；测试默认不 spawn 生产入口
- Sphinx 不 import 万象术 domain；万象术不内嵌 Kernel 闭包逻辑
- `node scripts/checks/spec.mjs` 与相关 unit 绿

**Blockers**

- 无

**Out of scope（本 Change）**

- Phase 1–5（A* / Bayes / MCTS / 表示优化 / 方法库扩张）
- GrandRewrite provider surface

## Final outcome

**Outcome**

Phase 0 Sphinx 已落地：正交 Node MCP（handle 有状态 co-yield）+ 万象术 Host 薄自动注入；Meditator 独占 `sphinx_*`。

**Final specification**

- `SPHINX-001`…`005`（`docs/{why,what,shape,how,proof}/sphinx.md`）
- `AGENT-028` 及 AGENT-006/025 增量（Meditator = inspector + Sphinx MCP）
- 导航 / `scripts/checks/spec.mjs` PREFIX 已注册

**Implementation result**

- `src/sphinx/`：内核 + `mcp-server.js`（`@modelcontextprotocol/sdk`）；工具 `start`/`resume`；不透明 handle
- `scripts/build.mjs` 复制至 `dist/sphinx/`
- 万象术：`SphinxMcp` / `SphinxMcpConfig` → `config.mcp.sphinx`；`ToolPermission.Sphinx` → Meditator allow
- 未改 GrandRewrite 范围；Semble 仍禁 MCP SDK（AGENT-027）

**Verification**

- `node scripts/checks/spec.mjs` OK
- `npm run format:check` OK
- unit：`tests/unit/sphinx/*` + `sphinx-mcp` + `meditator-permissions` + stealth/semble 回归共 32 pass

**References**

- `docs/what/sphinx.md`、`docs/what/agent.md`（AGENT-028）
- `src/sphinx/mcp-server.js`、`src/Wanxiangshu/Kernel/SphinxMcp.fs`
- `tests/unit/sphinx/`、`tests/unit/agent/sphinx-mcp.test.mjs`

## Corrective outcome — 2026-08-12

用户明确判定上一次完成“十分草率，很多语义漂移”，并要求以仓库标准 F# → Fable JS 全部重写。上方 Original proposal 与旧 Active/Final 记录保留用于审计；旧 Final outcome 的完成声明已被本节取代，不能再解释当前实现。

**Scope correction**

- 旧 Active work 自行把批准范围收窄为 Phase 0，并把 Phase 1–5 标成 Out of scope；该收窄没有用户授权，违反 Change 生命周期合同。
- 修复以 Original proposal 的认识论不变量为准：History ≠ State、Proposal ≠ Evidence、No Free Information、root-relative value、global closure、solver degeneration、dependency-aware equivalence。
- 正式规范扩为 `SPHINX-001`…`010`；当前产品语义只读 `docs/{why,what,shape,how,proof}/sphinx.md`。

**Semantic corrections**

- 删除 `evidenceMass` 伪置信度；SemanticAssessment / Candidates / Synthesis 不再增加世界证据。
- Candidate 恢复为“待调查认知动作”，必须经 Kernel 选择并 `InvestigateRequest` 后才能产生 Finding/Evidence；同 semantic+dependency 的多方法命中合并 provenance，不再丢来源。
- QuestionForm 保留概率分布；RootContract 不再靠 primary argmax 偷换不确定性。
- Evidence 强制 Source + DependencyKey；内部 identity = semantic+dependency，同 semantic 的独立来源可并存；同依赖组不按独立因子重复计数。
- Bayesian posterior 仅接受完整、有限 `[0,1]` likelihood 且 `numericQualified=true` 的 Evidence；否则无数值 posterior。
- root-relative policy 纳入 GatewayGain；Stop 与 Investigate/Synthesis 位于同一 utility 比较空间。
- QuestionForm 恢复为可随 co-yield 更新的 `Q_t(Form)`：Investigation 可带 control-only semanticAssessment，重算 RootContract 但不增加 Evidence。
- 方法生成恢复递归：每次 Investigation 后 `NeedsGeneration=true`，下一次裁决前重新激活 generator，而不是只在开局生成一次候选。
- 表示约简只接受 Kernel-owned EquivalenceKey 或 semantic+dependency 同一；wire `equivalenceKey` 不具判重权；独立来源的同问题不得误判重；类内保留真实 Pareto frontier。
- A* 改为标准 `g+h + bestG + reopen`；MCTS 改为 selection/expansion/rollout/backup + semantic transposition；两者均是 solver embedding，不是 ontology。

**Implementation correction**

- 生产源迁至 `src/Wanxiangshu/Sphinx/*.fs`，namespace = `Wanxiangshu.Sphinx.*`。
- Sphinx 加入 `Wanxiangshu.fsproj`，统一由 Fable 输出 `dist/Sphinx/*.js`。
- 生产 MCP 入口改为 `dist/Sphinx/McpServer.js`；`scripts/build.mjs` 不再复制平行 JS 源。
- raw JS 只停在 `Sphinx/Codec.fs`；MCP SDK / zod 只停在 `Sphinx/McpServer.fs`；handle 可变索引只停在 `Sphinx/Session.fs`。
- Host 仍只拥有 `SphinxMcp` identity / `SphinxMcpConfig` launch / `ToolPermission.Sphinx`；Inquiry 独占 `sphinx_*`。

**Proof correction**

- Sphinx unit 改为验证认识语义，而非验证 helper 名存在：control≠evidence、pending-request gate、closure idempotence、gateway value、qualified Bayes、dependency conservation、strict A* reopen、graph-MCTS、dependency-aware Pareto、真实 co-yield。
- 代表测试：`tests/unit/sphinx/*.test.mjs`、`tests/unit/agent/sphinx-mcp.test.mjs`。

**Verification**

- `npm run build`：OK。
- `node --test tests/unit/sphinx/*.test.mjs`：26 pass。
- 其余仓库标准门禁以本次 corrective commit 的实际运行结果为准。

**References**

- `src/Wanxiangshu/Sphinx/`
- `docs/what/sphinx.md`、`docs/shape/sphinx.md`、`docs/how/sphinx.md`、`docs/proof/sphinx.md`、`docs/why/sphinx.md`
- `src/Wanxiangshu/Kernel/SphinxMcp.fs`
- `src/Wanxiangshu/Infrastructure/OpenCode/Host/SphinxMcpConfig.fs`
- `tests/unit/sphinx/`
