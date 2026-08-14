# PROOF：requirement-system 测试落点表

落点类型：`MOVE`（从 tests/unit 物理移入）/ `REUSE`（留原处，记锚点与 SPLIT@cutover）/
`NEW`（新写）。运行命令均为仓库根目录相对。每条 WHAT 命题恰一行。

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| REQUIREMENT-SYSTEM-001 | `requirements/requirement-system/tests/meta-verifier.test.mjs`（test: meta verifier: 全量迁移状态——无 INDEX 外目录；每包唯一目录）；`requirements/requirement-system/tests/spec-rules.test.mjs`（formalClauseDefinitionHeadings——ID 唯一检测） | NEW | node --test requirements/requirement-system/tests/meta-verifier.test.mjs |
| REQUIREMENT-SYSTEM-002 | `requirements/requirement-system/tests/meta-verifier.test.mjs`（结构检查只认包名 + 5 份文档，不要求未裁决的 manifest 格式） | NEW | node --test requirements/requirement-system/tests/meta-verifier.test.mjs |
| REQUIREMENT-SYSTEM-003 | `requirements/requirement-system/tests/meta-verifier.test.mjs`（test: 全量迁移状态——INDEX 45 包 × 5 文档同时齐备才绿） | NEW | node --test requirements/requirement-system/tests/meta-verifier.test.mjs |
| REQUIREMENT-SYSTEM-004 | `requirements/requirement-system/tests/meta-verifier.test.mjs`（每个 WHAT 命题 ID 在 PROOF.md 至少一行）；`requirements/requirement-system/tests/spec-rules.test.mjs`（formalClauseDefinitionHeadings——同 ID 二次定义可识别） | NEW | node --test requirements/requirement-system/tests/meta-verifier.test.mjs |
| REQUIREMENT-SYSTEM-005 | `requirements/requirement-system/tests/spec-rules.test.mjs`（formalClauseDefinitionHeadings：路由/Change 文件不得定义正式条款） | MOVE | node --test requirements/requirement-system/tests/spec-rules.test.mjs |
| REQUIREMENT-SYSTEM-006 | `requirements/requirement-system/tests/meta-verifier.test.mjs`（test: 全量迁移状态——树入口与 INDEX 同一包集 + 无外目录） | NEW | node --test requirements/requirement-system/tests/meta-verifier.test.mjs |
| REQUIREMENT-SYSTEM-007 | `requirements/requirement-system/tests/meta-verifier.test.mjs`（命题 ID 只从 WHAT 标题提取，树入口只导航不定义）；`requirements/requirement-system/tests/spec-rules.test.mjs`（navigationProblems——README 精确导航） | NEW | node --test requirements/requirement-system/tests/meta-verifier.test.mjs |
| REQUIREMENT-SYSTEM-008 | `requirements/requirement-system/tests/spec-rules.test.mjs`（unknownClauseReferences / clauseReferences / clauseDefinitionHeadings / formalClauseDefinitionHeadings / navigationProblems 五组断言） | MOVE | node --test requirements/requirement-system/tests/spec-rules.test.mjs |
| REQUIREMENT-SYSTEM-009 | `requirements/requirement-system/tests/spec-rules.test.mjs`（formalClauseDefinitionHeadings：Change 文件禁止定义正式 Clause）；人工评审承接（archive/docs/proof/document-governance.md 人工评审表） | MOVE | node --test requirements/requirement-system/tests/spec-rules.test.mjs |
| REQUIREMENT-SYSTEM-010 | `requirements/requirement-system/tests/spec-rules.test.mjs`（legacyWorkflowPathReferences：废止路径；changeDependencyReferences：不依赖 proposed/completed 历史） | MOVE | node --test requirements/requirement-system/tests/spec-rules.test.mjs |
| REQUIREMENT-SYSTEM-011 | `requirements/requirement-system/tests/spec-rules.test.mjs`（changeDependencyReferences：proposed 非当前依赖）；人工评审承接（archive/docs/proof/document-governance.md 人工评审表：Agent 未经用户指定启动 Proposed） | MOVE | node --test requirements/requirement-system/tests/spec-rules.test.mjs |
| REQUIREMENT-SYSTEM-012 | `requirements/requirement-system/tests/spec-rules.test.mjs`（formalClauseDefinitionHeadings：CHG-001 与产品条款区分）；人工评审承接（spec.mjs 三目录存在 / 同路径不并存机制，check 集成时执行） | MOVE | node --test requirements/requirement-system/tests/spec-rules.test.mjs |
| REQUIREMENT-SYSTEM-013 | 人工评审承接（archive/docs/proof/document-governance.md 人工评审表：Active 原文被反向改写 / Completed 被用作当前实现依据）；机器落点 GAP@cutover | REUSE | 人工评审（无机器命令） |
| REQUIREMENT-SYSTEM-014 | 人工评审承接（archive/docs/proof/document-governance.md 人工评审表 + GOV-009 blocker 协议）；机器落点 GAP@cutover | REUSE | 人工评审（无机器命令） |
| REQUIREMENT-SYSTEM-015 | 人工评审承接（AGENTS.md 文档生命周期节「普通小型修复不要求自动创建 Change」）；机器落点 GAP@cutover | REUSE | 人工评审（无机器命令） |
| REQUIREMENT-SYSTEM-016 | `requirements/requirement-system/tests/meta-verifier.test.mjs`（每个包 README/WHY/WHAT 的 DEPENDS ON ⊆ INDEX 骨架断言） | NEW | node --test requirements/requirement-system/tests/meta-verifier.test.mjs |
| REQUIREMENT-SYSTEM-017 | `requirements/requirement-system/tests/meta-verifier.test.mjs`（本测试自身即机器执行；删已存在包 PROOF 行必红） | NEW | node --test requirements/requirement-system/tests/meta-verifier.test.mjs |

## 语义 anchor

`scripts/checks/semantic-anchors.mjs` 是角色/工具语义锚点 catalog（归属各产品包，如
cognitive-environment / office-capability / action-affordance）。本包是 META 包，**无
anchor id**；本包的机器事实由 meta-verifier + spec-rules 承担。

## SPLIT@cutover 清单

- 依赖骨架解析源：`archive/requirements-design/INDEX.md` → requirements/ 树新权威位置
  （meta-verifier 同步迁移）。
- `archive/docs/README.md` 导航职责 → `requirements/README.md`。
- spec gate 的 archive/docs/changes 检查面 → requirements/ 树治理（archive/docs/changes 归档后整体重写）。
- WHAT-013/014/015 机器落点：change-lifecycle verifier（GAP@cutover 补；聚合台账见 `requirements/GAP.md` GAP-003/004/005）。
- PROOF-MAP「顶层 3 文件」归属分歧（verdict-feed / domain.meta / guide-contract 的
  assertion 级 owner）：见 `requirements/verification-system/PROOF.md`，cutover 按断言复核。
