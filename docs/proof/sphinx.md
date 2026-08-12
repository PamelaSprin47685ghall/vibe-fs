# Sphinx — 证明

行为：`what/sphinx.md`。边界：`shape/sphinx.md`。算法：`how/sphinx.md`。Host：AGENT-030。

## Kernel / co-yield

| 证明 | 期望 | 条款 |
|---|---|---|
| start 首步 | `yield + SemanticAssessmentRequest` | SPHINX-001、003 |
| RootContract 不坍缩 | Why/How 混合输入同时保留 Explanation/Plan 质量 | SPHINX-007 |
| control ≠ evidence | SemanticAssessment / Candidates 后 Evidence、Findings 仍为空 | SPHINX-006 |
| candidate 必调查 | Candidates 后 Kernel 选 `InvestigateRequest`，不把候选当答案 | SPHINX-006、007 |
| pending request gate | 错 Observation type / action id → error；Revision 不变 | SPHINX-003 |
| fixed point | `close(close(S)) = close(S)` | SPHINX-004 |
| gateway value | low immediate gain + high GatewayGain 仍可被选中 | SPHINX-007 |
| control belief 可更新 | Investigation semanticAssessment 改 RootContract，同时 Evidence/Findings 仍不凭空增长 | SPHINX-006、007 |
| generator 再生长 | Investigation 后先重新 `GenerateCandidatesRequest`，不是直接把开局 frontier 当完整搜索空间 | SPHINX-001、010 |
| synthesis 守恒 | Synthesis 前后 Evidence.Count 不变 | SPHINX-006 |
| ungrounded finding | 保留 claim，但 answer 显式 `ungrounded-finding:<key>`；wire confidence 不进入权威状态 | SPHINX-004、006 |

代表测试：`tests/unit/sphinx/kernel.test.mjs`、`tests/unit/sphinx/semantics.test.mjs`。

## Handle / MCP

| 证明 | 期望 | 条款 |
|---|---|---|
| opaque handle | UUID 形态；同 handle 续作 | SPHINX-002 |
| missing / unknown | error；不建隐式会话 | SPHINX-002 |
| full co-yield | start → assess → candidates → investigate → synthesize → answered | SPHINX-001..004 |
| Canonical basis | answer 分列 finding/evidence；synthesis 只引用 finding key | SPHINX-004、006 |
| MCP surface | `_registeredTools` 恰好 `start`,`resume` | SPHINX-003 |

代表测试：`tests/unit/sphinx/mcp-handle.test.mjs`。

## Bayesian qualification / dependency

| 证明 | 期望 | 条款 |
|---|---|---|
| qualification gate | `numericQualified=false` 即使带 likelihood 也不产生 Bayesian | SPHINX-008 |
| qualified posterior | 0.5/0.5 prior × 0.7/0.3 likelihood → 0.7/0.3 posterior | SPHINX-008、009 |
| dependency conservation | 同 DependencyKey 两条 likelihood 不重复相乘 | SPHINX-006、008 |
| independent same-semantic evidence | 同 semantic key、不同 dependency group 的 Evidence 同时保留并可独立更新 posterior | SPHINX-006、008 |
| qualification before grouping | 同依赖组不合格记录不得遮住后续合格 likelihood | SPHINX-008 |

代表测试：`tests/unit/sphinx/bayes.test.mjs`。

## Strict graph A*

| 证明 | 期望 | 条款 |
|---|---|---|
| g+h | 固定 graph 得到最短路径 | SPHINX-009 |
| reopen | admissible-but-inconsistent heuristic 下更低 g 重开 closed node | SPHINX-009 |
| nonnegative precondition | negative edge → reject | SPHINX-009 |

代表测试：`tests/unit/sphinx/search.test.mjs`。

## Graph-MCTS

| 证明 | 期望 | 条款 |
|---|---|---|
| selection/rollout/backup | 多轮后高 terminal reward branch 胜出 | SPHINX-009 |
| transposition | 两 parent 指向同 semantic key，共享 visit stats | SPHINX-009 |
| unvisited UCT | unvisited node = +∞ exploration | SPHINX-009 |

代表测试：`tests/unit/sphinx/mcts.test.mjs`。

## Representation equivalence

| 证明 | 期望 | 条款 |
|---|---|---|
| wire 无等价权 | Candidate 发送 `equivalenceKey` 不能触发 merge；CognitiveAction 内部 EquivalenceKey 仍为空 | SPHINX-010 |
| kernel dominance | Kernel 已确定同一等价类后，一方逐维支配 → 淘汰弱代表 | SPHINX-010 |
| provenance conservation | 同 semantic+dependency Candidate 多方法命中 → 一个动作，但 provenance 并集保留 | SPHINX-010 |
| dependency separation | 同 semantic question + 不同 dependency → 两动作均保留 | SPHINX-010 |
| Pareto frontier | 高收益高成本 vs 低收益低成本不可比较 → 两者均保留 | SPHINX-010 |

代表测试：`tests/unit/sphinx/represent.test.mjs`。

## Methodology

| 证明 | 期望 | 条款 |
|---|---|---|
| 核心五方法 | Phase-0 core names 保持五个 | SPHINX-010 |
| 扩展库 | CausalMechanism / BaseRate / Falsification / SourceTriangulation / OntologyRepair 等存在 | SPHINX-010 |
| 多方法激活 | Why+causal/explanatory 同时触发多个 generator | SPHINX-007、010 |
| predictive Polar | BaseRate + Falsification + Counterexample 可同时激活 | SPHINX-007、010 |

代表测试：`tests/unit/sphinx/methodology.test.mjs`。

## F# / Host 边界

| 证明 | 期望 | 条款 |
|---|---|---|
| Fable source | Sphinx 生产源只在 `src/Wanxiangshu/Sphinx/*.fs` | SPHINX-005 |
| production entry | `dist/Sphinx/McpServer.js` | SPHINX-005、AGENT-030 |
| build | `scripts/build.mjs` 不 copy `src/sphinx` | SPHINX-005 |
| Host config | `config.mcp.sphinx.command = node <packageRoot>/dist/Sphinx/McpServer.js` | AGENT-030 |
| permission | 仅 Inquiry allow `sphinx_*` | AGENT-006、AGENT-030 |
| registry | 不进 ToolRegistry / `js-*` | SPHINX-005、AGENT-030 |
| SDK boundary | MCP SDK / zod 仅 Sphinx McpServer；Semble 仍受 AGENT-027 | SPHINX-005 |

代表测试：`tests/unit/agent/sphinx-mcp.test.mjs`、`tests/unit/agent/inquiry-permissions.test.mjs`、plugin/integration permission contract。

## 标准门禁

```text
npm run build
node --test tests/unit/sphinx/*.test.mjs
node scripts/checks/spec.mjs
npm run format:check
npm test
npm run test:integration
```

完成声明以标准门禁实际结果为准；一次性脚本或手工 transcript 不构成 proof。
