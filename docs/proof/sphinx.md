# Sphinx — 证明

行为：`what/sphinx.md`。边界：`shape/sphinx.md`。算法：`how/sphinx.md`。Host：AGENT-028。

## Phase 0 内核

| 证明 | 期望 | 条款 |
|------|------|------|
| start → handle | 返回必含 handle；无 handle 不可续作 | SPHINX-002、003 |
| 首步 SemanticAssessment | start 后首次非 error 为 yield + SemanticAssessmentRequest | SPHINX-003、004 |
| resume 缺/未知 handle | status=error；不创建隐式新会话 | SPHINX-002、003 |
| absorb → Closure → fixed point | 每次 observation 后达 fixed point 才再 yield/answer | SPHINX-001、004 |
| novelty / dominance | semantic-key 去重；simple dominance 可替换代表元 | SPHINX-004 |
| Stop as action | V_stop 最优 → answered；Canonical Answer 由 Kernel 写 | SPHINX-001、004 |
| 方法库 V1 | 仅 Multidisciplinary/Abduction/Analogy/Counterexample/Synthesis | SPHINX-004 |
| 无 A*/Bayes/MCTS 本体 | Phase 0 路径不依赖完整搜索/概率图/MCTS 模块 | SPHINX-004 |

## 正交与 Host

| 证明 | 期望 | 条款 |
|------|------|------|
| Sphinx 不依赖万象术 domain | `src/sphinx` 无万象术 domain import | SPHINX-005 |
| 万象术不内嵌闭包 | Host/Kernel 无 EpistemicState Closure 副本 | SPHINX-005、AGENT-028 |
| config 注入 | `configureFromHostConfig` 写入 `mcp.sphinx`；不删其它 MCP | AGENT-028 |
| 启动判定 | disabled / fixture / test / 生产 node 入口四分支确定性 | AGENT-028 |
| Meditator allow | Meditator allow `sphinx_*`；其它 managed role deny | AGENT-006、028 |
| 不进 ToolRegistry / js-* | plugin tool 注册表无 sphinx 名 | AGENT-028、SPHINX-005 |
| AGENT-027 不变 | Semble 路径仍禁止新增 MCP SDK；Sphinx 路径可用 | AGENT-027、SPHINX-005 |

代表测试（落地后）：`tests/unit/sphinx/*.test.mjs`（闭包、handle、start→resume→answer）、
`tests/unit/agent/sphinx-mcp.test.mjs`（注入 / disabled / fixture / Meditator allow）；
可选 fixture：`tests/unit/support/sphinx-mcp-fixture.js`。
门禁：`node scripts/checks/spec.mjs` 识别 `SPHINX-`。
