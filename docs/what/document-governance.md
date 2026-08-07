# 文档治理 — 必须成立的规则

本文件定义文档体系本身的行为合同。产品行为条款见各主题 `what/`；本文件只约束文档如何管辖知识。

## GOV-001：七目录与三类状态

治理与工程文档统一在 `docs/` 下，目录名小写：

```text
docs/
├── why/
├── what/
├── shape/
├── how/
├── proof/
├── status/
└── proposal/
```

三类状态：

```text
规范面：why / what / shape / how / proof
流动面：proposal / status
实现面：code / resources / 配置与构建产物
```

根目录仍保留产品交付面文件：`README.md`、`CHANGELOG.md`、`LICENSE`、`package.json` 等。它们不是七目录内的规范条款正文。`AGENTS.md` 承载宿主侧工程纪律与日常工作流，不替代七目录规范面。

## GOV-002：分域权威

| 目录 | 回答的问题 | 权威性质 |
|------|------------|----------|
| `why/` | 为什么这样设计？ | 理由与被拒方向；不直接约束代码 |
| `what/` | 当前系统应表现出什么行为？ | 已裁决的行为、语义、不变量 |
| `shape/` | 为实现 `what/`，所有权与边界如何？ | 边界、依赖方向、单一写入口、模块职责 |
| `how/` | 目标实现应如何工作？ | 可执行的目标实现：数据流、控制流、算法 |
| `proof/` | 如何证明规范成立？ | 验证策略、门禁、契约与回归要求 |
| `status/` | 实现相对规范差在哪？ | 活跃差距与阻塞；不定义产品行为 |
| `proposal/` | 是否应改变当前系统？ | 未裁决候选；不是当前规范 |

`how/` 描述的“目标实现”= 当前已裁决、应当被实现、足够具体可执行的状态。不是远期愿景，也不是运行时代码结构的流水账。

`resources/` 属于实现面。规范定义语义；实现可把实例放在 JSON、代码、生成物等载体中。产物不得发明文档未声明的新语义。

`AGENTS.md` 只承载 Agent 工作协议和规范路由；`docs/README.md` 只承载导航与索引。两者不得定义或改写正式产品条款。

## GOV-003：执行链

```text
what → shape → how → code/resources
                ↓
              proof
```

1. 代码与资源对齐 `how/` 的目标实现；不得直接解释 `what/`、`why/` 或 `proposal/`。
2. `shape/` 把 `what/` 转成所有权、边界与依赖方向。
3. `how/` 把 `shape/` 转成可执行目标设计。
4. `proof/` 验证整条 `what → shape → how → 实现面` 链，不只验证 `how/`。
5. 文档矛盾由治理程序解决，实现者不得临场选边。
6. `proposal/` 不得被代码直接实现。

错误路径：`what → code`、`proposal → code`。  
正确路径：`what → shape → how → code`；`proposal → 裁决 → 规范面 → code`。

治理程序自身也服从同一体系：理由在 `why/document-governance.md`，规则在本文件，执行程序在 `how/document-governance.md`。治理执行歧义时以 `how/document-governance.md` 为准。这不授权普通产品 `how/` 推翻产品 `what/`。

## GOV-004：滚动基线

```text
当前规范面 + 当前实现面 = 当前系统
```

不维护按版本复制的文档快照，不引入文档 revision 封印或 release manifest 绑定每篇文档版本。下一变更合入时，相关规范面必须恢复内部一致。禁止长期“先改代码、后补文档”而不在 `status/` 登记差距。

`what/` 是已裁决的当前行为设计；兼容性承诺由相关产品条款决定。任何破坏性变更必须同步传播到 `shape/how/proof`，并显式选择 `Compatible | ExplicitMigration | ExplicitReset | CleanBreak` 之一——“不保证兼容”不等于“不作迁移决策”。

## GOV-005：条款 ID

1. 既有 Clause ID 原样保留，不因目录、文件、章节搬家而重编号。
2. 允许编号不连续；删除后编号永久空缺，不回收。
3. 每个 ID 恰好一个定义位置；其它文件只能引用，不得复制定义。
4. ID 可落在 `why/what/shape/how/proof` 中最适合承载其核心规范命题的位置。
5. 新条款沿用领域前缀（`ARCH-`、`PROMPT-` 等）；文档治理条款前缀为 `GOV-`。
6. 同一主题跨层优先使用相同 slug（如 `what/fallback.md` 与 `how/fallback.md`）。

## GOV-006：Proposal 生命周期

```text
proposal → 裁决 → 正式层分发 → 活跃 status gap → 实现 → proof → 删除已关闭 gap
```

- **未实现 Proposal 删除保护**：禁止未经用户同意删除任何未实现的 proposal。
- **单向规范流动与更新**：Proposal 经裁决或推进时，遵循 `what → shape → how → code/resources` 单向链与 `proof` 验证，同一次变更内依序更新 `why` → `what` → `shape` → `how` 并决定 `proof`。
- **转换为 status 跟踪差距**：规范更新后若实现尚未完成，须把剩余物理差距写入 `status/`。不得把 Proposal 正文原样搬入 `status/`，也不得保留已分发的候选设计副本。
- **代码实现与验证**：阅读相关的代码和文档，进行代码实现，并通过 `proof` 检查验证整条 `what → shape → how → code` 链。
- **对齐后清理**：仅在代码实现完全对齐规范且通过 `proof` 检查后，方可删除对应差距条目。
- **长期理由分发**：有长期价值的理由写入 `why/`；不保留已完成或已废止 proposal 全文副本作为第二设计源。

## GOV-007：规范面可接纳性（Proposal Admission）

Proposal 可创建、讨论、修改。仅当规范面可接纳时，才允许裁决并写入正式层：

```text
BaselineAdmissible =
    DocumentationConsistent
    ∧ ExistingGapsRepresentedInStatus
    ∧ NoUntrackedBlockingContradiction
    ∧ ProposalImpactComplete
```

代码未完成、测试未绿，不自动禁止设计裁决。实现与发布是否达标属于实施门禁与发布门禁，不是 Proposal 存在前提。

禁止在基线修复中偷渡 proposal 架构：“旧架构不好，所以修复时顺便采用新设计”必须拆成先恢复当前规范可信，再独立裁决 proposal。

## GOV-008：status 合同

`status/` 只保存活跃差距：

```text
未完成或阻塞 → 可存在
实现已对齐规范 → 立即删除对应条目
```

禁止：流水账、完成百分比、提交列表、详细代码快照、已完成事项墓地、未裁决设计。

完成历史由 Git 与 `CHANGELOG` 承担。

## GOV-009：矛盾处理

允许：记录矛盾、划定影响、修复当前规范面、由责任方裁决、原子修改相关文档与必要实现。

禁止：实现者自选更喜欢的文档；从 `what/` 越级改代码；用 proposal 覆盖当前 `how/`；把矛盾说成“实现自由”。

裁决完成前，实现工作仍以当前 `how/` 为目标依据——这不表示 `how/` 可在逻辑上推翻 `what/`，而表示实现者无权重新设计系统。

## GOV-010：Clean break

本次文档体系切换为治理层 clean break：

- 切换后仅新目录体系具有规范权威。
- 原 `spec/` 不再具有特殊权威。
- 原 `docs/decisions/`、`docs/rfcs/` 不再具有旧状态语义。
- `TASK.md`、`PENDING.md` 不作为设计或实施权威。
- 未迁移进新体系的旧规则视为废止，不得隐式继续生效。
- 唯一保留的稳定身份是已迁移的 Clause ID。

## GOV-011：行为条款的层归属

可观察行为、语义与不变量（系统「是什么、必须怎样」）的权威定义在 `what/`。  
`how/` 即使经 GOV-005.4 承载同前缀条款，也只写「如何工作」——算法、数据流、控制流、类型与机制；不得以 `how/` 作为新行为的唯一权威来源。

判据：一段文字能否被外部观察者检验（给同一输入，系统表现是否可断言）？能 → 归 `what/`。能否只表达实现内部如何走到该表现？能 → 归 `how/`。

代码与资源对齐 `how/` 的目标实现（GOV-003.1），但行为不匹配时以 `what/` 的行为条款为准，不得把 `how/` 当行为豁免。

## GOV-012：Hotfix 原子修补

线上严重事故允许省略独立 proposal 文件，但只豁免前置文档形式，不豁免规范、证明与兼容性裁决：

1. 修补范围必须直接收敛事故，不得借机实现未裁决 proposal 或降低既有条款。
2. 同一变更必须原子更新受影响的 `what/shape/how/proof`、实现与回归测试。
3. 破坏性变化仍须按 GOV-004 显式选择兼容、迁移、重置或 clean break。
4. 无法在同一变更恢复规范内部一致时，不适用 Hotfix 豁免，改走 GOV-006 proposal 生命周期。
