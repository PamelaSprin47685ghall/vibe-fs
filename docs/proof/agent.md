# Agent — 证明

行为：`what/agent.md`。边界：`shape/agent.md`。实现：`how/agent.md`。

## 启动与配置

| 证明 | 期望 | 条款 |
|------|------|------|
| 二十名 Agent 齐全 | 缺一启动失败；无 student/teacher pair | AGENT-002 |
| peer 对称 + model 非空互异 | 配置验证失败则 fail fast | AGENT-003 |
| 非法旧名（含 student/teacher） | 无 alias，拒绝 | AGENT-004 |
| Authority 省略 agent | `HostContractUnsupported` | AGENT-005 |

## 权限

| 证明 | 落点 / 形态 | 条款 |
|------|-------------|------|
| 双层 fail-closed | Host schema 无越权工具 + ToolRegistry 拒绝执行 | AGENT-007 |
| Role 未定 → 工具集空 | unit / integration 工具契约 | AGENT-007 |
| `external_directory=allow` | Host ruleset（flat merge + findLast）对任意外部 path 为 allow | AGENT-019 |
| 唯一写入点 | 仅 `StaticTools.permissionObj` / `applyOwnedFields` | AGENT-019 |
| fast/deep 权限相等 | 权限对象结构比较 | AGENT-010 |
| 内部 Agent 不可见 | enum/schema 不含 blogger/executor | AGENT-008 |
| Student/Teacher 生产零 | Role DU / catalog / permissions / tools 无 Student/Teacher；旧名 legacy reject | AGENT-002、AGENT-004、AGENT-020 |

## Meditator

| 证明 | 落点 / 形态 | 条款 |
|------|-------------|------|
| 工具面 = `inspector` + `sphinx_*` | Host schema + ToolRegistry 均无 read/glob/grep/write/edit/executor/coder/fork/join/list/PTY/`stealth-browser-mcp_*`；Meditator allow `sphinx_*` | AGENT-006、AGENT-025、AGENT-028 |
| SyncDelegate 边 | 仅 `Meditator → Inspector`；无反向 | AGENT-024 |
| Epistemic style 在 prompt | meditator system prompt 含形成理解 / 反例 / 证据vs推论 / 综合 Inspector；无 LearningState/QA/Compile/return 协议 | AGENT-025 |
| 无 Student workflow 移植 | 无 MeditatorLearn/Compile RequestKind；终端为普通 Assistant completion | AGENT-025 |

## 能力矩阵

| 证明 | 条款 |
|------|------|
| Manager 无普通工具；Orchestrator 只 fork manager | AGENT-011、AGENT-015 |
| mv/rm 仅 Coder；非空目录 rm 拒绝 | AGENT-016…018 |
| bash-honeypot 仅 Coder；调用不跑 shell | AGENT-023 |
| DevOps 独占 PTY；Reviewer 只读 | AGENT-013、AGENT-014 |
| Meditator = inspector + Sphinx MCP | AGENT-025、AGENT-028 |
| Browser stealth-browser MCP | AGENT-026 |
| 内部 Semble MCP | AGENT-027 |

## stealth-browser MCP

| 证明 | 期望 | 条款 |
|------|------|------|
| Host schema 键 | Browser allow `stealth-browser-mcp_*`；其它 role deny；无虚构 `network` | AGENT-006、AGENT-026 |
| wildcard 求值 | Browser 对 `stealth-browser-mcp_get_debug_view` = allow；Coder/Meditator = deny | AGENT-007、AGENT-026 |
| config 注入 | `configureFromHostConfig` 写入 `mcp.stealth-browser-mcp`；不删其它 MCP | AGENT-026 |
| 启动判定 | disabled / fixture / test / uvx ref 四分支确定性 | AGENT-026 |
| 不进 ToolRegistry / js-* | plugin `tool` 注册表无 stealth-browser 名；js-browser 仍仅 fs 投影 | AGENT-026、JS-001 |

## Sphinx MCP

| 证明 | 期望 | 条款 |
|------|------|------|
| Host schema 键 | Meditator allow `sphinx_*`；其它 role deny | AGENT-006、AGENT-028 |
| wildcard 求值 | Meditator 对 `sphinx_start` / `sphinx_resume` = allow；Coder/Browser = deny | AGENT-007、AGENT-028 |
| config 注入 | `configureFromHostConfig` 写入 `mcp.sphinx`；不删其它 MCP | AGENT-028 |
| 启动判定 | disabled / fixture / test / 生产 node 四分支确定性 | AGENT-028 |
| 不进 ToolRegistry / js-* | plugin `tool` 注册表无 sphinx 名 | AGENT-028、SPHINX-005 |
| 正交 | 万象术无 Closure 副本；Sphinx 无万象术 domain import | SPHINX-005 |

## 内部 Semble MCP

| 证明 | 期望 | 条款 |
|------|------|------|
| command / launch | `uvx --from "semble[mcp] @ git+…@{ref}" semble`；disabled / fixture / test / uvx 四分支 | AGENT-027 |
| parse | MCP text JSON → Hit list；缺 `file_path` skip；非法 JSON → `[]` | AGENT-027 |
| Disabled search | 不 spawn，返回 `[]` | AGENT-027 |
| Fixture search | stdio roundtrip 命中确定性 Hit，转发 query/repo/`top_k`/`max_snippet_lines` | AGENT-027 |
| 不进 Host mcp | `configureFromHostConfig` 后 `config.mcp.semble` 不存在；stealth 仍注入 | AGENT-027 |
| 不进 Strength / AGENT-006 | permission 无 `semble` / `semble_*` 键 | AGENT-027、STRENGTH-004 |

代表测试：`tests/unit/agent/catalog.test.mjs`、`tests/unit/plugin/agent-permission-gate.test.mjs`、
`tests/unit/agent/stealth-browser-mcp.test.mjs`、`tests/unit/agent/semble-mcp.test.mjs`、
`tests/unit/agent/sphinx-mcp.test.mjs`、`tests/unit/sphinx/*.test.mjs`、
`tests/integration/plugin/manager-tool-contract.test.mjs`、`file-mutation-tools.test.mjs`；
Meditator inspector + Sphinx / Student-Teacher absence ratchet 随落地。
