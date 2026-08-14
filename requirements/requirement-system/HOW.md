# HOW：requirement-system 的实现模型与约束

非 normative。本文件解释机制怎么工作、历史怎么来的、什么被弃权。

## 实现模型

本包无 runtime 源码（META 包正确形态，见 `archive/requirements-design/EVIDENCE.md` §1：证据 =
`AGENTS.md` + `scripts/checks` + CI）。机制由四块组成：

### 1. meta-verifier（`tests/meta-verifier.test.mjs`）

`node --test requirements/requirement-system/tests/meta-verifier.test.mjs`

扫描 `requirements/` 全树，五个断言：

```text
1. INDEX 46 包 × 5 文档（README/WHY/WHAT/HOW/PROOF）齐备
2. 每个 WHAT.md 标题定义的 <PACKAGE>-NNN 命题 ID 在 PROOF.md 表格中有行
3. 每个 PROOF.md 落点引用的测试文件真实存在
4. requirements/ 下无 INDEX 外包目录
5. 每个包 README/WHY/WHAT 的 DEPENDS ON 引用集合 ⊆ INDEX 依赖骨架
```

设计要点：

- 包清单来源 = `requirements/README.md` 树入口链接 ∪ `archive/requirements-design/INDEX.md` 表格；
  两源必须一致（45 = 45）。
- 依赖骨架解析 `INDEX.md`「# 依赖骨架」后第一个 code block（87 edge）；cutover 后骨架迁入
  requirements/ 树时把解析源指向新位置（SPLIT@cutover）。
- DEPENDS ON 解析只认声明行（行首 `DEPENDS ON` / 标题 `## DEPENDS ON`）及其延续行，
  散文里的跨包引用不算声明；包自身名字不算自环。
- 两个 test() 分工：`已迁移包结构一致` 现在必须绿（每新包落地也须绿，删 PROOF 行立即红）；
  `全量迁移状态` 迁移中途红、cutover 后绿。
- 只读结构事实，不 import 其它包目录，不依赖 dist（无构建新鲜度耦合）。

### 2. spec gate（`scripts/checks/spec.mjs` + `scripts/checks/spec-rules.mjs`）

`node scripts/check.mjs` 中第 1 个 wired gate。检查当前 archive/docs/changes 世界的条款治理：

```text
条款唯一（同 ID 双定义红）、引用可解析（悬空/未知前缀红）
Change 文件不得定义正式 Clause（formalClauseDefinitionHeadings）
archive/changes/ 三目录存在；同一工作项不并存于多目录
废止路径 archive/docs/proposal|status 不得被引用（legacyWorkflowPathReferences）
当前规范/实现不得依赖 archive/changes/proposed|completed 历史（changeDependencyReferences）
archive/docs/README.md 导航精确覆盖正式文件（navigationProblems）
```

纯规则抽在 `spec-rules.mjs`（lib，不直接 spawn），回归测试已移入本包
`tests/spec-rules.test.mjs`（自 `tests/unit/verify/` 迁移，import 深度不变）。

### 3. 树入口导航

`requirements/README.md` 是 46 包树入口（迁移期与 `archive/docs/README.md` 同构承担导航）；导航文件
只路由不定义条款（REQUIREMENT-SYSTEM-005/007）。

### 4. change-lifecycle（`tests/change-lifecycle.test.mjs`）

锁 WHAT-015 AGENTS.md 小修复豁免、WHAT-014 blocker 四步原文、WHAT-013 Completed 不作当前依据。
live `changes/active/` 若存在必须声明 origin 标题。不扫 `archive/changes/`，不读正文推断生命周期。

## 依赖与理由

- INDEX 骨架：`requirement-system → 无`。理由：所有权元规则不消费任何产品 guarantee；它
  定义「谁拥有什么」，不依赖被治理对象的语义。`verification-system` 消费本包（其命题需要
  「每 assertion 一个 owner」与「WHAT 是唯一合同」才能定义证明资格）。

## 运行与验证

```text
node --test requirements/requirement-system/tests/meta-verifier.test.mjs
node --test requirements/requirement-system/tests/spec-rules.test.mjs
node --test requirements/requirement-system/tests/change-lifecycle.test.mjs
node scripts/check.mjs          # 集成时由 lead 跑；spec gate 是本包机制
node requirements/verification-system/tests/run.mjs         # 集成时由 lead 跑；自动发现 requirements/**/*.test.mjs
```

meta-verifier 迁移中途红是预期（见测试头注释）；结束时三条命令 + check + unit 全绿。

## 历史与弃权

| 来源 | 裁决 | 记录在哪 |
|---|---|---|
| GOV-001（当前规范只位于 archive/docs/ 5 层） | HOW/GARBAGE：当前文件层级是迁移载体；cutover 后由 requirements/ 树取代 | 本 HOW 实现模型 §3；WHAT 不收录 |
| GOV-003（执行链 what→shape→how→code） | HOW：流程描述并入 WHAT-009 层归属的动机，不另立条款 | WHAT-009 |
| GOV-004（滚动基线：当前 docs+实现=当前系统） | HOW/GARBAGE：迁移期过渡概念；「不得从 Completed 解释当前语义」的 live 面已并入 WHAT-010 | WHAT-010 |
| GOV-010（clean break：archive/docs/proposal|status 废止） | HOW/GARBAGE：一次性迁移历史；live 面（废止路径不引用）并入 WHAT-010 | WHAT-010；本 HOW |
| 当前 Clause ID 前缀表（ARCH/GOV/…/VERIFY） | HOW：迁移载体；ID 稳定性原则本身是 WHAT-008 | WHAT-008 |
| `archive/docs/README.md` 导航职责 | HOW：当前由 archive/docs/README.md 承担，cutover 后由 requirements/README.md 承接 | 本 HOW §3 |
| change 正文内容合同（Active 字段白名单、Completed 原文冻结） | Completed 不作当前依据 + live Active origin 标题：`tests/change-lifecycle.test.mjs`；Active 字段白名单 / 原文不被反向改写仍人工（GOV 禁止检查器读正文推断生命周期） | WHAT-013；PROOF L20 |
| blocker 协议（GOV-009） | WHAT-014 四步原文由 `tests/change-lifecycle.test.mjs` 锁定；一次实现是否真正停下仍人工 | WHAT-014；PROOF L21 |
| 直接闭环小变更（GOV-012） | AGENTS.md 豁免句由 `tests/change-lifecycle.test.mjs` 锁定 | WHAT-015；PROOF L22 |
| 旧 36 工作集 / Proposal 生命周期本身 | 不迁入 WHAT：Git 记历史，未来树只表达当前接受真理（HANDOFF §25.8） | 本 HOW |

## 遗留风险 / cutover 待办

- **SPLIT@cutover**：meta-verifier 的依赖骨架解析源从 `archive/requirements-design/INDEX.md` 迁入
  requirements/ 树；`archive/docs/README.md` 导航职责移交 `requirements/README.md`；archive/docs/changes
  归档后 spec gate 的 archive/docs/changes 检查面整体重写为 requirements/ 树治理。
- **GAP**：WHAT-013 Active 原文冻结 / 正文白名单仍人工（`requirements/GAP.md` GAP-003 PARTIAL）；
  WHAT-014/015 机器面已由 `tests/change-lifecycle.test.mjs` 承接（GAP-004/005 CLOSED）。
- 命题 ID 前缀规则（`<PACKAGE>-NNN` = 大写包名）由 meta-verifier 强制；若后续裁决改用其它
  格式，需同步改 verifier 与全部 WHAT（属本包独立变化）。
