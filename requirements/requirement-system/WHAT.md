# requirement-system — WHAT

本文件是 `requirement-system` 的**唯一 normative 合同**。WHY 与 HOW 非 normative。

---

## REQUIREMENT-SYSTEM-001: 唯一语义所有权

当前系统接受的每一条产品规范命题（normative proposition）在任意时刻恰有一个 package owner。仓库中严禁存在无 owner、双重 owner 或互相矛盾的规范权威。当语义矛盾发生时，必须能够通过 owner 裁决唯一归属。

## REQUIREMENT-SYSTEM-002: 包身份独立于物理布局

package 的语义身份由包名唯一确定。包的物理目录布局、文件名组织与元数据表现格式属于 HOW 的实现范畴，其整体更换不得改变任何包所拥有的 WHAT 语义合同。

## REQUIREMENT-SYSTEM-003: 全部包同时为真

已接受（accepted）仓库状态中所有 package 的规范命题必须同时为真。包之间的依赖关系表示保障消费（guarantee consumption），不表示执行优先级、时序冻结或规范覆盖（override）。被依赖的包不因被消费而获得对下游包命题的裁决权威。

## REQUIREMENT-SYSTEM-004: 每个 executable proof 恰一个 owner

仓库中每条新世界可执行断言（assertion）恰有一个 package owner。共享测试运行器或检查脚手架可以服务于多个包，但断言级的所有权严禁双重归属。

## REQUIREMENT-SYSTEM-005: 无裸规范权威

跨包治理与元系统规则本身必须被明确的 package 拥有。纯路由与导航文件（如 README、AGENTS、CHANGELOG）严禁定义正式规范条款；所有正式规范只存在于所属包的规范文档中。

## REQUIREMENT-SYSTEM-006: 索引完整性

`requirements/` 规范树只能包含 `requirements/INDEX.md` 所列出的包目录。所有合法包必须完整包含 `{WHY,WHAT,HOW}.md` 与 `tests/` 目录；规范树入口（`requirements/README.md`）与 `requirements/INDEX.md` 必须命名完全相同的包集合。

## REQUIREMENT-SYSTEM-007: WHAT 是唯一 normative 合同

包目录下的 `WHAT.md` 是该包对外声明的唯一 normative 语义合同。`WHY.md` 仅解释设计理由与动机，`HOW.md` 仅说明架构机制与测试落点映射，两者均不具备规范定义权。任何非 `WHAT.md` 文件中的散文均不得作为产品合规性裁决依据。

## REQUIREMENT-SYSTEM-008: 条款 ID 唯一性与稳定性

正式命题 ID 只能在其唯一 owner 包的 `WHAT.md` 标题中进行定义；其余所有文档只能对其进行精确引用。ID 引用必须可机械解析且无悬空前缀。命题定义在包内调整时必须保持 ID 稳定；命题废止后其编号永久空缺，不得复用。

## REQUIREMENT-SYSTEM-009: 条款层归属

产品可观察行为、语义和不变量权威归属于 WHAT；所有权划分归属于 SHAPE；算法实现与数据流转归属于 HOW；证明责任归属于 PROOF；演进动机归属于 WHY。当代码实现与规范发生冲突时，一律以 WHAT 为最高裁决标准。

## REQUIREMENT-SYSTEM-010: 废止路径不引用与实现不依赖历史

仓库实现与当前规范不得引用已废止的工作流路径或归档历史文件作为当前产品行为的语义依据。历史工作记录仅作审计溯源用途，不得作为影子规范存在。

## REQUIREMENT-SYSTEM-011: 用户所有权与启动授权

未来材料与延期提案归存于 `proposals/` 目录并由用户全权管理。Agent 严禁自行扫描自选工作、擅自修改批准范围或重新进行人工裁决。当用户明确指示启动指定提案时即获得充分授权；若在实施中发现正式规范冲突，必须记录 blocker 并上报用户，严禁私自裁决变更范围。

## REQUIREMENT-SYSTEM-012: 单文件 Change 约束

若启用变更管理流程，每项独立变更必须遵循单文件生命周期管理，严禁建立平行的提案、状态或裁决数据库。变更编号（如 `CHG-NNN`）是工作跟踪标识，不得与产品规范条款 ID 混淆。

## REQUIREMENT-SYSTEM-013: Active 与 Completed 边界

活动中的变更记录（Active）仅保留冻结的原始提案、来源说明、有限的剩余工作、客观 blocker 以及经用户批准的修订案，严禁包含未经批准的设计扩充或无意义的进度流水。已完成的记录（Completed）永久保存原文与 Final outcome，不解释当前产品行为。

## REQUIREMENT-SYSTEM-014: 矛盾与 blocker 协议

在实现已批准范围的过程中，若发现正式规范存在内在矛盾、底层能力缺失或客观不可实施条件：停止受影响的产品语义修改 → 在 Active 的 Blockers 追加事实与证据 → 报告用户 → 用户修订范围时追加 Amendment 继续，严禁实现者私自选边裁决。

## REQUIREMENT-SYSTEM-015: 直接闭环小变更

对于不改变正式规范的局部修复、重构、测试补充或文档排版，允许在单次提交内原子化对齐规范、代码与证明，无需额外创建变更生命周期文件；但严禁借机夹带未经批准的规范变更或弱化正式条款。

## REQUIREMENT-SYSTEM-016: 依赖声明 ⊆ 骨架

每个包在其文档中声明的 `DEPENDS ON` 依赖关系集合，必须严格是 `requirements/INDEX.md` 依赖骨架定义的子集，严禁声明骨架中不存在的依赖边。跨包引用必须使用包名，严禁直接复制其他包的命题内容。

## REQUIREMENT-SYSTEM-017: meta-verifier 机器执行

仓库必须维护可执行的机器验证器（`meta-verifier`），对 `requirements/` 全树进行机械化扫描，断言文档齐备性、命题证明落点完整性、测试文件物理存在性、包目录封闭性以及依赖声明合法性。

## REQUIREMENT-SYSTEM-018: 可执行证明双向可追溯

`requirements/**/tests/**/*.test.mjs` 中的每个有效可执行测试用例（`test()` 或 `t.test()`），必须在其标题显式声明恰好一个当前合法的命题标签 `WHAT[<PACKAGE-NNN>]`。每个有效的 WHAT 命题必须至少被一个处于激活状态的非 skip、非 todo 测试用例所证明。测试与规范之间严禁存在悬空引用、多重 primary 归属或无标签的孤立测试。

## REQUIREMENT-SYSTEM-019: migration ledger 门禁与状态机完整性

`scripts/checks/migration-ledger.json` 的 DAG 与节点状态机必须由 `scripts/checks/migration-ledger.mjs` 的机械门禁守护，禁止 11 类非法状态：PENDING 声明成功证据（evidence 含 verified/complete/GREEN 大小写不敏感）、READY 缺 owner 图（publishes/consumes/depends_on/production_callers 全空）、READY 缺证明门禁（proofs/architecture_gates 全空）、DONE 结果仍 PENDING、分类/结果不兼容（KEEP→PROVEN-KEEP、DELETE→DELETED、MOVE/SPLIT/ADAPTER→CUTOVER、COMPOSITION-ROOT→CUTOVER|PROVEN-KEEP）、DONE 缺实现提交或提交非 HEAD 祖先（40 位哈希且 git merge-base --is-ancestor HEAD）、DONE 缺生产/测试变更（touched_paths 非空且含 src/或*.fs）、DONE 缺 proofs、DONE 缺 architecture_gates、closure 记录非法（closure 边目标非 DONE）、仅覆盖无 owner 图（仅 coverage_tags 无 owner 图）、基线/抑制增长（deadcode-baseline.json / provider-prose-ownership-baseline.json 不得无显式 admission 而增长）。所有门禁变更必须同步 WHY/WHAT/HOW/GAP 与自测，PENDING 证据、READY 条件、DONE 闭环、分类兼容、提交祖先、变更路径、证明门禁、闭合依赖、覆盖归属、基线冻结均需可红可绿的独立落点。
