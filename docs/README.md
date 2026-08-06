# 万象术（Wanxiangshu）规范与文档体系

万象术是 OpenCode 的高可靠增强插件。本文档体系按工程问题切分，遵循 GOV-003 的单向执行链；`why` 解释理由，`proof` 验证整条执行链，不与 `what/shape/how` 串成另一条权威顺序。

```text
what（可观察行为） → shape（所有权与边界） → how（目标实现） → code/resources
proof（验证与剧本）验证上述整条链
why（设计理由与被拒方案；不直接约束实现）
```

流动面：`proposal/`（未裁决）、`status/`（实现相对规范的活跃差距）。  
治理合同：`what/document-governance.md`；执行程序：`how/document-governance.md`；理由：`why/document-governance.md`。

---

## 顶层架构定调与全局约束（A1 约束定调）

万象术插件的核心设计遵循不可妥协的工程铁律：

1. **结构化程序替代状态机（ARCH-001）**：业务控制流只用 `let!/do!/use!/match/尾递归`，绝对禁止定义程序计数器、Stage、Phase、Lease、Owner 或 Generation 字段。
2. **事件是信号不是数据（ARCH-002）**：流式碎片在最早边界丢弃；只有 `session.status=idle/retry`、`session.deleted` 能驱动业务层。
3. **零修改 OpenCode 本体（ARCH-003）**：只允许使用 OpenCode 现有 Hook 与 SDK API。
4. **失败驱动的上下文恢复（CTX-001 / CTX-002）**：绝对禁止估算 token/容量、禁止比较容量阈值，失败发生前绝对禁止压缩。
5. **分层与 Clean Break（ARCH-006 / ARCH-008）**：生产源码只保留一套分层根；旧路径与旧命名不得重新成为实现依赖。

---

## 评审维度覆盖映射表

为满足技术方案评审与工程纪律要求，各信息维度的权威落点如下：

| 维度 | 定义说明 | 权威落点 | 涵盖文件 |
| :--- | :--- | :--- | :--- |
| **A1 约束定调** | 不可妥协的工程铁律与 No-Go 门禁 | `what/architecture.md`, `proof/verify.md` | ARCH-001..012, CTX-001..002 |
| **A2 需求意图与范围** | 问题陈述、输入输出与规则边界 | `what/*.md` 头部, `how/*.md` 头部 | 各主题的 what/how 头部 |
| **A3 地缘上下文** | 物理位置、宿主拓扑与构建契约 | `shape/architecture.md`, `how/host.md` | ARCH-003, HOST-009..012 |
| **A4 设计理由 (WHY)** | 决策考量、被拒方向与权衡 | `why/*.md` | 各主题的 why/ 目录 |
| **B1 代码组织边界** | 模块划分、包依赖与层级拓扑 | `shape/architecture.md`, `shape/*.md` | ARCH-006..008 |
| **B2 架构与模块拓扑** | 内部组件交互、执行与投影模型 | `how/architecture.md`, `how/projection.md` | PROJ-001..008, FLOW-001..008 |
| **B3 功能流程与失败路径** | 正常流转与异常 Fallback 路径 | `how/fallback.md`, `how/context.md` | FALLBACK-001..012, CTX-001..014 |
| **B4 恢复决策** | 事实、证据与有限恢复构造 | `how/fallback.md`, `how/enforcer.md` | FALLBACK-002, ENFORCER-001..071 |
| **B5 依赖契约与类型边界** | 强类型 DU、值对象与接口边界 | `shape/prompt.md`, `shape/fallback.md` | PROMPT-008, FALLBACK-002 |
| **B6 数据与领域建模** | 事实、证据与 Witness 结构 | `how/persist.md`, `shape/review.md` | PERSIST-001..010, REVIEW-001..010 |
| **B7 关键技术决策** | 核心算法与 DSL 迁移计划 | `why/*.md`, `how/synthetic-toml.md` | LOOP-001..010, PROJ-008 |
| **C1 安全与数据隔离** | 视图裁剪矩阵与低信任上下文 | `shape/security.md` | CTX-013, COMPANION-010/012, REVIEW-006 |
| **C2 并发安全** | 线程模型、多实例共享与单写入口 | `shape/host.md`, `how/loop.md` | HOST-012, LOOP-005/006 |
| **D1 白盒可测性** | 测试金字塔、单元门禁与断言契约 | `proof/verify.md` | VERIFY-001..008 |
| **D2 黑盒对齐验收** | Canary Mock 剧本与稳定性检查 | `proof/verify.md`, `proof/*.md` | VERIFY-003..004 |

---

## 代码地图与物理地缘（A3 / B1 / B2）

### 1. 物理位置与环境拓扑
- **Host 二进制**：已安装 OpenCode 二进制位于 `~/.bun/bin/opencode`（指向 `~/.bun/install/global/node_modules/opencode-ai/bin/opencode.exe`）。
- **插件本仓库**：当前 Working Tree 根路径，负责编译与分发万象术 OpenCode 插件。
- **Host 源码引用**：`how/host.md` 中引理形式化锚定 OpenCode 源码 commit `e024e2ef` 的 `prompt.ts` 与 `processor.ts`。

### 2. 源码结构（`src/Wanxiangshu/`）
生产源码统一在 `src/Wanxiangshu/`（由 `Wanxiangshu.fsproj` 编译全部）：
- `Kernel/`：与业务无关的基础代数与并发控制（`AsyncSupport.fs`、`Parallel.fs` 等）。
- `Domain/`：领域事实、证据、决策与值对象（纯逻辑，不引用上层与 `Fable.Core.JsInterop`）。
- `Session/`：会话级运行时 cell 与直接执行的结构化程序；禁止业务 Program AST + Interpreter。
- `Application/`：工作流、恢复逻辑与协调器（`Reconciliation/`、`Orchestrator/`）。
- `Infrastructure/`：与 OpenCode Host/SDK/Journal/Resources 适配（`OpenCode/`、`Journal/`、`Resources/`）。

### 3. 分发与构建契约
- **编译工具链**：Fable 将 F# 源码编译为 JavaScript 产物至 `dist/` 目录。
- **主入口**：npm 包主入口指向 `dist/Infrastructure/OpenCode/Plugin/Plugin.js`。
- **静态资源**：`resources/prompts/` 与 `resources/enforcer/catalog.json` 随 npm 包直接分发。

---

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
| 结构化程序 DSL | [dsl-structured-program](what/dsl-structured-program.md) | [dsl-structured-program](shape/dsl-structured-program.md) | [dsl-structured-program](how/dsl-structured-program.md) | [dsl-structured-program](proof/dsl-structured-program.md) | [dsl-structured-program](why/dsl-structured-program.md) |
| Blogger / Enforcer | [enforcer](what/enforcer.md) | [enforcer](shape/enforcer.md) | [enforcer](how/enforcer.md) | [enforcer](proof/enforcer.md) | [enforcer](why/enforcer.md) |
| Projection Algebra | [projection](what/projection.md) | [projection](shape/projection.md) | [projection](how/projection.md) | [projection](proof/projection.md) | [projection](why/projection.md) |
| 循环检测 | [loop](what/loop.md) | [loop](shape/loop.md) | [loop](how/loop.md) | [loop](proof/loop.md) | [loop](why/loop.md) |
| 词汇表 | [glossary](what/glossary.md) | — | — | — | — |

---

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
| `DSL-` | `what/dsl-structured-program.md` / `shape/dsl-structured-program.md` / `how/dsl-structured-program.md` / `proof/dsl-structured-program.md` / `why/dsl-structured-program.md` |

检查器以全文唯一 `## PREFIX-NNN` 定义为准；上表为导航，不复制条款正文。

---

## 流动面与活跃差距跟踪 (`status/`)

| 差距记录文件 | 主题与消除目标 |
|------|------|
| [fallback-offset-du-gap.md](status/fallback-offset-du-gap.md) | 将 `byte` 泄露消除，全域使用 `FallbackOffset` DU 拦截非法字节 |
| [enforcer-fallback-bridge-gap.md](status/enforcer-fallback-bridge-gap.md) | 让无效 Blogger cycle 经唯一 FallbackController 真正推进 A/A/B/B |
| [projection-algebra-gap.md](status/projection-algebra-gap.md) | 收敛 Legacy 拼接到纯 Projection Planner，并覆盖现行投影 intent |
| [gov-behavior-migration.md](status/gov-behavior-migration.md) | 按照 GOV-011 将 `how/` 中遗留的行为定义升迁至 `what/` |
| [proposal-code-isolation-gap.md](status/proposal-code-isolation-gap.md) | 清除生产编译图对未裁决 Strength / Student-Teacher proposal 的直接依赖 |

---

## 未裁决候选 (`proposal/`)

| Proposal | 候选范围 |
|------|------|
| [ChatGPT-F# DSL 规范问题.md](proposal/ChatGPT-F# DSL 规范问题.md) | F# 结构化流程 DSL 迁移分析与痛点候选 |
| [glory.md](proposal/glory.md) | Manager 全生命周期「生于任务，终于荣耀」提示词与工具重写候选 |
| [strength.md](proposal/strength.md) | Predict & Reduce Strength 旁路投机执行 |
| [student-teacher.md](proposal/student-teacher.md) | Student / Teacher 知识生产流程 |

Proposal 仅供讨论，不是实现依据。裁决流程与最小模板见 [document-governance](how/document-governance.md)。

---

## Clean Break 机制

本仓库已实施完整的规范 Clean Break：
1. 旧 `spec/` 目录、`docs/rfcs/`、`TASK.md`、`SSOT/` 均已删除且不再具有权威；proposal 内的历史迁移文字不是现行路径引用。
2. 规范条款的唯一真理源为 `docs/what/`, `shape/`, `how/`, `proof/`, `why/` 中的 Markdown 文件。
3. `scripts/checks/spec.mjs` 在 CI 与 `npm run lint` 中硬阻断条款唯一性、前缀归属、悬空引用、伪条款 ID 与导航遗漏；跨文件语义一致性仍须由 proof 与评审验证。

---

## 阅读与审计路径

针对不同维度的评审者，推荐阅读顺序如下：

1. **架构与约束审计**：`what/architecture.md` → `shape/architecture.md` → `how/architecture.md` → `why/architecture.md`
2. **并发与 Host 隔离审计**：`what/host.md` → `shape/host.md` (HOST-012) → `how/host.md` → `why/host.md`
3. **退化循环与自动强杀**：`what/loop.md` → `shape/loop.md` → `how/loop.md` → `why/loop.md`
4. **验证与 Canary 测试**：`proof/verify.md` (VERIFY-001..008) → `tests/unit/` → `tests/e2e/`
