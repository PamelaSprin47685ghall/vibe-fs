# 文档导航

规范与工程知识按问题分域。阅读与实现顺序：

```text
what → shape → how → code/resources
              ↓
            proof
```

流动面：`proposal/`（未裁决）、`status/`（实现差距）。  
治理合同：`what/document-governance.md`；执行程序：`how/document-governance.md`；理由：`why/document-governance.md`。

## 正式规范索引

| 主题 | what | shape | how | proof | why |
|------|------|-------|-----|-------|-----|
| 文档治理 | [document-governance](what/document-governance.md) | [document-governance](shape/document-governance.md) | [document-governance](how/document-governance.md) | [document-governance](proof/document-governance.md) | [document-governance](why/document-governance.md) |
| 架构 DNA / 程序结构 | [architecture](what/architecture.md) | [architecture](shape/architecture.md) | [architecture](how/architecture.md) | [architecture](proof/architecture.md) | [architecture](why/architecture.md) |
| Agent 与能力 | [agent](what/agent.md) | [agent](shape/agent.md) | [agent](how/agent.md) | [agent](proof/agent.md) | [agent](why/agent.md) |
| Prompt 与 Authority | [prompt](what/prompt.md) | [prompt](shape/prompt.md) | [prompt](how/prompt.md) | [prompt](proof/prompt.md) | [prompt](why/prompt.md) |
| Fallback | [fallback](what/fallback.md) | [fallback](shape/fallback.md) | [fallback](how/fallback.md) | [fallback](proof/fallback.md) | [fallback](why/fallback.md) |
| Review | [review](what/review.md) | [review](shape/review.md) | [review](how/review.md) | [review](proof/review.md) | [review](why/review.md) |
| Orchestrator | [orchestrator](what/orchestrator.md) | [orchestrator](shape/orchestrator.md) | [orchestrator](how/orchestrator.md) | [orchestrator](proof/orchestrator.md) | [orchestrator](why/orchestrator.md) |
| Host 集成 | [host](what/host.md) | [host](shape/host.md) | [host](how/host.md) | [host](proof/host.md) | [host](why/host.md) |
| Companion / 投影 | [companion](what/companion.md) | [companion](shape/companion.md) | [companion](how/companion.md) | [companion](proof/companion.md) | [companion](why/companion.md) |
| 执行模型 | [execution](what/execution.md) | [execution](shape/execution.md) | [execution](how/execution.md) | [execution](proof/execution.md) | [execution](why/execution.md) |
| 验证 | — | — | — | [verify](proof/verify.md) | — |
| Journal / 持久化 | [persist](what/persist.md) | [persist](shape/persist.md) | [persist](how/persist.md) | [persist](proof/persist.md) | [persist](why/persist.md) |
| 上下文恢复 | [context](what/context.md) | [context](shape/context.md) | [context](how/context.md) | [context](proof/context.md) | [context](why/context.md) |
| 合成 TOML | [synthetic-toml](what/synthetic-toml.md) | [synthetic-toml](shape/synthetic-toml.md) | [synthetic-toml](how/synthetic-toml.md) | [synthetic-toml](proof/synthetic-toml.md) | [synthetic-toml](why/synthetic-toml.md) |
| 数据视图隔离 / 会话边界 | — | [security](shape/security.md) | — | [verify](proof/verify.md) | — |
| 结构化程序 FLOW | [flow](what/flow.md) | [flow](shape/flow.md) | [flow](how/flow.md) | [flow](proof/flow.md) | [flow](why/flow.md) |
| Blogger / Enforcer | [enforcer](what/enforcer.md) | [enforcer](shape/enforcer.md) | [enforcer](how/enforcer.md) | [enforcer](proof/enforcer.md) | [enforcer](why/enforcer.md) |
| Projection Algebra | [projection](what/projection.md) | [projection](shape/projection.md) | [projection](how/projection.md) | [projection](proof/projection.md) | [projection](why/projection.md) |
| 循环检测 | [loop](what/loop.md) | [loop](shape/loop.md) | [loop](how/loop.md) | [loop](proof/loop.md) | [loop](why/loop.md) |
| 词汇表 | [glossary](what/glossary.md) | — | — | — | — |

## 条款前缀归属（定义所在主题文件）

| 前缀 | 定义文件（相对 docs/） |
|------|------------------------|
| `GOV-` | `what/document-governance.md` |
| `ARCH-` | `shape/architecture.md`（结构边界）与 `what/architecture.md`（可观察不变量）— 以各文件内 `## ARCH-NNN` 定义为准 |
| `AGENT-` | `what/agent.md` / `shape/agent.md` |
| `PROMPT-` | `what/prompt.md` / `shape/prompt.md` / `how/prompt.md` |
| `FALLBACK-` | `what/fallback.md` / `shape/fallback.md` / `how/fallback.md` |
| `REVIEW-` | `what/review.md` / `shape/review.md` / `how/review.md` |
| `ORCH-` | `what/orchestrator.md` / `shape/orchestrator.md` / `how/orchestrator.md` |
| `HOST-` | `what/host.md` / `shape/host.md` / `how/host.md` |
| `COMPANION-` | `what/companion.md` / `shape/companion.md` / `how/companion.md` |
| `EXEC-` | `what/execution.md` / `shape/execution.md` / `how/execution.md` |
| `VERIFY-` | `proof/verify.md` |
| `PERSIST-` | `what/persist.md` / `shape/persist.md` / `how/persist.md` |
| `CTX-` | `what/context.md` / `shape/context.md` / `how/context.md` |
| `FLOW-` | `what/flow.md` / `shape/flow.md` / `how/flow.md` / `proof/flow.md` |
| `ENFORCER-` | `what/enforcer.md` / `shape/enforcer.md` / `how/enforcer.md` / `proof/enforcer.md` |
| `PROJ-` | `what/projection.md` / `shape/projection.md` / `how/projection.md` |
| `LOOP-` | `what/loop.md` / `shape/loop.md` / `how/loop.md` / `proof/loop.md` |

检查器以全文唯一 `## PREFIX-NNN` 定义为准；上表为导航，不复制条款正文。

## 流动面

| 路径 | 用途 |
|------|------|
| `proposal/` | 未裁决设计（如 strength、student-teacher） |
| `status/` | 实现相对规范的活跃差距 |

## 工程操作（非产品条款）

开发命令、发布步骤见根 `AGENTS.md`。产品交付说明见根 `README.md`。  
旧 `spec/`、`docs/rfcs/`、`docs/decisions/` 已 clean break，不具权威。

阅读建议：先 `what/` 行为 → `shape/` 边界 → `how/` 目标实现 → 对照 `status/` → 改代码 → 用 `proof/` 与 `npm run lint` / 测试证明。