# 文档治理 — 必须成立的规则

本文件定义文档与变更工作记录如何管辖知识。产品行为条款见各主题 `what/`。

## GOV-001：正式层与变更历史

当前产品规范只位于：

```text
docs/
├── why/
├── what/
├── shape/
├── how/
└── proof/
```

变更工作记录位于：

```text
changes/
├── proposed/
├── active/
└── completed/
```

`docs/README.md` 只导航当前正式知识；`changes/README.md` 定义变更目录的工作协议。
根 README、CHANGELOG、AGENTS 和 Changes 文件都不是产品规范正文。

## GOV-002：分域权威

| 位置 | 回答的问题 | 权威性质 |
|---|---|---|
| `docs/why/` | 为什么这样设计？ | 理由与被拒方向；不直接约束代码 |
| `docs/what/` | 当前系统应表现出什么行为？ | 已裁决行为、语义与不变量 |
| `docs/shape/` | 所有权与边界如何？ | 边界、依赖方向、唯一 writer 与模块职责 |
| `docs/how/` | 目标实现如何工作？ | 已裁决算法、数据流与控制流 |
| `docs/proof/` | 如何证明规范成立？ | 门禁、测试、反例与发布义务 |
| `changes/proposed/` | 哪些已批准工作等待用户启动？ | 用户管理的 Proposal 原文；不是当前规范 |
| `changes/active/` | 哪些已启动工作尚未闭环？ | 冻结 Proposal、Remaining work、blocker 与完成条件 |
| `changes/completed/` | 哪些工作已经闭环？ | 永久历史记录；不解释当前产品语义 |

`resources/`、代码、配置与构建产物属于实现面，不得发明正式文档未声明的产品语义。
Changes 文件只保存变更范围和历史；目标产品语义必须进入正式 `docs/`。

## GOV-003：执行链

当前规范到实现的链保持不变：

```text
what → shape → how → code/resources
                ↓
              proof
```

变更启动链与正式执行链分离：

```text
用户管理 changes/proposed
        ↓ 用户明确启动指定文件
changes/active
        ↓ 更新正式 docs、实现与 proof
changes/completed
```

禁止：Agent 自选 Proposed 直接实现；Active 代替正式 docs；Completed 解释当前产品行为。
正确路径：用户指定 Proposal → 移入 Active → 更新正式层 → 修改实现 → proof → 同文件移入 Completed。

## GOV-004：滚动基线

```text
当前正式 docs + 当前实现面 = 当前系统
```

- Proposed 保存已批准但尚未启动的工作，不属于当前系统。
- Active 表示工作已启动但尚未闭环；它不定义目标语义，正式 docs 才定义目标。
- Completed 是历史记录，不属于当前规范，也不参与普通实现解释。

正式 docs 与实现暂时不一致时，差距必须能从相关 Active 文件的 Remaining work 定位；
不得把 Active 半成品写回 how 充当目标规范。

兼容性承诺由相关产品条款决定。破坏性变更必须在批准范围中选择
`Compatible | ExplicitMigration | ExplicitReset | CleanBreak`，并同步传播到 shape/how/proof。

## GOV-005：条款与 Change ID

1. 正式 Clause ID 只能定义在 `docs/why|what|shape|how|proof`。
2. 每个正式 Clause ID 恰好一个定义位置；其它文件只能引用。
3. 既有 Clause ID 移动时保留原编号；删除后编号永久空缺。
4. ID 位于最适合承载核心命题的正式层，不为目录整齐重编号。
5. `changes/proposed|active|completed` 均不得定义正式 Clause ID，但可以引用它们。
6. Change 可以使用独立 `CHG-NNN` 编号；它不是产品 Clause ID，不建立中央注册表。
7. 迁移旧工作项不要求补 Change ID。

## GOV-006：单文件 Change 生命周期

每项 Change 始终只对应一个文件：

```text
proposed/<file>.md
    ↓ 用户明确启动
active/<file>.md
    ↓ 实现与验证闭环
completed/<file>.md
```

目录位置是生命周期状态的唯一来源；正文不得维护重复 status 字段。不得创建平行 Proposal、
Status、Decision 或 Outcome 文件，也不得引入 manifest、中央注册表、状态数据库或复杂状态机。

文件从 Proposed 进入 Active 后，Original proposal 永久冻结。实施事实只追加到 Active work、
Blockers、用户批准的 Amendments 和 Final outcome。关闭时先追加 Final outcome，再移动同一文件；
Completed 永久保留 Proposal 原文。

## GOV-007：用户所有权与启动授权

所有进入 `changes/proposed/` 的 Proposal 都已经由用户或负责人裁决并批准。
Agent 不再次检查批准状态、Proposal Admission、Accepted 证据或 Decision Owner。

`changes/proposed/` 由用户管理。Agent 默认不得创建、修改、重命名、移动或删除其中的文件，
不得扫描并自行选择工作，也不得因不同意内容而修改批准范围。

当前用户明确要求实施指定 Proposal，就是将该指定文件移入 Active 并开始工作的充分授权；
Agent 不得再次要求裁决证明。发现正式冲突或客观不可实施条件时，记录 blocker 并报告用户，
不得重新裁决、缩减或扩大批准范围。

## GOV-008：Active 与 Completed 合同

Active 只保存：冻结的 Original proposal（若存在）、工作来源、正式规范影响、有限 Remaining work、
Completion criteria、客观 blocker 和用户批准的 Amendments。

禁止：完成百分比、每日或逐提交流水、大段代码快照、Git 已保存的历史、未经用户批准的新设计、
正式 Clause 定义。实施期间不要求每次提交更新；至少在启动、用户改范围、出现 blocker 和关闭时更新。

没有独立 Proposal 的旧 Status 迁入 Active 时，必须明确标注 Work origin，不得伪造 Original proposal。

Completed 必须永久保存 Original proposal/工作背景、用户批准的范围修订和 Final outcome。
Final outcome 记录结果、最终正式条款与路径、实现结果、实际验证和引用。不得用 Completed 代替正式 docs。

## GOV-009：矛盾与 blocker

实现已批准范围时若发现正式规范矛盾、Host 能力缺失或其它客观不可实施条件：

1. 停止受影响的产品语义修改；
2. 在同一 Active 文件的 Blockers 追加事实、影响和证据；
3. 向用户报告，不自行修改批准范围；
4. 用户明确修订范围时才追加 Amendment，再继续实施。

普通正式规范冲突同样不得由实现者按偏好选边。

## GOV-010：Clean break

当前治理结构是 clean break：

- 原 docs 下的 Proposal 与 Status 目录已废止，不得重新创建或引用为工作入口。
- 原 `spec/`、`docs/decisions/`、`docs/rfcs/` 不再具有规范或状态权威。
- `TASK.md`、`PENDING.md` 不作为设计或实施权威。
- 当前变更状态只由 `changes/proposed|active|completed` 的目录位置表达。
- 未迁入正式 docs 的旧规则不继续生效；正式 Clause ID 保持稳定。

## GOV-011：行为条款的层归属

可观察行为、语义与不变量的权威定义在 what；所有权和 writer 在 shape；算法和数据转换在 how；
证明义务在 proof；理由在 why。Change 文件不得承担任何一层的正式定义职责。

代码与资源对齐 how，但行为不匹配时以 what 为准；Active 不能作为降低或覆盖正式条款的豁免。

## GOV-012：直接闭环的小变更

不改变正式规范的小型 bug 修复、局部重构、测试补充、格式修复、普通依赖升级，以及能在一次修改中
完整对齐 docs、实现和 proof 的小变更，通常不需要 Change 文件。

线上事故也可直接原子修补，但不得借机实现 Proposed、降低正式条款或跳过兼容性与 proof。
若工作已由用户通过指定 Change 启动，则仍按 GOV-006 在同一文件中闭环。
