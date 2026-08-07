# 文档导航

本页只提供路由和索引，不定义产品行为。文档治理规则见
[`GOV-`](what/document-governance.md)，术语见[词汇表](what/glossary.md)。

## 体系与阅读顺序

| 位置 | 职责 |
|---|---|
| [`why/`](why/) | 理由、权衡与被拒方案；不直接约束实现 |
| [`what/`](what/) | 可观察行为、语义与不变量 |
| [`shape/`](shape/) | 所有权、唯一 writer、边界与依赖方向 |
| [`how/`](how/) | 已裁决目标算法、控制流与数据转换 |
| [`proof/`](proof/) | 证明义务、门禁、测试与反例 |
| [`status/`](status/) | 实现相对正式规范的活跃差距 |
| [`proposal/`](proposal/) | 未裁决候选 Delta；不是当前规范 |

按任务读取：

```text
what → shape → how → status → code/resources → proof
```

`why` 用于理解理由；Proposal 只在评审候选变化时读取。治理主题的五层入口：
[what](what/document-governance.md) · [shape](shape/document-governance.md) ·
[how](how/document-governance.md) · [proof](proof/document-governance.md) ·
[why](why/document-governance.md)。

## 主题索引

每行依次链接 `what / shape / how / proof / why`；缺少某层表示该主题使用共享证明或理由。

| 条款前缀 / 主题 | 文档 |
|---|---|
| `GOV-` 文档治理 | [what](what/document-governance.md) · [shape](shape/document-governance.md) · [how](how/document-governance.md) · [proof](proof/document-governance.md) · [why](why/document-governance.md) |
| `ARCH-` 架构 | [what](what/architecture.md) · [shape](shape/architecture.md) · [how](how/architecture.md) · [proof](proof/architecture.md) · [why](why/architecture.md) |
| `AGENT-` Agent | [what](what/agent.md) · [shape](shape/agent.md) · [how](how/agent.md) · [proof](proof/agent.md) · [why](why/agent.md) |
| `PROMPT-` Prompt | [what](what/prompt.md) · [shape](shape/prompt.md) · [how](how/prompt.md) · [proof](proof/prompt.md) · [why](why/prompt.md) |
| `FALLBACK-` Fallback | [what](what/fallback.md) · [shape](shape/fallback.md) · [how](how/fallback.md) · [proof](proof/fallback.md) · [why](why/fallback.md) |
| `REVIEW-` Review | [what](what/review.md) · [shape](shape/review.md) · [how](how/review.md) · [proof](proof/review.md) · [why](why/review.md) |
| `ORCH-` Orchestrator | [what](what/orchestrator.md) · [shape](shape/orchestrator.md) · [how](how/orchestrator.md) · [proof](proof/orchestrator.md) · [why](why/orchestrator.md) |
| `HOST-` Host | [what](what/host.md) · [shape](shape/host.md) · [how](how/host.md) · [proof](proof/host.md) · [why](why/host.md) |
| `COMPANION-` Companion | [what](what/companion.md) · [shape](shape/companion.md) · [how](how/companion.md) · [proof](proof/companion.md) · [why](why/companion.md) |
| `EXEC-` Execution | [what](what/execution.md) · [shape](shape/execution.md) · [how](how/execution.md) · [proof](proof/execution.md) · [why](why/execution.md) |
| `VERIFY-` 验证 | [proof](proof/verify.md) |
| `PERSIST-` 持久化 | [what](what/persist.md) · [shape](shape/persist.md) · [how](how/persist.md) · [proof](proof/persist.md) · [why](why/persist.md) |
| `CTX-` 上下文 | [what](what/context.md) · [shape](shape/context.md) · [how](how/context.md) · [proof](proof/context.md) · [why](why/context.md) |
| `FLOW-` 结构化流程 | [what](what/flow.md) · [shape](shape/flow.md) · [how](how/flow.md) · [proof](proof/flow.md) · [why](why/flow.md) |
| `ENFORCER-` Enforcer | [what](what/enforcer.md) · [shape](shape/enforcer.md) · [how](how/enforcer.md) · [proof](proof/enforcer.md) · [why](why/enforcer.md) |
| `PROJ-` Projection | [what](what/projection.md) · [shape](shape/projection.md) · [how](how/projection.md) · [proof](proof/projection.md) · [why](why/projection.md) |
| `LOOP-` Loop | [what](what/loop.md) · [shape](shape/loop.md) · [how](how/loop.md) · [proof](proof/loop.md) · [why](why/loop.md) |
| `DSL-` DSL 结构化程序 | [what](what/dsl-structured-program.md) · [shape](shape/dsl-structured-program.md) · [how](how/dsl-structured-program.md) · [proof](proof/dsl-structured-program.md) · [why](why/dsl-structured-program.md) |
| `GLORY-` / `SURFACE-` Glory | [what](what/glory.md) · [shape](shape/glory.md) · [how](how/glory.md) · [proof](proof/glory.md) · [why](why/glory.md) |
| Synthetic TOML（`ARCH-010` / `ARCH-011`） | [what](what/synthetic-toml.md) · [shape](shape/synthetic-toml.md) · [how](how/synthetic-toml.md) · [proof](proof/synthetic-toml.md) · [why](why/synthetic-toml.md) |
| Security 边界 | [shape](shape/security.md) |
| Kolmogorov 原则 | [why](why/kolmogorov.md) |

## 活跃实现差距

- [Projection Algebra](status/projection-algebra-gap.md)

## 未裁决候选

- [F# DSL 治理候选](proposal/fsharp-dsl-governance.md)
- [Strength](proposal/strength.md)
- [Student / Teacher](proposal/student-teacher.md)
- [waitFact causal renewal](proposal/waitfact-causal-renewal.md)
