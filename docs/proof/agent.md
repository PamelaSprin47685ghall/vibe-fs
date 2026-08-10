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
| 工具面仅 `inspector` | Host schema + ToolRegistry 均无 read/glob/grep/write/edit/executor/coder/fork/join/list/PTY/network | AGENT-006、AGENT-025 |
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
| Meditator inspector-only | AGENT-025 |

代表测试：`tests/unit/agent/catalog.test.mjs`、`tests/unit/plugin/agent-permission-gate.test.mjs`、
`tests/integration/plugin/manager-tool-contract.test.mjs`、`file-mutation-tools.test.mjs`；
Meditator inspector-only / Student-Teacher absence ratchet 随 G3 落地。
