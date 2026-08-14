# Requirement Packages 重构：新对话保姆级交接

> 这是当前 Requirement Package ontology 设计工作的**主交接文档**。
>
> 新对话不要从 Repomix、旧总结、包数量或当前目录名反推设计。先读本文件，再直接阅读真实 checkout。
>
> 仓库：`/home/kunweiz/Desktop/vibe/wanxiangshu/`

---

# 0. 一句话任务

把万象术现有 `docs/`、代码、tests、static gates、completed changes 中混杂的产品语义重新因式分解为一组**小、正交、可独立验收、同时为真的 Requirement Packages**；每个规范命题与每条 executable semantic proof 都有唯一 package owner。

这不是文档改名，也不是把现有 `docs/what/*.md` 一文件映射一个包。

最终目标是：

```text
current accepted truth
    = conjunction of accepted Requirement Packages

每个 package
    = 一个不可替代 WHY
    + 一组唯一拥有的 WHAT propositions
    + 显式 hard dependencies
    + package-local proof ownership
```

包数量是推导结果，不是目标。

当前设计得到 **45 个候选 package**。45 随后续全仓反向审计仍可增加、拆分、合并或删除。

---

# 1. 先分清三层 authority

这是整个任务最重要的防误操作规则。

## 1.1 当前正式产品语义：`docs/`

当前 checkout 仍遵守根 `AGENTS.md`：

```text
docs/{why,what,shape,how,proof}
```

中的 Clause 是**现行 normative authority**。

只要 Requirement Package cutover 尚未真正执行：

- 不得把 `requirements-design/` 当成已经生效的产品规范；
- 不得为了让当前实现符合未来设计而偷偷改旧 Clause；
- 不得形成 `docs/` 与未来 requirement tree 两套同时自称权威的正式世界。

## 1.2 未来 ontology 设计：`requirements-design/`

本目录只回答：

> 如果重新设计规范所有权，未来应该有哪些语义 owner，它们分别拥有和不拥有什么？

它可以否定旧文档边界，可以判断某个旧 Clause 只是 HOW / compatibility / migration sediment，也可以发现旧 36 工作集漏包。

它目前不是 runtime contract。

## 1.3 历史证据：`changes/`、旧 tests、旧 gates

这些材料说明：

- 某个问题为什么曾经发生；
- 当前实现为什么长这样；
- 某个 invariant 是否确实有现实失败模式；
- 当前有哪些 executable proof 可迁移。

它们**不能因为存在就自动升级为未来永久 requirement**。

`changes/completed/` 往往比 `docs/why` 更详细：保留被拒方案、迁移细节、proof plan、失败模式复盘与完整推导链。当 `docs/why` 只留下结论而缺推理过程时，completed change 是 WHY 考古的第一手参考，参考价值高——但这不改变「历史证据 ≠ 规范权威」的边界：参考价值是考古价值，不是 authority。

尤其注意：

```text
historical clean-break assertion ≠ permanent product requirement
current implementation shape       ≠ ontology
current test file                   ≠ future proof owner
current component name              ≠ package identity
```

---

# 2. 不再使用 Repomix 作为事实源

用户已明确允许直接读取真实 checkout，并明确“不再 repomix”。

后续考古以：

```text
/home/kunweiz/Desktop/vibe/wanxiangshu/
```

的真实文件为 source of truth。

新对话应：

1. 用 DevSpace `open_workspace` 打开上述路径，`mode=checkout`；
2. 阅读根 `AGENTS.md`；
3. 查看 `git status --short --branch`；
4. 阅读本文件、`README.md`、`INDEX.md`、`AUDIT.md`、`PROOF-MAP.md`；
5. 再按任务需要直接 `read` / `rg` 当前 `docs/`、`src/`、`tests/`、`scripts/checks/`。

不要优先读 `repomix-output.xml`。

---

# 3. 当前设计目录

```text
requirements-design/
├── README.md
├── HANDOFF.md
├── INDEX.md
├── AUDIT.md
├── PROOF-MAP.md
├── COVERAGE.md
├── EVIDENCE.md
├── CHANGES-AUDIT.md
├── 01-meta-programming.md
├── 02-session-host.md
├── 03-participant-core.md
├── 04-cognitive-environment.md
├── 05-decision-surface.md
├── 06-language.md
├── 07-projection.md
├── 09-test.md
├── 10-interaction.md
├── 11-test.md
├── 12-work-execution.md
├── 13-context-continuity.md
├── 14-recovery.md
├── 15-mission-review.md
├── 16-feedback.md
├── 17-repository.md
├── 18-optimization-epistemics.md
├── 19-distribution.md
├── 20-capability-external.md
└── 21-work-record.md
```

编号文件只是设计期把邻近 boundary cards 放在一起，**不是 package identity，也不是依赖顺序**。

重点文件：

- `README.md`：候选 package 的接受标准。
- `INDEX.md`：45 包全景、关键拆分裁决、依赖骨架。
- `AUDIT.md`：ORPHAN / OVERLAP / GARBAGE / unresolved questions。
- `PROOF-MAP.md`：现有 tests/gates 到 future unique proof ownership 的第一版投影。
- 各编号文件：完整 boundary card。

每张 boundary card 必须有：

```text
WHY
OWNS
DOES NOT OWN
DEPENDS ON
PROVIDES
FAILURE MEANING
INDEPENDENT CHANGE
CURRENT EVIDENCE
```

当前 45 张卡已经全部满足该 schema。

---

# 4. 设计铁律

后续每一次拆包、并包、归属判断都用下面这些，而不是看旧目录。

## 4.1 一个 package 最好只有一个不可替代 WHY

最重要的问题：

> 为什么这个 requirement 必须独立存在？

如果答案里出现两个互不依赖的“因为”，优先考虑拆。

## 4.2 Independent Change Test

对 A / B 问：

> B 能否发生重大 redesign，而 A 的 WHAT 完全不变？

若可以，强烈说明它们应该分包。

反过来，如果所谓两个包任何真实变化都必须同步修改同一规范命题，可能是假边界。

## 4.3 `DOES NOT OWN` 比 `OWNS` 更重要

垃圾桶包通常不是因为没有内容，而是因为没有边界。

每张卡都要能明确回答：

> 哪些看起来很邻近的事实**绝对不归我**？

如果一个包开始收容“所有模型应该知道的东西”“所有恢复”“所有 Host 行为”“所有安全规则”，立即停下做 split audit。

## 4.4 Requirement architecture ≠ production architecture

一个 production module 完全可以同时受多个 Requirement Packages 约束。

例如当前 `PromptAuthority.fs` 同时承载 interaction authority 与 dispatch bookkeeping，并不意味着未来必须存在一个 `prompt` package。

我们拆的是：

```text
semantic ownership
spec ownership
proof ownership
```

不是强迫生产代码微服务化。

## 4.5 所有 accepted packages 同时为真

dependency 只表示：

> 本包定义需要另一个包已经提供某个 guarantee。

它不表示：

- 优先级；
- override；
- 不可修改；
- “上层规范覆盖下层规范”。

未来 main branch 应表达一组同时成立的当前事实，而不是沉积式 amendment stack。

## 4.6 每条 executable semantic proof 恰好一个 owner

共享 checker/framework 可以存在。

但：

```text
one semantic assertion → one package owner
```

若一个 test 同时证明两个语义事实：拆 test / 拆 oracle。

若一个 integration/E2E 场景物理上经过多个包：可以是一条 Long Stroke，但其中每个 semantic assertion 仍有明确 package owner。

## 4.7 先定义 package boundary，再反向覆盖仓库

不要先问：

> 当前哪些文件实现一起？

要先问：

> 哪些 proposition 的 WHY 不同、可以独立改变、应该由不同 owner 负责？

然后再去仓库找 evidence / orphan / overlap / garbage。

---

# 5. 当前 45 个候选 package

下面是当前全集。详细边界以对应 card 为准。

## 5.1 Requirement system

1. `requirement-system` — 当前 accepted truth 必须有唯一 package owner、显式 dependency 与唯一 proof ownership。
2. `verification-system` — acceptance 必须由分层、可失败、可重放 evidence 定义。

文件：`01-meta-programming.md`

## 5.2 Programming / causality

3. `structured-workflow` — 业务流程由语言结构直接表达，不在领域层重造第二程序计数器/runtime。
4. `time-capability` — 时间/等待物理能力显式进入系统，不能由 ambient clock/timer 偷渡。
5. `causal-wait` — 等待可观测可诊断，但诊断 observation 不能升级成业务 authority。

文件：`01-meta-programming.md`

## 5.3 Session / Host substrate

6. `session-ontology` — execution class、ownership、attachment、personhood 正交。
7. `managed-session-lifecycle` — managed session 创建、复用、取消、retire、replacement、owner closure 有单一合同。
8. `host-boundary` — 业务只依赖最小、可验证的 Host capability/observation boundary。

文件：`02-session-host.md`

## 5.4 Participant / provider world

9. `participant-identity` — Role ≠ Persona ≠ ExecutionBinding；换执行者不等于换人。
10. `office-capability` — office 由 entitled consequence 定义，不由 persona/tool whitelist 定义。
11. `capability-enforcement` — provider 看见的 capability 与 runtime 真能执行的 capability 同源，且不扩大 office entitlement。
12. `participant-horizon` — machine knowledge > participant experience；只让行动相关的最小事实穿过。
13. `cognitive-environment` — enduring World/Role/knowledge 与 Runtime/Mission 分开；knowledge 不创造 authority。
14. `action-affordance` — action decision surface 必须说明 act、适用时机、负边界、成功后果、参数意义。
15. `provider-language` — 一个 life 一个稳定 natural-language world；protocol identifiers 不翻译。
16. `provider-projection` — typed semantic intent 经唯一确定性投影成为 provider representation，representation 不反向创造 authority/state。
17. `external-investigation` — external/public-web facts 以 provenance、source quality、disagreement-aware observation 建立。

文件：

```text
03-participant-core.md
04-cognitive-environment.md
05-decision-surface.md
06-language.md
07-projection.md
20-capability-external.md
```

## 5.5 Interaction / effect / durability

18. `interaction-authority` — PhysicalUserMessage ≠ AuthorityTurn；只有 typed provenance 能创建/继续 logical interaction。
19. `dispatch-protocol` — 已获授权 interaction 穿过 unreliable Host 时避免 unknown outcome 复制逻辑效果。
20. `effect-accounting` — Requested / outcome unknown / Accepted 分型；未知不能伪装未发生或成功。
21. `durable-events` — immutable facts + atomic commit + deterministic fold = durable truth substrate。
22. `durable-convergence` — replicas 按对象语义收敛，不靠 wall-clock/LWW 猜赢家。

文件：

```text
09-test.md        # effect-accounting；历史文件名仅物理名
10-interaction.md
11-test.md        # durable-events / durable-convergence；历史文件名仅物理名
```

注意：`09-test.md` / `11-test.md` 名字来自一次写入过程中的临时物理选择，不是设计术语。未来整理 physical layout 时可以重命名；不能从文件名推断 package identity。

## 5.6 Work / execution

23. `delegation` — semantic work 转交另一 participant 时，authority、charge、owner、returned consequence 明确。
24. `process-execution` — 真实 process/PTY 有 bounded、可终止、物理完成可信的 execution semantics。
25. `output-distillation` — 大输出可以有损压缩，但 fragment 不能冒充整体成功、不能发明因果。
26. `change-integration` — 独立 Git road 进入共享 ref 时只短暂原子串行，长 review/repair 不占全局门。

文件：`12-work-execution.md`

## 5.7 Context continuity

27. `semantic-trace` — participant life 中不可丢失的原始 semantic history append-only、可定位。
28. `work-record` — 一段 bounded work 跨 participant/review/finality 传递时有 canonical statement，而不是 session-head summary / 固定 DTO。
29. `context-compression` — history 太长时，只在证据边界上用 semantic memory 替换可压缩区域。
30. `prefix-stability` — 同一 semantic epoch 已呈现前缀 byte-stable；冷边界由事实驱动。

文件：

```text
13-context-continuity.md
21-work-record.md
```

## 5.8 Failure / recovery

31. `provider-attempt-recovery` — provider attempt 失败后可 bounded 换 execution binding，但不改变 authority/personhood。
32. `crash-reconciliation` — process/plugin 中断后只从 durable facts + trusted physical observation 重入普通程序。
33. `degeneration-guard` — 未结束 attempt 已退化重复时主动止损，再交给正常 recovery。

文件：`14-recovery.md`

## 5.9 Mission / judgement / finality

34. `obligation-ledger` — mission 持续维护“仍欠世界什么”，不用 phase/status 伪装进度。
35. `review-judgement` — PERFECT/REVISE 是 discrimination + proportionate evidence judgement，不是表演式拒绝/checklist。
36. `review-assurance` — judgement 何时可消费，由 bounded evidence、fresh witness、causal confirmation 建立。
37. `finality` — irreversible mission end 基于 obligations + current tree + qualified review evidence，而不是 participant 自宣完成。

文件：`15-mission-review.md`

## 5.10 Feedback

38. `behavior-diagnosis` — pathology 只有满足 trigger / negative / distinction 的 evidence 才成立。
39. `guidance-delivery` — diagnosis 与何时/如何再次告知分离；有 occurrence、coverage、dedupe、horizon-relative delivery。

文件：`16-feedback.md`

## 5.11 Repository knowledge / programming

40. `repository-investigation` — repository claim 必须由可定位、可追溯真实 observation 建立；reasoning ≠ evidence acquisition。
41. `knowledge-reuse` — 历史 repository knowledge 只是 best-effort cache/hint，不冒充当前证明。
42. `repository-programming` — repository mutation 使用 capability-projected、sandboxed、all-or-nothing programming surface。

文件：`17-repository.md`

## 5.12 Optimization / epistemics

43. `speculative-investigation` — disposable speculation 只有 authoritative world 零影响时才可换成本收益。
44. `epistemic-reasoning` — proposal/evidence、dependency、不确定性、information action、closure 由认识状态 controller 明确治理。

文件：`18-optimization-epistemics.md`

## 5.13 Delivery

45. `distribution` — shipped artifact 携带 runtime code/resource closure；consumer 不依赖源码树/cwd 才能运行。

文件：`19-distribution.md`

---

# 6. 相对旧 36 工作集的关键设计裁决

旧 36 只是 archaeology workset，不保数字。

当前重要变化如下。

## 6.1 `participant-guidance` 被拆掉

旧名太容易成为：

```text
everything the model should know
```

垃圾桶。

现在分成：

- `cognitive-environment`：长期 World / self / inherited knowledge 的组织边界；
- `action-affordance`：调用瞬间的 act contract / boundary mirror。

其它 canonical facts 仍归真正 owner，例如：

- Coder mutation authority → `office-capability`；
- Reviewer PERFECT/REVISE meaning → `review-judgement`；
- Persona stability → `participant-identity`。

Prompt/Role Law 只是这些事实的 presentation surface，不获得 semantic ownership。

## 6.2 新增 `provider-language`

这是第一轮 participant audit 发现的明确 orphan。

“一个 life 的世界语言稳定、child 继承、协议标识不翻译”不是：

- identity；
- horizon；
- projection；
- guidance。

因此独立成包。

## 6.3 新增 `effect-accounting`

`durable-events` 只回答 durable truth substrate。

而：

```text
Requested ≠ Accepted
unknown outcome ≠ not happened
unknown outcome ≠ success
```

是跨 Prompt、Git publish、repository transaction 的 effect semantics，有独立 WHY。

## 6.4 `review-protocol` 拆两包

- `review-judgement`：判断到底是什么意思、何时应该 PERFECT/REVISE。
- `review-assurance`：这个判断什么时候有资格被消费，witness/seal/tree freshness/bounded evidence 如何建立。

可以整体重写 review philosophy 而不改 seal protocol，反之亦然。

## 6.5 `recovery` 拆两包

- `provider-attempt-recovery`：业务 attempt 已明确失败后的 bounded retry/fallback。
- `crash-reconciliation`：进程丢失临时状态后，从 durable + physical truth 恢复。

它们的 failure meaning 完全不同。

## 6.6 `process-execution` 抽出 `output-distillation`

控制进程生命周期与“如何诚实压缩过大 observation”不是同一个 WHY。

Distiller 的 fragment humility 因此成为独立 requirement，而不是 process runner 的附属 helper。

## 6.7 `sphinx` 改为 `epistemic-reasoning`

Sphinx/F#/MCP/A*/Bayes/MCTS 是现行实现与 proof evidence。

未来 package 拥有的是：

- Proposal ≠ Evidence；
- No Free Information；
- dependency-aware evidence；
- qualified posterior；
- controller-owned continuation/closure；
- information-action policy。

算法/组件可以整体替换。

## 6.8 新增 `capability-enforcement`

`office-capability` 回答：

> office 有资格产生什么后果？

`capability-enforcement` 回答：

> provider 看见的 capability 与 runtime 真能执行的 capability 如何保持同源，而且不能扩大 office entitlement？

Role permission matrix / schema gate 不是 office ontology 本身。

## 6.9 新增 `external-investigation`

Browser 的 external/public-web provenance 与 Inspector 的 local repository evidence 有不同 source/authority boundary。

不能因为两者都“查资料”就并入一个 investigation 包。

## 6.10 新增 `work-record`

LifecycleWorkRecord 被 delegation、process review、Finality 同时复用，且有独立 WHY：

> 一段 work 跨边界传递时，需要 bounded canonical statement；不是 receiver-relative summary，也不是固定 report DTO。

因此从 Companion/Review 中抽出。

---

# 7. 几组绝对不要再合回去的边界

这些是当前设计最重要的“防倒退”边界。

## 7.1 person / office / execution

```text
participant-identity ≠ office-capability ≠ capability-enforcement
```

分别回答：

```text
谁在行动？
这个 office 有资格产生什么后果？
系统如何确保可见/可执行 capability 不漂移？
```

当前 `Role`, `PersonaCatalog`, `ToolPermission`, `AttemptExecutionProfile` 在代码里相互靠近，不构成合并理由。

## 7.2 horizon / projection

```text
participant-horizon ≠ provider-projection
```

分别回答：

```text
什么信息有资格进入 participant experience？
已经决定可见后，怎样确定性表示？
```

Horizon filter 当前落在 projection 路径里只是 implementation fact。

## 7.3 cognition / action contract

```text
cognitive-environment ≠ action-affordance
```

长期“我是谁、世界怎样、前人学会什么”与调用瞬间“这个 verb 做什么/不做什么”可独立重写。

## 7.4 authority / dispatch

```text
interaction-authority ≠ dispatch-protocol
```

分别回答：

```text
这个逻辑 interaction 有资格发生吗？
已经决定发生后，如何穿过 unreliable Host 而不复制逻辑效果？
```

`authority.test.mjs` 当前混在一起，未来必须拆 proof ownership。

## 7.5 event storage / effect semantics

```text
durable-events ≠ effect-accounting
```

EventStore 不应该拥有每个外部 effect “未知结局”意味着什么。

## 7.6 judgement / assurance

```text
review-judgement ≠ review-assurance
```

“判断是否正确”与“判断是否因果上可消费”不是一件事。

## 7.7 attempt failure / crash

```text
provider-attempt-recovery ≠ crash-reconciliation
```

已确认失败与进程失忆不是同一个 failure domain。

## 7.8 local / external evidence

```text
repository-investigation ≠ external-investigation
```

本地 repository fact 与 public-web provenance 的 source law 不同。

---

# 8. 当前 DAG 状态

当前 boundary cards 的 `DEPENDS ON` 已做机器检查：

```text
packages = 45
unknown dependency refs = []
dependency cycles = []
dependency edges = 87
```

Phase E 删 3 条 coupling edge（`structured-workflow`→`causal-wait`、`time-capability`→`causal-wait`、`guidance-delivery`→`provider-projection`）；`finality`→`participant-horizon` 经审计保留。

所以当前 hard dependency graph 无环。

但不要把 DAG 当完成证据。后续 reverse coverage 若发现一个 dependency 是因为 implementation import / test organization / presentation 才存在，应删。

本轮已经删过几类假依赖：

- `host-boundary -> verification-system`：proof governance 不应变 product semantic dependency；
- `semantic-trace -> participant-horizon`：trace 位于 horizon 之前；
- investigation -> `action-affordance`：action description 是 presentation contract，不是 evidence ontology prerequisite；
- `epistemic-reasoning` 不 hard-depend 某一种 acquisition package；它消费 evidence acquisition contract，但认识机不应绑定 repository/web 的某个具体渠道。

完整依赖骨架看 `INDEX.md`，不要在本文件维护第二份精确 adjacency source。

---

# 9. 当前 proof ownership 结论

读 `PROOF-MAP.md`。

核心原则：

```text
shared checker allowed
shared semantic assertion ownership forbidden
```

已确认必须拆的现有 proof：

## 9.1 `tests/unit/prompt/authority.test.mjs`

至少拆：

- `interaction-authority`：PhysicalUser≠Authority、Root/Continuation/Unknown origin、authority profile invariants；
- `dispatch-protocol`：claim lifecycle、PromptKey、no blind resend / acceptance semantics。

legacy bare-agent-name absence 要单独判断是 migration ratchet 还是 future requirement；不能默认永久保留。

## 9.2 `scripts/checks/semantic-anchors.mjs`

mechanism 可共享。

但一个大 catalog 目前同时装：

- Role cognition；
- office consequence；
- tool affordance；
- review craft；
- epistemic craft。

未来每个 semantic ID 要有 package owner。

## 9.3 `language-parity-gate.test.mjs`

当前混合：

- locale leaf / placeholder / invariant identifier → `provider-language`；
- office semantic meaning → `office-capability`；
- tool action meaning → `action-affordance`；
- domain cognition → respective semantic owner。

## 9.4 `projection-algebra.test.mjs`

- intent order / merge / conflict / permutation independence / deterministic render → `provider-projection`；
- Repair/Review/Companion/Strength intent 为什么合法 → respective owner。

Projection 只是组合器，不拥有所有通过它渲染的产品事实。

## 9.5 Provider leak gates

迁移期 blacklist 可继续作 ratchet。

未来 `participant-horizon` proof 应逐步转成 positive information-admission law，而不是永久不断累加：

```text
SessionId
AgentId
status
code
...
```

历史 token 名。

---

# 10. 已知 ORPHAN / DEFERRED / WATCH

详细看 `AUDIT.md`。

## 10.1 Fission / `intra-participant-parallelism` — DEFERRED

当前仓库已经有世界观：

> one identity may contain several presents

也已有 Manager `fission` tool surface。

但 `FissionTool.fs` 明确只是 MVP：合法请求当前恒 capacity refusal，真正 multi-lane runtime 未实现。

因此现在**不**因为工具名存在就造永久 package。

未来如果真正实现：

```text
same participant
same authority/responsibility
multiple independent presents
eventual coherent reunion
```

再用 independent-change test 决定是否立 `intra-participant-parallelism`。

## 10.2 Pair-programming guideline / NEEDHELP — WATCH

当前意义可被这些 owner 组合解释：

- enduring craft → `cognitive-environment`；
- office/action boundary → `office-capability` / `action-affordance`；
- wire injection → `provider-projection` / `prefix-stability`；
- consultation → `delegation`；
- same-run authority → `interaction-authority`。

只有在 reverse coverage 发现一个**无法由上述 guarantees 组合解释的独立 WHY**，才新增 `collaboration-guidance`。

不要先因为 HOST-013 是大功能就立包。

## 10.3 `runtime-resource-integrity` — open question

`distribution` 与 `provider-language` 都会碰 runtime resources。

还没证明是否需要一个独立包来拥有：

> runtime 必需 semantic resources 在安装 artifact 中可解析、不可静默缺失

目前先不拆。要看全仓 resource loader / packaging / localization failure 是否形成独立 failure meaning。

---

# 11. 已吸收到其它包、不应该单独立包的旧主题

## 11.1 Companion / Blogger topology

当前“每个 Work Session 一个 leaf Companion Y”是重要现行 shape，但未通过 standalone WHY test。

其长期 guarantees 已分散给：

- topology → `session-ontology`；
- lifecycle → `managed-session-lifecycle`；
- history capture → `semantic-trace`；
- summary/compression → `context-compression`；
- bounded cross-boundary statement → `work-record`；
- diagnosis → `behavior-diagnosis`；
- redelivery/coverage → `guidance-delivery`。

如果未来 deterministic in-process summarizer 可替代 physical Blogger leaf 而这些 WHAT 都不变，则 `companion` 显然不是永久 ontology。

## 11.2 Synthetic TOML

TOML comment/field、escaping、value tree 是 representation HOW。

长期事实分别由：

- instruction/data boundary → horizon/projection；
- identifier 不翻译 → provider-language；
- representation 不反解 authority → interaction-authority/provider-projection。

不设 `synthetic-toml` package。

## 11.3 Agent catalog

不设 `agent-catalog` package。

拆为：

- Role/Persona/Binding → participant identity；
- office consequence → office capability；
- schema/runtime gate → capability enforcement；
-具体 Browser/Sphinx/repository behavior → respective domain package。

exact agent list 是当前 implementation vocabulary，不应自动成为永久 requirement。

## 11.4 MCP

MCP 是 Host integration mechanism，不是产品 ontology。

Browser / Sphinx / Semble 的产品语义分别落在 external investigation / epistemic reasoning / repository investigation 或 optimization contracts。

---

# 12. 当前 GARBAGE / HOW 清单

以下内容**不得因为旧 Clause/test 存在就直接搬进未来 WHAT**：

- “必须恰好 22 个 agent”；
- `fast-*` / `deep-*` 当前 machine names；
- Student/Teacher/Meditator/Executor 等历史 absence ratchet；
- exact Persona display names，除非重新证明名称本身是 public contract；
- OpenCode hook 名、callback shape、F# module/file path；
- MCP repo URL/ref、`uvx` command、test fixture env vars；
- Semble `MaxKeywords=8` / `TopK=4` / `64 KiB` 等 tuning values；
- Prompt recovery `TailWindow=50` / `Budget=3` 等具体常量；
- Context `200 KiB`，除非重新证明上界本身是产品合同；
- current `ProjectionSnapshot` 字段集合；
- current ProjectionIntent case 名；
- Synthetic TOML quote/escape strategy；
- migration double-renderer absence；
- `SuppressTransportOnly` 某历史 Change 的 deferred wiring；
- current Fission MVP capacity refusal 作为未来 parallelism 定义；
- `resources/prompts`、`catalog.json` 等旧路径“必须永远不存在”的 absence tests。

原则：

> clean world 应正面定义什么成立，而不是永远背历史墓碑清单。

Git/PR/history 保存历史，未来 Requirement Package 不负责模拟考古层。

---

# 13. 当前 docs 主题最明显的混合 ownership

做 reverse audit 时这些是第一批高收益拆解点。

## 13.1 `docs/what/prompt.md`

至少混合：

- `interaction-authority`；
- `dispatch-protocol`；
- `participant-identity`；
- `cognitive-environment`；
- `provider-language`；
- `action-affordance`；
- Todo/Finality runtime guidance。

绝对不要整体迁成 `prompt` package。

## 13.2 `docs/what/agent.md`

至少混合：

- identity/catalog；
- office capability；
- capability enforcement；
- delegation；
- repository investigation；
- external investigation；
- epistemic reasoning integration；
- warm-start optimization。

绝对不要整体迁成 `agent` package。

## 13.3 `docs/what/architecture.md`

- ARCH-014 Provider Horizon → `participant-horizon`；
- ARCH-017 Office Capability → `office-capability`；
- ARCH-016 static gates → 各 semantic owner proof + `verification-system` mechanism。

不要再造“Architecture 包统治所有横切事实”。

## 13.4 `docs/what/host.md`

当前至少混合：

- Host capability assumptions；
- session ontology/lifecycle；
- ProviderLanguage；
- pair guideline projection；
- Magic Todo membrane；
- provider/tool physical identity；
- compaction/reanchor；
- assistance。

只把真正 Host adapter guarantee 留给 `host-boundary`。

## 13.5 `docs/what/companion.md`

里面的 XTrace / WorkRecord / coverage / Opening / compression 需要继续按：

```text
semantic-trace
work-record
context-compression
prefix-stability
```

拆 owner。

## 13.6 `docs/what/review.md` + `docs/what/todo.md` + `docs/what/glory.md`

这三组互相交织：

```text
obligation-ledger
review-judgement
review-assurance
work-record
finality
provider projection / horizon
```

后续 reverse coverage 要按 proposition 归属，不按文件归属。

---

# 14. 下一步：不要立刻创建正式 `requirements/`

当前正确的下一阶段是**全仓 reverse coverage**。

目标不是再“想一些包”，而是证明当前 45 包能否覆盖真实语义，并发现：

```text
ORPHAN  = 有长期产品命题，没有 package owner
OVERLAP = 一个命题被多个 package 声称拥有
GARBAGE = 旧 Clause/test/change 只是 migration/HOW/compatibility，不应进入未来 WHAT
```

## Phase A — 建 Clause → owner 反向覆盖表

建议新增设计期文件：

```text
requirements-design/COVERAGE.md
```

逐 `docs/what/*.md` clause 记录：

```text
Clause / proposition
Current topic
Future owner
Classification = OWNED | HOW | GARBAGE | ORPHAN | NEEDS-SPLIT
Evidence notes
```

注意：**按 proposition，不按整 Clause 文件粗暴搬家**。一个 Clause 若实际有两个 WHY，可以标 `NEEDS-SPLIT`。

优先顺序：

1. `prompt.md`
2. `agent.md`
3. `host.md`
4. `companion.md`
5. `execution.md`
6. `persist.md`
7. `context.md`
8. `review.md`
9. `todo.md`
10. `glory.md`
11. `enforcer.md`
12. `casebook.md`
13. `strength.md`
14. `sphinx.md`
15. `js-tools.md`
16. 其余 architecture/flow/loop/orchestrator 等

这是高风险顺序，不是 normative dependency 顺序。

## Phase B — WHY 反审计

对每个 future package 回到 `docs/why/*.md` + completed changes：

- 是否真的只有一个 WHY？
- 有没有另一个完全不同 failure meaning 被塞进来？
- 当前 DOES NOT OWN 是否足够硬？
- 是否只是一个当前 mechanism 被误认为需求？

若发现 double-WHY：立即拆卡，不要为了稳定 45 而忍。

## Phase C — Source / runtime evidence

对每个 package 找：

- canonical domain types / pure decisions；
- application wiring；
- Host boundary；
- resources；
- durable facts；
- failure paths。

这里的目标不是设计 production module tree，而是验证 package WHAT 不是纯文档幻想。

## Phase D — Test / gate reverse coverage

以 `PROOF-MAP.md` 为起点，逐 test family 标：

```text
KEEP under package X
SPLIT into X/Y
MECHANISM shared checker
DELETE migration-only proof
ORPHAN assertion
```

任何旧 test 都没有“必须活到未来”的特权。

## Phase E — 再跑 dependency audit

每次新增/拆包后：

- 检查所有 `DEPENDS ON` 引用存在；
- 检查 DAG cycle；
- 对每条依赖问“这是 semantic prerequisite，还是 implementation/presentation/proof coupling？”

后者删除。

## Phase F — 设计正式 cutover

只有 A–E 足够闭环后，才设计真正的：

```text
requirements/<package>/
  PACKAGE.toml
  WHY.md
  WHAT.md
  HOW.md
  tests/
```

名字/结构仍可再审；不要把这里当已批准 schema。

然后才决定一次性迁移：

- 旧 `docs/` authority 如何退休；
- 根 `AGENTS.md` 如何改为 Requirement Package workflow；
- tests/gates 如何变成 package proof；
- changes/history 如何只保留历史而不继续产生 supra-package authority；
- verifier 如何检查 unique owner + dependency closure + package-local proof。

---

# 15. Reverse coverage 每轮应产出什么

为了避免新对话又陷进“读很多但没有收敛”，每轮固定产出：

## 15.1 Boundary delta

```text
UNCHANGED packages
SPLIT packages
MERGED packages
NEW packages
REMOVED packages
```

任何数量变化都允许。

## 15.2 Coverage delta

```text
new OWNED propositions
new ORPHAN propositions
new OVERLAP
new GARBAGE/HOW
```

## 15.3 Proof delta

```text
proofs with clear future owner
mixed tests needing split
migration-only proofs to delete
missing behavioral oracle
```

## 15.4 Dependency delta

新增/删除 hard edge，并写一句 WHY。

如果一轮只“读完了很多文件”，但这四类 delta 全空，要怀疑是否只是机械浏览。

---

# 16. 什么时候拆包

出现任一信号就做 split test：

- 一个包有两个独立 failure meaning；
- 一部分可以整体换算法/实现/产品策略，而另一部分 WHAT 不动；
- 一个包同时拥有“事实是什么”和“如何呈现事实”；
- 一个包同时拥有 authority 与 enforcement；
- 一个包同时拥有判断语义与判断可信建立；
- 一个包开始收容多个不同 evidence source law；
- package 的名字只能用“system / architecture / protocol / guidance / recovery”这种宽词解释，而一句 WHY 写不窄。

不要因为拆完文件多而犹豫。

交接原则明确偏向：

> 宁可稍小、WHY 清晰的 package，也不要 broad garbage bucket。

---

# 17. 什么时候并包

仅当：

- 两个包所谓 WHY 实际是同一句话的不同措辞；
- 任一重大变化都必然同时改变另一包的规范命题；
- 一个包没有独立 RED meaning；
- 一个包只是另一个包的 presentation / type alias / implementation stage。

“代码在同一个 module”不是并包证据。

“测试目前在同一个文件”不是并包证据。

---

# 18. 当前几个特别容易误判的事实

## 18.1 `ProviderLanguage` 不是 Horizon

Horizon 决定**什么**可看。

Language 决定这些 natural-language material 在一个 life 中以**哪种语言世界**出现。

## 18.2 Tool description 不是 capability owner

Action affordance 可以重复说明 Coder/Inspector distinction，但 canonical office authority 仍只在 `office-capability`。

## 18.3 Tool permission matrix 不是 office ontology

权限是 enforcement / current implementation projection。

Office 由 consequence 定义。

## 18.4 `AttemptExecutionProfile` 不是一个未来 package

它是当前把多个 invariant 原子化的 integration structure，可能同时承载 identity、authority、capability、request-kind、projection choice。

未来 requirement 应拥有 propositions，不拥有这个 record layout。

## 18.5 WorkRecord 不是 fixed report schema

当前强证据：

```text
Opening? / Chronicle / Recent work
formal statement = Recent work 最后一条 assistant prose
no Closing report DTO
```

未来长期 requirement 是 bounded canonical work statement / honesty / coverage/provenance；标题与 renderer 是否永久保持，需要继续区分 WHAT/HOW。

## 18.6 Strength Candidate 未 Promote ≠ history

这是 `speculative-investigation` 与 `semantic-trace` 的 cross-boundary invariant。

不要因为 candidate 会经过 projection 就让 projection 拥有它的因果合法性。

## 18.7 Review PERFECT 不是 Finality

`review-judgement` 决定一个 judgement；`review-assurance` 决定能否消费；`finality` 决定 mission 是否不可逆结束。

Acceptance ≠ rest。

## 18.8 Infrastructure failure 不是 semantic REVISE

基础设施故障不能伪装成工作需要修改。这是 review/todo/finality 当前历史里非常重要的 failure-domain separation，reverse coverage 时要找正确 owner，不要丢。

---

# 19. 新对话开始后的推荐第一批命令

使用 DevSpace；不要用 shell 改文件。

概念流程：

```text
open_workspace("/home/kunweiz/Desktop/vibe/wanxiangshu/", checkout)
read AGENTS.md
git status --short --branch
read requirements-design/HANDOFF.md
read requirements-design/README.md
read requirements-design/INDEX.md
read requirements-design/AUDIT.md
read requirements-design/PROOF-MAP.md
```

然后开始 coverage：

```text
rg '^## [A-Z]+-[0-9]+' docs/what/prompt.md docs/what/agent.md docs/what/host.md
```

对命中的 Clause 用 `read` 看全文，不要只靠 rg 行摘要归属。

Source evidence discovery 可用：

```text
rg 'PromptAuthority|SessionPersona|ToolCapabilitySet|ProjectionIntent|ProviderLanguage' src tests scripts
```

但最终归属必须回到命题，不是 symbol 名。

---

# 20. Repository 操作纪律

继续遵守根 `AGENTS.md`。

尤其：

- 修改前看 `git status` / diff；
- 保留用户无关改动；
- `changes/proposed/` 由用户管理，默认不创建/修改/移动；
- 只有用户明确要求启动指定 Proposal 才进入 `changes/active/`；
- 精准编辑，不用自动脚本批量改代码；
- shell 只做 read-only discovery、tests、build、git inspection；
- 文件修改用 DevSpace `edit` / `write`；
- 自动 commit；
- 不 push，除非用户明确要求；
- 不 force push / rewrite shared history。

当前 Requirement Package 工作仍是设计期，所以不要擅自按旧 Proposal lifecycle 为它制造 Proposal/Status 文件。

---

# 21. 当前设计自检

本轮已做过机器检查：

```text
index_packages=45
unique=45
card_blocks=45
missing_cards=[]
extra_cards=[]
boundary_failures=[]
unknown_dependency_refs=[]
dependency_cycles=[]
dependency_edges=87
DESIGN_CHECK: OK
```

其中 boundary schema 检查要求每张卡都有：

```text
WHY
OWNS
DOES NOT OWN
DEPENDS ON
PROVIDES
FAILURE MEANING
INDEPENDENT CHANGE
CURRENT EVIDENCE
```

下次修改 package set 后要重新做等价检查。

不要把“DAG 无环”误认为 ontology 已完成；它只说明当前依赖表没有机械环。

---

# 22. 当前未做的事情

截至本交接：

- 没有创建正式 `requirements/` normative tree；
- 没有迁移/删除现行 `docs/`；
- 没有修改 source code；
- 没有重写现有 tests/gates；
- ~~没有完成 Clause-by-Clause 全仓 reverse coverage~~ —— 已完成（Phase A），结果见 `requirements-design/COVERAGE.md`：~418 条款、0 新包、0 ORPHAN、45 包不需增删并；
- ~~没有完成 WHY 反审计~~ —— 已完成（Phase B），见 `requirements-design/COVERAGE.md` Phase B 节：45 包单-WHY 全通过、0 double-WHY、0 假边界；修复 1 处 OVERLAP（`repository-programming` 不再重复拥有 capability 同构律，新增 `capability-enforcement` edge）+ 删除 1 处假依赖（`finality → managed-session-lifecycle`）；4 处弱依赖转入 Phase E；
- ~~没有完成 Source/runtime evidence~~ —— 已完成（Phase C），见 `requirements-design/EVIDENCE.md`：43 REAL + 2 META、0 THIN、0 FANTASY；无文档幻想包；
- ~~没有完成 Test/gate reverse coverage~~ —— 已完成（Phase D），见 `requirements-design/PROOF-MAP.md` Phase D 节：24 gates + 35 test families 逐项标 KEEP/SPLIT/MECHANISM/DELETE；family 级 0 ORPHAN、3 missing oracle 待补；
- ~~没有完成 changes/ 逆向~~ —— 已完成，见 `requirements-design/CHANGES-AUDIT.md`：36 份 completed 全部命中（27 EVIDENCE + 5 GARBAGE-mixed + 3 GARBAGE-pure + 1 HOW）、0 份升级为 authority、无新 ORPHAN；
- ~~没有完成 dependency audit~~ —— 已完成（Phase E）：删 3 条 coupling edge（`structured-workflow`/`time-capability`→`causal-wait`、`guidance-delivery`→`provider-projection`），保留 `finality`→`participant-horizon`；INDEX 骨架重画为完整邻接清单，90→87 edges、0 cycle、0 unknown ref；
- 没有最终确定 45 是最终数量；
- 没有决定正式 package manifest schema；
- 没有执行 normative cutover；
- 没有 push。

所以新对话的任务不是“开始实现 45 包”，而是继续**证明/修正 ontology**。

---

# 23. 设计阶段 Definition of Done

进入正式 migration 前，至少满足：

1. 所有 `docs/what` normative propositions 已 reverse-classify：OWNED / HOW / GARBAGE；无未解释 ORPHAN。
2. 每个 future package 仍只有一个可清楚表达的 WHY。
3. 每个 future package 有明确 DOES NOT OWN。
4. hard dependency refs 完整，DAG 无无法解释的 cycle。
5. 当前重要 source behavior 能映射到 package guarantees，不存在“文档幻想包”。
6. 所有现有 semantic tests/gates 已有：future owner / split plan / delete plan。
7. 无 test assertion 需要永久双 owner。
8. migration-only absence/compatibility ratchets 已明确哪些退休。
9. Fission、runtime resource integrity、Pair/NEEDHELP 等 WATCH 项已得到明确 verdict。
10. 可以画出正式 cutover 后“只有 Requirement Packages 拥有 normative authority”的仓库结构，而无需 supra-package architecture/governance/testing 文档。

---

# 24. 最终 migration 的目标形态

方向性目标，不是当前已批准文件 schema：

```text
requirements/
  <package>/
    PACKAGE.toml
    WHY.md
    WHAT.md
    HOW.md
    tests/
```

核心约束：

- `WHAT.md` 是 package 的唯一 normative semantic contract；
- WHY 解释不可替代原因；
- HOW 解释实现模型/约束，但不另造 normative owner；
- tests 是 package-local executable proof；
- dependency 表示 consume guarantee，不表示 precedence；
- 所有 accepted packages 同时为真；
- 不再存在一个 supra-package `architecture.md` / `security.md` / `testing.md` 可以定义横跨所有包的产品事实；
- 横切 invariant 也必须有自己的 semantic package owner，或归入真正拥有该 proposition 的包。

是否使用恰好这些文件名、TOML schema，仍要在 cutover 设计时确认。

---

# 25. 最容易把工作做坏的方式

新对话务必避免：

1. **重新数包。** 45 不是 KPI。
2. **按当前文件名一对一迁移。** `prompt/agent/host/architecture` 都是明显混合域。
3. **把所有 security/static gate 收成 Security package。** 安全 invariant 应归真正 semantic owner。
4. **把所有恢复收回 Recovery package。** 已明确 attempt failure 与 crash reconciliation 不同。
5. **让 Prompt/Role Law 拥有它描述的全部事实。** presentation ≠ semantic ownership。
6. **让 Projection 拥有所有经过 renderer 的 feature semantics。** renderer ≠ authority。
7. **保所有旧 test。** migration ratchet 可退休。
8. **为兼容历史造永久需求。** Git 记历史，未来 WHAT 记当前真理。
9. **因为 production module 没拆就不拆 requirement。** 两种架构不是一回事。
10. **过早创建正式 `requirements/`。** 先完成 reverse coverage。
11. **因为已有 tool/component 名就立包。** Fission/Sphinx/SyntheticToml 已给出反例。
12. **用 dependency 表达优先级。** dependency 只表达 guarantee consumption。
13. **用 test 文件当 owner。** assertion 才有 owner。
14. **把 exact constants / paths / enum names 当 ontology。** 先做 WHY test。

---

# 26. 给下一位 Agent 的最短行动指令

如果只保留这一段：

```text
1. 打开真实 checkout，不用 Repomix。
2. 读 AGENTS.md + requirements-design/{HANDOFF,README,INDEX,AUDIT,PROOF-MAP}.md。
3. 不创建正式 requirements/，不改现行 docs authority。
4. 从 docs/what/prompt.md 开始 Clause-by-Clause reverse coverage。
5. 每个 proposition 判 OWNED / HOW / GARBAGE / ORPHAN / NEEDS-SPLIT。
6. owner 只能依据 WHY + independent-change + failure meaning，不依据文件/module/test 共址。
7. 同步投影 current tests/gates 到 unique future proof owner；migration-only proof 标 delete。
8. 发现 double-WHY 就拆，发现假边界就并，发现 orphan 就新增；不要维护 45。
9. 每轮更新 AUDIT / INDEX / boundary cards / future COVERAGE，重新检查 dependency refs + cycles。
10. ontology + proof ownership 全仓闭环后，再设计一次性 normative cutover。
```

---

# 27. 可直接粘贴给新对话的启动 Prompt

```text
请继续万象术 Requirement Packages 重构。

真实仓库：/home/kunweiz/Desktop/vibe/wanxiangshu/
不要使用 Repomix 作为事实源，请直接通过 DevSpace 阅读 checkout。

先完整阅读：
- AGENTS.md
- requirements-design/HANDOFF.md
- requirements-design/README.md
- requirements-design/INDEX.md
- requirements-design/AUDIT.md
- requirements-design/PROOF-MAP.md

当前 requirements-design 只是未来 ontology 设计，不是现行 normative authority；当前正式语义仍在 docs/。
当前有 45 个候选包，但数量不是目标，可以继续拆/并/增/删。

继续工作的第一目标是全仓 reverse coverage，不要立刻创建正式 requirements/：
从 docs/what/prompt.md 开始，逐 proposition 判 future owner / HOW / GARBAGE / ORPHAN / NEEDS-SPLIT；同时把 tests/gates 投影到唯一 future proof owner。

严格使用 WHY、DOES NOT OWN、failure meaning、independent-change test 决定边界；不要按当前文件/module/test 的共址决定 package。

每轮把发现更新回 requirements-design，并保持 dependency graph 可解释且无假依赖。
```

---

# 28. 最后的设计提醒

真正要问的永远不是：

> 当前代码把什么实现到一起？

而是：

> 哪些命题有不同 WHY？
> 哪些可以独立重大变化？
> 哪些失败意味着不同的世界破坏？
> 哪些应该有不同 semantic owner？

宁可多一个边界清楚的小包，也不要一个“什么都管一点”的大包。

只有在全仓反向覆盖后仍能同时成立的那些包，才值得进入未来 normative world。
