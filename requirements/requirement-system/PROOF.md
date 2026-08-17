# PROOF：requirement-system 测试落点表

落点类型：`MOVE`（物理移入本包 tests/）/ `REUSE`（留原处，记锚点）/ `NEW`（新写）/
`GATE`（静态门禁，`node scripts/check.mjs` 集成执行）。运行命令均为仓库根目录相对。每条 WHAT 命题恰一行。

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| REQUIREMENT-SYSTEM-001 | `requirements/requirement-system/tests/meta-verifier.test.mjs`（test: meta verifier: 全量迁移状态——无 INDEX 外目录；每包唯一目录）；`requirements/requirement-system/tests/spec-rules.test.mjs`（formalClauseDefinitionHeadings——ID 唯一检测） | NEW | node --test requirements/requirement-system/tests/meta-verifier.test.mjs |
| REQUIREMENT-SYSTEM-002 | `requirements/requirement-system/tests/meta-verifier.test.mjs`（结构检查只认包名 + 5 份文档，不要求未裁决的 manifest 格式） | NEW | node --test requirements/requirement-system/tests/meta-verifier.test.mjs |
| REQUIREMENT-SYSTEM-003 | `requirements/requirement-system/tests/meta-verifier.test.mjs`（test: 全量迁移状态——INDEX 48 包 × 5 文档同时齐备才绿） | NEW | node --test requirements/requirement-system/tests/meta-verifier.test.mjs |
| REQUIREMENT-SYSTEM-004 | `requirements/requirement-system/tests/meta-verifier.test.mjs`（每个 WHAT 命题 ID 在 PROOF.md 至少一行）；`requirements/requirement-system/tests/spec-rules.test.mjs`（formalClauseDefinitionHeadings——同 ID 二次定义可识别） | NEW | node --test requirements/requirement-system/tests/meta-verifier.test.mjs |
| REQUIREMENT-SYSTEM-005 | `requirements/requirement-system/tests/spec-rules.test.mjs`（formalClauseDefinitionHeadings：路由/Change 文件不得定义正式条款） | MOVE | node --test requirements/requirement-system/tests/spec-rules.test.mjs |
| REQUIREMENT-SYSTEM-006 | `requirements/requirement-system/tests/meta-verifier.test.mjs`（test: 全量迁移状态——树入口与 INDEX 同一包集 + 无外目录） | NEW | node --test requirements/requirement-system/tests/meta-verifier.test.mjs |
| REQUIREMENT-SYSTEM-007 | `requirements/requirement-system/tests/meta-verifier.test.mjs`（命题 ID 只从 WHAT 标题提取，树入口只导航不定义）；`requirements/requirement-system/tests/spec-rules.test.mjs`（navigationProblems——README 精确导航） | NEW | node --test requirements/requirement-system/tests/meta-verifier.test.mjs |
| REQUIREMENT-SYSTEM-008 | `requirements/requirement-system/tests/spec-rules.test.mjs`（unknownClauseReferences / clauseReferences / clauseDefinitionHeadings / formalClauseDefinitionHeadings / navigationProblems 五组断言） | MOVE | node --test requirements/requirement-system/tests/spec-rules.test.mjs |
| REQUIREMENT-SYSTEM-009 | `requirements/requirement-system/tests/spec-rules.test.mjs`（formalClauseDefinitionHeadings：Change 文件禁止定义正式 Clause）；人工评审承接（本文件 人工评审承接表） | MOVE | node --test requirements/requirement-system/tests/spec-rules.test.mjs |
| REQUIREMENT-SYSTEM-010 | `scripts/checks/spec.mjs`（archivePathReferences：全仓零归档树引用；legacyWorkflowPathReferences：废止路径）；`tests/spec-rules.test.mjs`（规则单测） | GATE + MOVE | node scripts/check.mjs / node --test requirements/requirement-system/tests/spec-rules.test.mjs |
| REQUIREMENT-SYSTEM-011 | `tests/spec-rules.test.mjs`（changeDependencyReferences：proposed 非当前依赖，规则单测）；人工评审承接（本文件 人工评审承接表：Agent 未经用户指定启动 Proposed） | MOVE | node --test requirements/requirement-system/tests/spec-rules.test.mjs |
| REQUIREMENT-SYSTEM-012 | `tests/spec-rules.test.mjs`（formalClauseDefinitionHeadings：CHG-001 与产品条款区分）；`scripts/checks/spec.mjs`（正式定义只在 WHAT.md） | MOVE + GATE | 分别 node --test / node scripts/check.mjs |
| REQUIREMENT-SYSTEM-013 | `tests/change-lifecycle.test.mjs`（Completed 不作当前依据；live Active 必须声明冻结 origin）；Active 原文冻结 / 正文白名单仍人工评审（本文件 人工评审承接表） | NEW + 人工 | node --test requirements/requirement-system/tests/change-lifecycle.test.mjs |
| REQUIREMENT-SYSTEM-014 | `tests/change-lifecycle.test.mjs`（WHAT-014 四步 blocker 协议；删步即红） | NEW | node --test requirements/requirement-system/tests/change-lifecycle.test.mjs |
| REQUIREMENT-SYSTEM-015 | `tests/change-lifecycle.test.mjs`（AGENTS.md「普通小型修复不要求创建 Change」；删句即红） | NEW | node --test requirements/requirement-system/tests/change-lifecycle.test.mjs |
| REQUIREMENT-SYSTEM-016 | `requirements/requirement-system/tests/meta-verifier.test.mjs`（每个包 README/WHY/WHAT 的 DEPENDS ON ⊆ INDEX 骨架断言） | NEW | node --test requirements/requirement-system/tests/meta-verifier.test.mjs |
| REQUIREMENT-SYSTEM-017 | `requirements/requirement-system/tests/meta-verifier.test.mjs`（本测试自身即机器执行；删已存在包 PROOF 行必红） | NEW | node --test requirements/requirement-system/tests/meta-verifier.test.mjs |
| REQUIREMENT-SYSTEM-018 | `tests/requirement-trace.test.mjs::WHAT[REQUIREMENT-SYSTEM-018] scanner skips strings, comments, and template literals`; `tests/requirement-trace.test.mjs::WHAT[REQUIREMENT-SYSTEM-018] scanner recognizes test.skip / test.todo / t.test forms`; `tests/requirement-trace.test.mjs::WHAT[REQUIREMENT-SYSTEM-018] template titles with ${} nesting are parsed`; `tests/requirement-trace.test.mjs::WHAT[REQUIREMENT-SYSTEM-018] scanner rejects duplicate, non-leading, and missing primary tags`; `tests/requirement-trace.test.mjs::WHAT[REQUIREMENT-SYSTEM-018] scanner ignores declarations, constructors, and methods named test`; `tests/requirement-trace.test.mjs::WHAT[REQUIREMENT-SYSTEM-018] scanner sees nested test calls in template expressions`; `tests/requirement-trace.test.mjs::WHAT[REQUIREMENT-SYSTEM-018] scanner skips regex literals containing quotes`; `tests/requirement-trace.test.mjs::WHAT[REQUIREMENT-SYSTEM-018] graph closes exact proof anchors and rejects stale anchors`; `tests/requirement-trace.test.mjs::WHAT[REQUIREMENT-SYSTEM-018] buildTraceGraph classifies orphan / unknown / multi-primary / unproved`; `scripts/checks/requirement-trace.mjs` (`--report`, `--package`, `--strict`, `--explain`) | NEW | `node --test requirements/requirement-system/tests/requirement-trace.test.mjs`; `node scripts/checks/requirement-trace.mjs --strict=requirement-system` |

## 人工评审承接表（生命周期机制停用后仍有效的过程检查）

| 检查 | 失败含义（对应条款） |
|---|---|
| Agent 未经用户指定启动 Proposed | REQUIREMENT-SYSTEM-011 |
| Active 原文被反向改写 | REQUIREMENT-SYSTEM-013 |
| Active 成为目标产品语义的唯一来源 | REQUIREMENT-SYSTEM-007/013 |
| Completed 被用作当前实现依据 | REQUIREMENT-SYSTEM-010 |
| Active 保存进度流水或未经批准的新设计 | REQUIREMENT-SYSTEM-013 |

## 语义 anchor

`scripts/checks/semantic-anchors.mjs` 是角色/工具语义锚点 catalog（归属各产品包，如
cognitive-environment / office-capability / action-affordance）。本包是 META 包，**无
anchor id**；本包的机器事实由 meta-verifier + spec-rules 承担。

## SPLIT@cutover（已闭合 2026-08-14）

- 依赖骨架解析源已迁至 `requirements/INDEX.md`；meta-verifier 已同步。
- 树导航职责已移交 `requirements/README.md`。
- spec gate 已重写为 requirements/ 树治理（归档树检查面整体替换）。
- WHAT-013/014/015：`tests/change-lifecycle.test.mjs`（GAP-003 PARTIAL / GAP-004 CLOSED / GAP-005 CLOSED）；Active 原文冻结仍人工。
- PROOF-MAP「顶层 3 文件」归属分歧（verdict-feed / domain.meta / guide-contract 的
  assertion 级 owner）：见 `requirements/verification-system/PROOF.md`。
