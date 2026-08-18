# HOW：requirement-system 的实现模型与约束

非 normative。本文件解释机制怎么工作、历史怎么来的、什么被弃权。

## 实现模型

本包无 runtime 源码（META 包正确形态，证据 =
`AGENTS.md` + `scripts/checks` + CI）。机制由四块组成：

### 1. meta-verifier（`tests/meta-verifier.test.mjs`）

`node --test requirements/requirement-system/tests/meta-verifier.test.mjs`

扫描 `requirements/` 全树，五个断言：

```text
1. INDEX 48 包 × 3 文档（WHY/WHAT/HOW）与 tests/ 齐备
2. 每个 WHAT.md 标题定义的 <PACKAGE>-NNN 命题 ID 在 HOW.md 表格中有行
3. 每个 HOW.md 落点引用的测试文件真实存在
4. requirements/ 下无 INDEX 外包目录
5. 每个包 WHY/WHAT/HOW 的 DEPENDS ON 引用集合 ⊆ INDEX 依赖骨架
```

设计要点：

- 包清单来源 = `requirements/README.md` 树入口链接 ∪ `requirements/INDEX.md` 表格
  （2026-08-14 cutover 迁入）；
  两源必须一致。
- 依赖骨架解析 `INDEX.md`「# 依赖骨架」后第一个 code block（当前 110 edge）；cutover 后骨架迁入
  requirements/ 树时把解析源指向新位置（SPLIT@cutover）。
- DEPENDS ON 解析只认声明行（行首 `DEPENDS ON` / 标题 `## DEPENDS ON`）及其延续行，
  散文里的跨包引用不算声明；包自身名字不算自环。
- 两个 test() 分工：`已迁移包结构一致` 现在必须绿（每新包落地也须绿，删 PROOF 行立即红）；
  `全量迁移状态` 迁移中途红、cutover 后绿。
- 只读结构事实，不 import 其它包目录，不依赖 dist（无构建新鲜度耦合）。

### 2. spec gate（`scripts/checks/spec.mjs` + `scripts/checks/spec-rules.mjs`）

`node scripts/check.mjs` 中第 1 个 wired gate。2026-08-14 cutover 后检查
requirements/ 树治理与归档脱离合同：

```text
正式条款定义只在 package WHAT.md（重复定义 / 越权定义红）
已知前缀的条款引用必须可解析（悬空红）
已删归档树的路径引用 = 死引用，红（archivePathReferences）
废止工作流路径不得被引用（legacyWorkflowPathReferences）
本地 Markdown 链接必须存在（markdownLocalLinks）
```

纯规则抽在 `spec-rules.mjs`（lib，不直接 spawn），回归测试已移入本包
`tests/spec-rules.test.mjs`（自 `tests/unit/verify/` 迁移，import 深度不变）。

### 3. requirement-trace（`scripts/lib/requirement-trace.mjs` + `scripts/checks/requirement-trace.mjs`）

`findTestFiles` 的扫描宇宙是 `requirements/**/tests/**/*.test.mjs`，包含其下的
`e2e/` 与 `integration/`；support/helper/fixture 只有在自身文件名是 `*.test.mjs` 且确实
调用 `test()` / `t.test()` 时才进入 call-site 图。scanner 先做 token 化：注释、字符串、
正则 literal、template 静态正文被跳过，`${ ... }` 表达式递归 token 化；只识别
`test()`、`test.only()`、`test.fails()`、`test.skip()`、`test.todo()`、`t.test()`、
`t.test.only()`、`t.test.fails()`、`t.test.skip()`、`t.test.todo()`。
`describe`、hook、alias、函数/类声明、构造器调用与 method body 不是 proof case。每个 call 都记录
file/line/title/state/WHAT ids/anchor；首个 title token 非字符串或模板时也记录为 orphan，而不是静默漏掉。

WHAT tag 只认 title 开头的 `WHAT[<PREFIX-NNN>]`，完整 ID 必须能在 WHAT.md 唯一定义中解析。
同一 title 的重复 tag 仍是 multi-primary；skip/todo 需要 tag 但不计入 active proof。graph
输出 `edges`（test → WHAT）与 `proofEdges`（PROOF exact anchor → test），供 gate 与
`--explain` 共用，不重复实现 parser。

PROOF 解析保留 meta-verifier 的 WHAT→PROOF row 结构检查；只有明确 `file.test.mjs::exact
anchor` 的 executable edge 才要求 call-site 精确闭合。edge 必须同时满足文件、line/title、
active state 与 WHAT ID 相同；不存在、歧义、skip/todo、WHAT mismatch 统一进入
`TRACE_DANGLING_PROOF`。裸文件、命令、人工证据不被猜成 wildcard test edge。

### 4. 树入口导航

`requirements/README.md` 是 48 包树入口（2026-08-14 cutover 后承担导航）；导航文件
只路由不定义条款（REQUIREMENT-SYSTEM-005/007）。

### 5. change-lifecycle（`tests/change-lifecycle.test.mjs`）

锁 WHAT-015 AGENTS.md 小修复豁免、WHAT-014 blocker 四步原文、WHAT-013 Completed 不作当前依据。
live `changes/active/` 若存在必须声明 origin 标题。不扫归档 changes 树，不读正文推断生命周期。
WHAT-013 Active 冻结 origin 边界 + 段白名单 + 禁止 progress/commit/code-snapshot 段由
`activeBodyViolations` 纯验证器机械承接；跨版本原文不被反向改写由
`frozenOriginViolations(before, after)` 纯验证器承接（均由 `scripts/checks/spec-rules.mjs` 导出，
不扫 `changes/active/`，不从正文推断生命周期）。

## 依赖与理由

- INDEX 骨架：`requirement-system → 无`。理由：所有权元规则不消费任何产品 guarantee；它
  定义「谁拥有什么」，不依赖被治理对象的语义。`verification-system` 消费本包（其命题需要
  「每 assertion 一个 owner」与「WHAT 是唯一合同」才能定义证明资格）。

## 运行与验证

```text
node --test requirements/requirement-system/tests/meta-verifier.test.mjs
node --test requirements/requirement-system/tests/spec-rules.test.mjs
node --test requirements/requirement-system/tests/requirement-trace.test.mjs
node scripts/checks/requirement-trace.mjs --strict=requirement-system
node scripts/checks/requirement-trace.mjs --report
node scripts/checks/requirement-trace.mjs --explain=requirements/requirement-system/tests/requirement-trace.test.mjs:16
node scripts/check.mjs          # 集成时由 lead 跑；spec gate 是本包机制
node requirements/verification-system/tests/run.mjs         # 集成时由 lead 跑；自动发现 requirements/**/*.test.mjs
```

meta-verifier 迁移中途红是预期（见测试头注释）；结束时三条命令 + check + unit 全绿。

## 历史与弃权

| 来源 | 裁决 | 记录在哪 |
|---|---|---|
| GOV-001（当前规范只位于旧 5 层 docs） | HOW/GARBAGE：当前文件层级是迁移载体；cutover 后由 requirements/ 树取代 | 本 HOW 实现模型 §4；WHAT 不收录 |
| GOV-003（执行链 what→shape→how→code） | HOW：流程描述并入 WHAT-009 层归属的动机，不另立条款 | WHAT-009 |
| GOV-004（滚动基线：当前 docs+实现=当前系统） | HOW/GARBAGE：迁移期过渡概念；「不得从 Completed 解释当前语义」的 live 面已并入 WHAT-010 | WHAT-010 |
| GOV-010（clean break：旧 proposal/status 目录废止） | HOW/GARBAGE：一次性迁移历史；live 面（废止路径不引用）并入 WHAT-010 | WHAT-010；本 HOW |
| 当前 Clause ID 前缀表（ARCH/GOV/…/VERIFY） | HOW：迁移载体；ID 稳定性原则本身是 WHAT-008 | WHAT-008 |
| 旧 docs/README.md 导航职责 | HOW：已由 requirements/README.md 承接（2026-08-14 cutover） | 本 HOW §4 |
| change 正文内容合同（Active 字段白名单、Completed 原文冻结） | Completed 不作当前依据 + live Active origin 标题 + `activeBodyViolations` / `frozenOriginViolations` 纯验证器（冻结 origin 边界、跨版本原文不变、段白名单、禁止 progress·commit·code-snapshot 段）：`tests/change-lifecycle.test.mjs`；验证器接受纯文本输入，不扫目录 | WHAT-013；PROOF L20 |
| blocker 协议（GOV-009） | WHAT-014 四步原文由 `tests/change-lifecycle.test.mjs` 锁定；一次实现是否真正停下仍人工 | WHAT-014；PROOF L21 |
| 直接闭环小变更（GOV-012） | AGENTS.md 豁免句由 `tests/change-lifecycle.test.mjs` 锁定 | WHAT-015；PROOF L22 |
| 旧 36 工作集 / Proposal 生命周期本身 | 不迁入 WHAT：Git 记历史，未来树只表达当前接受真理 | 本 HOW |

## 遗留风险 / cutover 待办

- **已闭合（2026-08-14）**：meta-verifier 依赖骨架解析源迁入 `requirements/INDEX.md`；
  `requirements/README.md` 承接树导航；spec gate 已重写为 requirements/ 树治理。
- **已闭合（2026-08-17）**：WHAT-013 Active 冻结 origin 边界、跨版本原文不变、段白名单与
  progress/commit/code-snapshot 禁止段由 `activeBodyViolations` + `frozenOriginViolations`
  纯验证器机械承接；不要求启用 `changes/active/`。WHAT-014/015 机器面已由
  `tests/change-lifecycle.test.mjs` 承接（GAP-003/004/005 CLOSED）。
- 命题 ID 前缀规则（`<PACKAGE>-NNN` = 大写包名）由 meta-verifier 强制；若后续裁决改用其它
  格式，需同步改 verifier 与全部 WHAT（属本包独立变化）。

## 边界（DOES NOT OWN）

- 什么证据技术足够证明某个产品事实 → `verification-system`。
- 任一产品领域事实（prompt/journal/review/host/…）→ 各对应包。
- Git/PR 历史沉积、Proposal 生命周期本身；未来 requirements/ 树只表达当前接受真理。
- 当前 Clause ID 前缀表、旧五层 docs 文件层级 → 当前 HOW（历史载体，2026-08-14 归档）。

## 验证与测试落点

落点类型：`MOVE`（物理移入本包 tests/）/ `REUSE`（留原处，记锚点）/ `NEW`（新写）/
`GATE`（静态门禁，`node scripts/check.mjs` 集成执行）。运行命令均为仓库根目录相对。每条 WHAT 命题恰一行。

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| REQUIREMENT-SYSTEM-001 | `requirements/requirement-system/tests/meta-verifier.test.mjs`（test: meta verifier: 全量迁移状态——无 INDEX 外目录；每包唯一目录）；`requirements/requirement-system/tests/spec-rules.test.mjs`（formalClauseDefinitionHeadings——ID 唯一检测） | NEW | node --test requirements/requirement-system/tests/meta-verifier.test.mjs |
| REQUIREMENT-SYSTEM-002 | `requirements/requirement-system/tests/meta-verifier.test.mjs`（结构检查只认包名 + 5 份文档，不要求未裁决的 manifest 格式） | NEW | node --test requirements/requirement-system/tests/meta-verifier.test.mjs |
| REQUIREMENT-SYSTEM-003 | `requirements/requirement-system/tests/meta-verifier.test.mjs`（test: 全量迁移状态——INDEX 48 包 × 5 文档同时齐备才绿） | NEW | node --test requirements/requirement-system/tests/meta-verifier.test.mjs |
| REQUIREMENT-SYSTEM-004 | `requirements/requirement-system/tests/meta-verifier.test.mjs`（每个 WHAT 命题 ID 在 HOW.md 至少一行）；`requirements/requirement-system/tests/spec-rules.test.mjs`（formalClauseDefinitionHeadings——同 ID 二次定义可识别） | NEW | node --test requirements/requirement-system/tests/meta-verifier.test.mjs |
| REQUIREMENT-SYSTEM-005 | `requirements/requirement-system/tests/spec-rules.test.mjs`（formalClauseDefinitionHeadings：路由/Change 文件不得定义正式条款） | MOVE | node --test requirements/requirement-system/tests/spec-rules.test.mjs |
| REQUIREMENT-SYSTEM-006 | `requirements/requirement-system/tests/meta-verifier.test.mjs`（test: 全量迁移状态——树入口与 INDEX 同一包集 + 无外目录） | NEW | node --test requirements/requirement-system/tests/meta-verifier.test.mjs |
| REQUIREMENT-SYSTEM-007 | `requirements/requirement-system/tests/meta-verifier.test.mjs`（命题 ID 只从 WHAT 标题提取，树入口只导航不定义）；`requirements/requirement-system/tests/spec-rules.test.mjs`（navigationProblems——README 精确导航） | NEW | node --test requirements/requirement-system/tests/meta-verifier.test.mjs |
| REQUIREMENT-SYSTEM-008 | `requirements/requirement-system/tests/spec-rules.test.mjs`（unknownClauseReferences / clauseReferences / clauseDefinitionHeadings / formalClauseDefinitionHeadings / navigationProblems 五组断言） | MOVE | node --test requirements/requirement-system/tests/spec-rules.test.mjs |
| REQUIREMENT-SYSTEM-009 | `requirements/requirement-system/tests/spec-rules.test.mjs`（formalClauseDefinitionHeadings：Change 文件禁止定义正式 Clause）；人工评审承接（本文件 人工评审承接表） | MOVE | node --test requirements/requirement-system/tests/spec-rules.test.mjs |
| REQUIREMENT-SYSTEM-010 | `scripts/checks/spec.mjs`（archivePathReferences：全仓零归档树引用；legacyWorkflowPathReferences：废止路径）；`tests/spec-rules.test.mjs`（规则单测） | GATE + MOVE | node scripts/check.mjs / node --test requirements/requirement-system/tests/spec-rules.test.mjs |
| REQUIREMENT-SYSTEM-011 | `tests/spec-rules.test.mjs`（changeDependencyReferences：proposed 非当前依赖，规则单测）；人工评审承接（本文件 人工评审承接表：Agent 未经用户指定启动 Proposed） | MOVE | node --test requirements/requirement-system/tests/spec-rules.test.mjs |
| REQUIREMENT-SYSTEM-012 | `tests/spec-rules.test.mjs`（formalClauseDefinitionHeadings：CHG-001 与产品条款区分）；`scripts/checks/spec.mjs`（正式定义只在 WHAT.md） | MOVE + GATE | 分别 node --test / node scripts/check.mjs |
| REQUIREMENT-SYSTEM-013 | `tests/change-lifecycle.test.mjs`（Completed 不作当前依据；live Active 必须声明冻结 origin；activeBodyViolations + frozenOriginViolations 纯验证器：冻结 origin 边界、跨版本原文不变、段白名单、禁止 progress/commit/code-snapshot 段） | NEW | node --test requirements/requirement-system/tests/change-lifecycle.test.mjs |
| REQUIREMENT-SYSTEM-014 | `tests/change-lifecycle.test.mjs`（WHAT-014 四步 blocker 协议；删步即红） | NEW | node --test requirements/requirement-system/tests/change-lifecycle.test.mjs |
| REQUIREMENT-SYSTEM-015 | `tests/change-lifecycle.test.mjs`（AGENTS.md「普通小型修复不要求创建 Change」；删句即红） | NEW | node --test requirements/requirement-system/tests/change-lifecycle.test.mjs |
| REQUIREMENT-SYSTEM-016 | `requirements/requirement-system/tests/meta-verifier.test.mjs`（每个包 README/WHY/WHAT 的 DEPENDS ON ⊆ INDEX 骨架断言） | NEW | node --test requirements/requirement-system/tests/meta-verifier.test.mjs |
| REQUIREMENT-SYSTEM-017 | `requirements/requirement-system/tests/meta-verifier.test.mjs`（本测试自身即机器执行；删已存在包 PROOF 行必红） | NEW | node --test requirements/requirement-system/tests/meta-verifier.test.mjs |
| REQUIREMENT-SYSTEM-018 | `tests/requirement-trace.test.mjs::WHAT[REQUIREMENT-SYSTEM-018] scanner skips strings, comments, and template literals`; `tests/requirement-trace.test.mjs::WHAT[REQUIREMENT-SYSTEM-018] scanner recognizes test.skip / test.todo / t.test forms`; `tests/requirement-trace.test.mjs::WHAT[REQUIREMENT-SYSTEM-018] template titles with ${} nesting are parsed`; `tests/requirement-trace.test.mjs::WHAT[REQUIREMENT-SYSTEM-018] scanner rejects duplicate, non-leading, and missing primary tags`; `tests/requirement-trace.test.mjs::WHAT[REQUIREMENT-SYSTEM-018] scanner ignores declarations, constructors, and methods named test`; `tests/requirement-trace.test.mjs::WHAT[REQUIREMENT-SYSTEM-018] scanner sees nested test calls in template expressions`; `tests/requirement-trace.test.mjs::WHAT[REQUIREMENT-SYSTEM-018] scanner skips regex literals containing quotes`; `tests/requirement-trace.test.mjs::WHAT[REQUIREMENT-SYSTEM-018] graph closes exact proof anchors and rejects stale anchors`; `tests/requirement-trace.test.mjs::WHAT[REQUIREMENT-SYSTEM-018] buildTraceGraph classifies orphan / unknown / multi-primary / unproved`; `scripts/checks/requirement-trace.mjs` (`--report`, `--package`, `--strict`, `--explain`) | NEW | `node --test requirements/requirement-system/tests/requirement-trace.test.mjs`; `node scripts/checks/requirement-trace.mjs --strict=requirement-system` |

### 人工评审承接表（生命周期机制停用后仍有效的过程检查）

- Agent 未经用户指定启动 Proposed → REQUIREMENT-SYSTEM-011
- Active 成为目标产品语义的唯一来源 → REQUIREMENT-SYSTEM-007/013
- Completed 被用作当前实现依据 → REQUIREMENT-SYSTEM-010
- Active 保存进度流水或未经批准的新设计 → REQUIREMENT-SYSTEM-013（进度流水段已机械承接；段内未经批准新设计仍人工）

### 语义 anchor

`scripts/checks/semantic-anchors.mjs` 是角色/工具语义锚点 catalog（归属各产品包，如
cognitive-environment / office-capability / action-affordance）。本包是 META 包，**无
anchor id**；本包的机器事实由 meta-verifier + spec-rules 承担。

### SPLIT@cutover（已闭合 2026-08-14）

- 依赖骨架解析源已迁至 `requirements/INDEX.md`；meta-verifier 已同步。
- 树导航职责已移交 `requirements/README.md`。
- spec gate 已重写为 requirements/ 树治理（归档树检查面整体替换）。
- WHAT-013/014/015：`tests/change-lifecycle.test.mjs`（GAP-003/004/005 CLOSED）。
- PROOF-MAP「顶层 3 文件」归属分歧（verdict-feed / domain.meta / guide-contract 的
  assertion 级 owner）：见 `requirements/verification-system/HOW.md`。
