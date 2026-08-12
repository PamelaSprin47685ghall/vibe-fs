# Agent — 证明

行为：`what/agent.md`。边界：`shape/agent.md`。实现：`how/agent.md`。

## 启动与配置

| 证明 | 期望 | 条款 |
|------|------|------|
| 二十二名 Agent 齐全 | 缺一启动失败；含 `fast\|deep-bookkeeper`；无 meditator/executor/student/teacher pair | AGENT-002 |
| peer 对称 + model 非空互异 | 配置验证失败则 fail fast | AGENT-003 |
| 非法旧名（含 meditator/executor/student/teacher） | 无 alias，拒绝；不得映射到 Inquiry/Distiller/Inspector | AGENT-004 |
| Authority 省略 agent | `HostContractUnsupported` | AGENT-005 |
| SyncDelegate DAG 无环 | 仅 `Inquiry\|Coder\|DevOps → Inspector` 与 `DevOps → Coder`；启动静态证明 | AGENT-024 |
| PersonaCatalog resolve-once | `Role × initial tier → SessionPersona` 创建路径冻结；Fallback/Strength/Peer 不得重绑 | AGENT-028、AGENT-029 |

## 权限

| 证明 | 落点 / 形态 | 条款 |
|------|-------------|------|
| 双层 fail-closed | Host schema 无越权工具 + ToolRegistry 拒绝执行 | AGENT-007 |
| Role 未定 → 工具集空 | unit / integration 工具契约 | AGENT-007 |
| `external_directory=allow` | Host ruleset（flat merge + findLast）对任意外部 path 为 allow | AGENT-019 |
| 唯一写入点 | 仅 `StaticTools.permissionObj` / `applyOwnedFields` | AGENT-019 |
| fast/deep 权限相等 | 权限对象结构比较 | AGENT-010 |
| 内部 Agent 不可见 | enum/schema 不含 blogger/distiller/bookkeeper | AGENT-008 |
| 旧角色生产零 | Role DU / catalog / permissions / tools 无 Meditator/Executor/Student/Teacher；旧名 legacy reject | AGENT-002、AGENT-004、AGENT-020 |
| 旧工具名非法 | `fork-manager`/`list`/`inspector`(工具)/`verdict`/`blog`/`executor`(工具)/`fork-pty`/`edit-qa`/`return` 无 alias | AGENT-006、AGENT-007 |

## Persona / Binding

| 证明 | 期望 | 条款 |
|------|------|------|
| Persona 不可变 | session 创建后 Persona 字节稳定；Fallback 换模型不换 Persona | AGENT-028、AGENT-029 |
| Binding ≠ Persona | Strength/Fallback 只改 ExecutionBinding；provider 自称不含 `fast-*`/`deep-*` | AGENT-029 |
| Bookkeeper 独立 Persona | Clerk/Curator；机器身份 `fast\|deep-bookkeeper`；不进 public Role DU / fork 面 | AGENT-002、AGENT-028 |

## Inquiry

| 证明 | 落点 / 形态 | 条款 |
|------|-------------|------|
| 工具面 = `inspect` + `sphinx_*` | Host schema + ToolRegistry 均无 read/glob/grep/write/edit/run/fork/commission/join/horizon/终端/`stealth-browser-mcp_*`；Inquiry allow `sphinx_*` | AGENT-006、AGENT-025、AGENT-030 |
| SyncDelegate 边 | 仅 `Inquiry → Inspector`；无反向；无独立 `return` | AGENT-024 |
| Epistemic style 在 prompt | inquiry system prompt 含形成理解 / 反例 / 证据vs推论 / 综合 Inspector；无 LearningState/QA/Compile/return 协议 | AGENT-025 |
| 无 Student workflow 移植 | 无 MeditatorLearn/Compile RequestKind；终端为普通 Assistant completion | AGENT-025 |
| SyncDelegate 无 return | `InvocationMode=SynchronousDelegate` → ordinary completion → bounded WorkRecord；无 Returned 通道 | AGENT-024、EXEC-028、EXEC-031 |

## 能力矩阵

| 证明 | 条款 |
|------|------|
| Manager 无普通工具；Orchestrator 只 `commission` Manager | AGENT-011、AGENT-015 |
| mv/rm 仅 Coder；非空目录 rm 拒绝 | AGENT-016…018 |
| bash-honeypot 仅 Coder；调用不跑 shell；instruction-only 无 error 字段 | AGENT-023 |
| DevOps 独占终端/`run`；Reviewer 只读 + `judge` | AGENT-013、AGENT-014 |
| Inquiry = inspect + Sphinx MCP | AGENT-025、AGENT-030 |
| Distiller 无工具且不可见 | AGENT-006、AGENT-008 |
| Browser stealth-browser MCP | AGENT-026 |
| 内部 Semble MCP | AGENT-027 |

## Gate A — Tool Referential Integrity

| 证明 | 期望 | 条款 |
|------|------|------|
| 同名唯一合同 | 同一工具名 → 唯一 schema owner + 唯一语义合同；`fork`≠`commission` | §17 Gate A；AGENT-006、AGENT-015 |
| 新名齐全旧名缺席 | `commission`/`inspect`/`horizon`/`judge`/`chronicle`/`run`/`establish-behavior`/`repair-behavior`/终端四动词/`js-bookkeeper` 在位；旧名集合为空 | AGENT-006、AGENT-007 |

## stealth-browser MCP

| 证明 | 期望 | 条款 |
|------|------|------|
| Host schema 键 | Browser allow `stealth-browser-mcp_*`；其它 role deny；无虚构 `network` | AGENT-006、AGENT-026 |
| wildcard 求值 | Browser 对 `stealth-browser-mcp_get_debug_view` = allow；Coder/Inquiry = deny | AGENT-007、AGENT-026 |
| config 注入 | `configureFromHostConfig` 写入 `mcp.stealth-browser-mcp`；不删其它 MCP | AGENT-026 |
| 启动判定 | disabled / fixture / test / uvx ref 四分支确定性 | AGENT-026 |
| 不进 ToolRegistry / js-* | plugin `tool` 注册表无 stealth-browser 名；js-browser 仍仅 fs 投影 | AGENT-026、JS-001 |

## Sphinx MCP

| 证明 | 期望 | 条款 |
|------|------|------|
| Host schema 键 | Inquiry allow `sphinx_*`；其它 role deny | AGENT-006、AGENT-030 |
| wildcard 求值 | Inquiry 对 `sphinx_start` / `sphinx_resume` = allow；Coder/Browser = deny | AGENT-007、AGENT-030 |
| config 注入 | `configureFromHostConfig` 写入 `mcp.sphinx`；不删其它 MCP | AGENT-030 |
| 启动判定 | disabled / fixture / test / 生产 node 四分支确定性 | AGENT-030 |
| 不进 ToolRegistry / js-* | plugin `tool` 注册表无 sphinx 名 | AGENT-030、SPHINX-005 |
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
| warm-start normalization | CRLF/LF、trim、blank、stable case-sensitive dedupe；8-keyword cap → `tests/unit/agent/repository-warm-start.test.mjs` | AGENT-032 |
| bounded parallel search | 独立 queries 同一 wave；每 query top_k=4；失败独立 fail-open；merge 恢复 keyword ordinal/local rank → `tests/unit/agent/repository-warm-start.test.mjs` | AGENT-032 |
| prompt safety/bounds | ≤24 unique hints；≤64KiB；只删完整 entry；hostile strings 仍解析为 data；zero-keyword byte-exact Charge → `tests/unit/agent/repository-warm-start.test.mjs` | AGENT-032 |
| role gate | snippets 只到 Coder/Inspector/DevOps；其它 role 非空 keywords fail；commission 无 keywords → warm-start + `tests/unit/tools/fork-tool.test.mjs` | AGENT-032 |
| NEEDHELP collaboration | exact Host run binding；fast→deep 同 Life；deep→真实 `deep-inquiry` consultation child；finite/single-flight/no-recursion/cancel-no-resurrection → `tests/unit/host/needhelp-sensor.test.mjs` + `tests/unit/host/assistance-host.test.mjs` | AGENT-031 |

代表测试：`tests/unit/agent/catalog.test.mjs`、`tests/unit/plugin/agent-permission-gate.test.mjs`、
`tests/unit/agent/stealth-browser-mcp.test.mjs`、`tests/unit/agent/semble-mcp.test.mjs`、
`tests/unit/agent/sphinx-mcp.test.mjs`、`tests/unit/sphinx/*.test.mjs`、
`tests/integration/plugin/manager-tool-contract.test.mjs`、`file-mutation-tools.test.mjs`；
Inquiry inspect + Sphinx / Persona immutability / Gate A / 旧名 absence ratchet 随 GrandRewrite + Sphinx 落地。
