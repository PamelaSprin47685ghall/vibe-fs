# external-investigation — HOW

## 架构与实现机制

1. **角色隔离与权限受控**：
   - 外部网络访问能力（如 `stealth-browser-mcp`）仅对 Browser 角色开放，其他所有角色均受限拦截。
   - 权限矩阵由 `capability-enforcement` 与 `AgentProgram.fs` 实施硬件级隔离。

2. **Role Law 散文契约与语义锚点**：
   - 外部调查的全部证据法则完整固化在 `resources/provider/role/browser/{en,zh-CN}.md`。
   - 通过 `ROLE_SEMANTIC_ANCHORS.browser` 的 8 个核心溯源锚点及 `BROWSER_OBLIGATION_BOUNDARY_ANCHORS`，机械化锁定溯源区分与“观察非义务”负边界。

3. **Canary 契约验证**：
   - 通过 `browser-provenance-canary.test.mjs` 与 `facts-not-obligations.test.mjs`，确保在无真实浏览器运行的单元测试套件中，双语契约与实质性语义区分始终有效且不退化。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| EXTERNAL-INVESTIGATION-001 | `requirements/external-investigation/tests/browser-provenance-canary.test.mjs::WHAT[EXTERNAL-INVESTIGATION-001] provenance contract is stated in Role Law in both locales` |
| EXTERNAL-INVESTIGATION-002 | `requirements/external-investigation/tests/browser-provenance-canary.test.mjs::WHAT[EXTERNAL-INVESTIGATION-002] browser_provenance_anchor_ids_are_pinned_to_the_eight_distinctions`；`requirements/external-investigation/tests/browser-provenance-canary.test.mjs::WHAT[EXTERNAL-INVESTIGATION-002] provenance-not-reachability anchor hits real Role Law in both locales` |
| EXTERNAL-INVESTIGATION-003 | `requirements/external-investigation/tests/browser-provenance-canary.test.mjs::WHAT[EXTERNAL-INVESTIGATION-003] far-shore anchor hits real Role Law in both locales` |
| EXTERNAL-INVESTIGATION-004 | `requirements/external-investigation/tests/browser-provenance-canary.test.mjs::WHAT[EXTERNAL-INVESTIGATION-004] source-closest anchor hits real Role Law in both locales` |
| EXTERNAL-INVESTIGATION-005 | `requirements/external-investigation/tests/browser-provenance-canary.test.mjs::WHAT[EXTERNAL-INVESTIGATION-005] visual-truth anchor hits real Role Law in both locales` |
| EXTERNAL-INVESTIGATION-006 | `requirements/external-investigation/tests/browser-provenance-canary.test.mjs::WHAT[EXTERNAL-INVESTIGATION-006] condition-preserved anchor hits real Role Law in both locales` |
| EXTERNAL-INVESTIGATION-007 | `requirements/external-investigation/tests/browser-provenance-canary.test.mjs::WHAT[EXTERNAL-INVESTIGATION-007] inference-not-observation anchor hits real Role Law in both locales`；`requirements/external-investigation/tests/browser-provenance-canary.test.mjs::WHAT[EXTERNAL-INVESTIGATION-007] removing_one_distinction_from_the_fixture_turns_red` |
| EXTERNAL-INVESTIGATION-008 | `requirements/external-investigation/tests/browser-provenance-canary.test.mjs::WHAT[EXTERNAL-INVESTIGATION-008] disagreement-not-averaged anchor hits real Role Law in both locales`；`requirements/external-investigation/tests/browser-provenance-canary.test.mjs::WHAT[EXTERNAL-INVESTIGATION-008] disagreement_not_averaged_is_not_a_word_level_regex` |
| EXTERNAL-INVESTIGATION-009 | `requirements/external-investigation/tests/browser-provenance-canary.test.mjs::WHAT[EXTERNAL-INVESTIGATION-009] no-cross-sea-certainty anchor hits real Role Law in both locales` |
| EXTERNAL-INVESTIGATION-010 | `requirements/external-investigation/tests/stealth-browser-role-lock.test.mjs::WHAT[EXTERNAL-INVESTIGATION-010] browser_is_the_only_network_office` |
| EXTERNAL-INVESTIGATION-011 | `requirements/external-investigation/tests/facts-not-obligations.test.mjs::WHAT[EXTERNAL-INVESTIGATION-011] observation-not-obligation is pinned`；`requirements/external-investigation/tests/facts-not-obligations.test.mjs::WHAT[EXTERNAL-INVESTIGATION-011] Role Law hits observation-not-obligation in both locales`；`requirements/external-investigation/tests/facts-not-obligations.test.mjs::WHAT[EXTERNAL-INVESTIGATION-011] removing the distinction turns red`；`requirements/external-investigation/tests/facts-not-obligations.test.mjs::WHAT[EXTERNAL-INVESTIGATION-011] is not a word-level obligation regex` |
