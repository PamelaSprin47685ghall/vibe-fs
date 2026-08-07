# Agent — 证明

行为：`what/agent.md`。边界：`shape/agent.md`。实现：`how/agent.md`。

## 启动与配置

| 证明 | 期望 | 条款 |
|------|------|------|
| 二十四名 Agent 齐全 | 缺一启动失败 | AGENT-002 |
| peer 对称 + model 非空互异 | 配置验证失败则 fail fast | AGENT-003 |
| 非法旧名 | 无 alias，拒绝 | AGENT-004 |
| Authority 省略 agent | `HostContractUnsupported` | AGENT-005 |

## 权限

| 证明 | 落点 / 形态 | 条款 |
|------|-------------|------|
| 双层 fail-closed | Host schema 无越权工具 + ToolRegistry 拒绝执行 | AGENT-007 |
| Role 未定 → 工具集空 | unit / integration 工具契约 | AGENT-007 |
| `external_directory=allow` | Host ruleset（flat merge + findLast）对任意外部 path 为 allow | AGENT-019 |
| 唯一写入点 | 仅 `StaticTools.permissionObj` / `applyOwnedFields` | AGENT-019 |
| fast/deep 权限相等 | 权限对象结构比较 | AGENT-010 |
| 内部 Agent 不可见 | enum/schema 不含 blogger/executor/teacher | AGENT-008、AGENT-020 |

## Student / Teacher

| 证明 | 落点 / 形态 | 条款 |
|------|-------------|------|
| Student 公开、Teacher 私有 | config/catalog 与 provider-visible enum 对照 | AGENT-008、AGENT-020 |
| request-specific 双门 | Learn schema/gate 仅 `teacher`；Compile schema/gate 仅 read/glob/grep/write/edit/return | AGENT-007、AGENT-020、AGENT-021 |
| Compile 制品边界 | 只接受 `.agent/skills/<name>/SKILL.md`；平铺/绝对/穿越/额外嵌套拒绝 | AGENT-021、AGENT-022、PERSIST-011 |
| SKILL 可加载性 | final return 重读全部触达文件；name/description frontmatter、目录名一致与非空正文缺一即拒绝 | AGENT-022 |
| Teacher 能力 | 普通执行工具 + `return`，无 fork/list/join/PTY；fast/deep 相等 | AGENT-010、AGENT-020 |
| tier/model 绑定 | Student 与 Teacher 同 tier；发送只携带 Agent、不覆盖 model | AGENT-003、AGENT-020 |

## 能力矩阵

| 证明 | 条款 |
|------|------|
| Manager 无普通工具；Orchestrator 只 fork manager | AGENT-011、AGENT-015 |
| mv/rm 仅 Coder；非空目录 rm 拒绝 | AGENT-016…018 |
| DevOps 独占 PTY；Reviewer 只读 | AGENT-013、AGENT-014 |

代表测试：`tests/unit/agent/catalog.test.mjs`、`tests/unit/plugin/agent-permission-gate.test.mjs`、
`tests/unit/student-teacher/tool-loop.test.mjs`、`tests/integration/plugin/manager-tool-contract.test.mjs`、
`file-mutation-tools.test.mjs`。
