# Changes reverse audit

把 `docs` 标准（Phase A 的 OWNED / HOW / GARBAGE / ORPHAN）同样施于 `changes/`：
逐份 completed change 判定它确立了哪条 durable WHY、归哪个 future package、还是只是历史沉积。

## 原则（HANDOFF §1.3 / §12）

```text
historical clean-break assertion ≠ permanent product requirement
current implementation shape       ≠ ontology
change 文件是历史证据，不是当前产品规范，绝不因存在而升级为 authority。
```

判据：一份 completed change 只可能是

```text
EVIDENCE → owner(s)   = 确立了某条 durable WHY，该 WHY 现由 owner 拥有（喂 WHY / CURRENT EVIDENCE）
GARBAGE               = 迁移/clean-break 沉积：已删领域、absence ratchet、ChatGPT/review transcript
HOW                   = 机制/实施记录，无 normative 内容
```

## Completed（36 份）

| Change | 确立的 WHY | Future owner | Disposition |
|---|---|---|---|
| `storage.md` | 单一 durable substrate；event=truth；append-only；CAS；no LWW | `durable-events` + `durable-convergence` + `effect-accounting` | EVIDENCE |
| `test.md`（G4R One World Pure Time） | 可失败可重放的纯时序证明体系 | `verification-system` | EVIDENCE |
| `rabbit.md`（G4R-CE Vocabulary） | 业务流程由语义语言结构表达，无第二 runtime | `structured-workflow` | EVIDENCE |
| `magic-todo.md` | 义务账本 + lag-1 过程评审 + 终末 2N | `obligation-ledger` + `review-assurance` + `finality` | EVIDENCE |
| `rulebook.md` | 诊断成立 vs 何时/如何再次告知分离 | `behavior-diagnosis` + `guidance-delivery` | EVIDENCE |
| `enforcer.md` | Blogger as Enforcer：Detection/Remediation 双翼 | `behavior-diagnosis` + `guidance-delivery` | EVIDENCE |
| `perm-inspector.md` | 旧知识 best-effort cache，不冒充当前证明 | `knowledge-reuse` + `repository-investigation` | EVIDENCE |
| `strength.md` | 零影响 speculation 才可换成本收益 | `speculative-investigation` | EVIDENCE |
| `Sphinx.md` | 生成不增知识；proposal≠evidence | `epistemic-reasoning` | EVIDENCE |
| `js-capability-projected-tools.md` | 单一 capability-projected 编程面 | `repository-programming` + `capability-enforcement` | EVIDENCE |
| `js-tools-toml-result.md` | 结果值树进 SyntheticToml，非 JSON 字符串叠信封 | `repository-programming` + `provider-projection` | EVIDENCE |
| `PromptRestoration.md` | 一个 life 一个稳定语言世界；prose 属认知环境 | `provider-language` + `cognitive-environment` | EVIDENCE |
| `cursor-pair-hint.md` | canonical Hint 单语义 + provider-specific wire 分离 | `provider-projection` + `prefix-stability` | EVIDENCE |
| `pair-parallel-tools.md` | Pair Hint 并行 wave craft | `cognitive-environment` | EVIDENCE |
| `increase-strength.md` | assistance escalation ≠ fallback failure | `interaction-authority` + `delegation`（NEEDHELP WATCH） | EVIDENCE |
| `repository-warm-start.md` | warm-start 低信任 hint，不伪造 read/history | `repository-investigation` + `knowledge-reuse` | EVIDENCE |
| `cache.md` | HOST-013 anchored prefix + idle-only auto-continue 资格 | `prefix-stability` + `host-boundary` + `interaction-authority` | EVIDENCE |
| `corrective.md` | join 唤醒≠HumanRoot；TOML 分类由消费语义决定 | `interaction-authority` + `provider-projection` + `delegation` | EVIDENCE |
| `ce-temporal-ownership.md` | 时序所有权交回 CE，无程序计数器 | `structured-workflow` | EVIDENCE |
| `fsharp-dsl-governance.md` | mutable record 状态乘积识别（补 gate 盲区） | `structured-workflow` | EVIDENCE |
| `dsl-structured-program-gap.md` | DSL 结构化程序缺口闭环 | `structured-workflow` | EVIDENCE |
| `projection-algebra-gap.md` | 投影代数闭环 | `provider-projection` | EVIDENCE |
| `reconciler-event-driven-de-polling.md` | 事件驱动取代轮询（未裁决候选） | `causal-wait` + `host-boundary` | EVIDENCE |
| `waitfact-causal-renewal.md` | 续期因果归因，非任意 append | `causal-wait` + `verification-system` | EVIDENCE |
| `causal-ce-observability.md` | wait observation 可看程序，程序不可看 observation | `causal-wait` | EVIDENCE |
| `canary-unbend.md` | canary 不可弯曲迎合生产 | `verification-system` | EVIDENCE |
| `orchestrator-e2e-timeout.md` | 先可解释再修根因 | `verification-system` + `change-integration` | EVIDENCE |
| `universal.md` | 删除 Student/Teacher；SessionOwnership + ReuseScope | `session-ontology` + `delegation` + `knowledge-reuse` | GARBAGE（Student 删除）+ EVIDENCE |
| `ce-student-teacher-collapse.md` | Student/Teacher capability collapse | `delegation` + `session-ontology` | GARBAGE（collapse）+ EVIDENCE |
| `Student & Teacher.md` | SSOT/16（已删领域） | — | GARBAGE（被 universal.md 取代；absence ratchet） |
| `ChatGPT-时序控制流修复提案.md` | 时序控制流决策（raw export） | `structured-workflow` | GARBAGE（transcript）+ EVIDENCE |
| `refactor.md` | 按知识主权重新装箱（raw export） | `structured-workflow` | GARBAGE（transcript）+ EVIDENCE |
| `fix.md` | 验收口径不缩水、DSL gate 盲区（raw export） | `verification-system` | GARBAGE（transcript）+ EVIDENCE |
| `fix-revise.md` | REVISE follow-up 登记 | — | GARBAGE（review transcript） |
| `ce-revise-review.md` | CE 复审记录 | — | GARBAGE（review transcript） |
| `entry.md` | Cross-Proposal 实施 Playbook | — | HOW（实施手册，非产品语义） |

## Active（2 份，in-flight，非历史）

| Change | 说明 |
|---|---|
| `GrandRewrite.md` | provider world clean break：机器拓扑撤出 horizon、普通 completion 取代 `return`。语义已写入 docs/why（EXEC-026/031 等「GrandRewrite 后」），仍在实施中。 |
| `fork-attach.md` | `fork` 追加可选 `attach`，纯增量；未 Active 禁止实现。 |

## proposed/blockedForNow（3 份，用户管理，不逆向裁决）

| 文件 | 对应 future 状态 |
|---|---|
| `fission.md` | → `intra-participant-parallelism`（DEFERRED，HANDOFF §10.1） |
| `Steward.md` | → 尚未立包（orchestrator why 明言「Steward 不在本轮创建」） |
| `Sphinx-wiki.html` | → HOW 参考，`epistemic-reasoning` 的算法资料，非 ontology |

## 结论

- **36 份 completed 全部命中**：27 EVIDENCE + 5 GARBAGE-mixed（transcript / 已删领域）+ 3 GARBAGE-pure + 1 HOW。
- **0 份 changes 升级为 authority**：每份都正确自标「不是当前产品规范」；其确立的 durable WHY 已由 45 包之一拥有，无 ORPHAN。
- **高参考价值**：completed change 比 `docs/why` 更详细（被拒方案、迁移细节、proof plan、失败模式复盘），是 WHY 考古第一手参考；参考价值是考古价值，不改变「历史证据 ≠ 权威」边界（见 HANDOFF §1.3）。
- **GARBAGE 沉积确认**：Student/Teacher 删除（3 份）、ChatGPT/review transcript（5 份）、absence ratchet——与 Phase A 的 GARBAGE 清单一致，不进入永久 WHAT。
- **Active/blocked 不冒充历史**：GrandRewrite 语义已进 docs、fork-attach 增量未实现、fission/Steward 维持 DEFERRED/不立包——与 HANDOFF §10 的三个 WATCH/DEFERRED 项一一对应。

## Delta

```text
Boundary:   UNCHANGED 45（changes 未提供任何需要新包的独立 WHY）
Coverage:   0 新 ORPHAN / 0 新 OVERLAP
Proof:      0 新 owner 变化（各 change 的 proof 已在 Phase D 分类）
Dependency: 0 新增/删除 edge
```
