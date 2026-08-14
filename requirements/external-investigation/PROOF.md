# PROOF — external-investigation

> 每条 WHAT 命题恰好一行落点。类型：`MOVE` = 物理移入本包；`REUSE` = 留在原处（多-owner，
> cutover 时按 `SPLIT@cutover` 拆分）；`NEW` = 本包新写。
> 运行命令统一为 `node --test <file>`（在仓库根执行）。

## 落点表

| 命题 | 落点测试（文件 + test 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| EXTERNAL-INVESTIGATION-001（外部事实以 provenance 建立） | `requirements/external-investigation/tests/browser-provenance-canary.test.mjs`（NEW）`browser_provenance_anchors_hit_real_role_law_in_both_locales`（provenance 合同全文双语命中）+ 散文规范文本（`resources/provider/role/browser/en.md`「Provenance, compression, and certainty」） | NEW | `node --test requirements/external-investigation/tests/browser-provenance-canary.test.mjs` |
| EXTERNAL-INVESTIGATION-002（provenance-not-reachability） | 同上 `browser_provenance_anchors_hit_real_role_law_in_both_locales`（锚点双语命中）+ `browser_provenance_anchor_ids_are_pinned_to_the_eight_distinctions`（id pin） | NEW | 同上 |
| EXTERNAL-INVESTIGATION-003（far-shore） | 同上（锚点 `far-shore` 双语命中 + id pin） | NEW | 同上 |
| EXTERNAL-INVESTIGATION-004（source-closest） | 同上（锚点 `source-closest` 双语命中 + id pin） | NEW | 同上 |
| EXTERNAL-INVESTIGATION-005（visual-truth） | 同上（锚点 `visual-truth` 双语命中 + id pin） | NEW | 同上 |
| EXTERNAL-INVESTIGATION-006（condition-preserved） | 同上（锚点 `condition-preserved` 双语命中 + id pin） | NEW | 同上 |
| EXTERNAL-INVESTIGATION-007（inference-not-observation） | 同上 `removing_one_distinction_from_the_fixture_turns_red`（删掉该区分 → 红，证明锚点有区分力）+ 双语命中 | NEW | 同上 |
| EXTERNAL-INVESTIGATION-008（disagreement-not-averaged） | 同上 `disagreement_not_averaged_is_not_a_word_level_regex`（「Just average the disagreement.」必须不命中）+ 双语命中 | NEW | 同上 |
| EXTERNAL-INVESTIGATION-009（no-cross-sea-certainty） | 同上（锚点 `no-cross-sea-certainty` 双语命中 + id pin） | NEW | 同上 |
| EXTERNAL-INVESTIGATION-010（外部/本地证据分离） | REUSE `requirements/external-investigation/tests/stealth-browser-role-lock.test.mjs` `AGENT_026_browser_only_wildcard_permission`（Browser 是唯一网络能力 office；其它 role deny）；散文规范文本 Role Law「Reachability is not ownership」节 | REUSE | `node --test requirements/external-investigation/tests/stealth-browser-role-lock.test.mjs` |
| EXTERNAL-INVESTIGATION-011（外部事实不自动产生义务） | `requirements/external-investigation/tests/facts-not-obligations.test.mjs` `EXTERNAL-INVESTIGATION-011 Role Law hits observation-not-obligation in both locales` / `removing the distinction turns red` / `is not a word-level obligation regex`；义务产生路径仍归 office-capability/obligation-ledger | NEW | `node --test requirements/external-investigation/tests/facts-not-obligations.test.mjs` |

## 新写测试清单

| 文件 | 断言 | 结果 |
|---|---|---|
| `requirements/external-investigation/tests/browser-provenance-canary.test.mjs`（Oracle 1） | ① pin 8 anchor id；② `scanSemanticAnchorParity(realProvider, {browser})` 双语绿；③ 删一条区分 → fixture 红；④ `disagreement-not-averaged` 非单词级（反面句子不命中） | `node --test` 4/4 绿 |
| `requirements/external-investigation/tests/facts-not-obligations.test.mjs`（011） | ① pin `observation-not-obligation`；② 真实 Role Law 双语绿；③ 删区分 → 红；④ 反面「网上应该 = 仓库义务」不命中 | `node --test` |

## SPLIT@cutover（REUSE 项拆 owner 计划）

- `requirements/external-investigation/tests/stealth-browser-role-lock.test.mjs`：
  - `AGENT_026_browser_only_wildcard_permission` 的「Browser 是唯一 network office」事实
    → external-investigation（010）；「permission 矩阵 allow/deny」→ `capability-enforcement`。
  - `AGENT_026_kernel_identity_and_commands` / `launch_disabled_fixture_test_uvx` /
    `apply_preserves_other_mcp_servers` / `configure_injects_mcp_on_ok_and_error` →
    `host-boundary`（Host adapter 机制）。

## GAP 声明

- 聚合台账见 `requirements/GAP.md`（GAP-002 CLOSED）。
- EXTERNAL-INVESTIGATION-011 负边界 oracle：`tests/facts-not-obligations.test.mjs`
  （`observation-not-obligation` 双语命中 / 删区分红 / 反面句子不命中）。义务产生路径仍归
  `office-capability` / `obligation-ledger`；本包无 F# observation 类型，机器落点在 Role Law。
- 真实 runtime provenance oracle（真实 browse → claim 带 provenance）需 browser MCP
  adapter / Long Stroke，明确落在 unit 套件之外；canary 是 unit 内可红替代
  （测试注释已写明该边界）。

## 本包拥有的 semantic anchor id

`ROLE_SEMANTIC_ANCHORS.browser` 全部 8 个 provenance id（`scripts/checks/semantic-anchors.mjs`）：

```text
provenance-not-reachability / far-shore / source-closest / visual-truth /
condition-preserved / inference-not-observation / disagreement-not-averaged /
no-cross-sea-certainty
```

011 负边界另册，不混入上述 8 id：`BROWSER_OBLIGATION_BOUNDARY_ANCHORS.browser` 的
`observation-not-obligation`。

（`OFFICE_CAPABILITY_ANCHORS.browser` 的 `browser-external-provenance` 属
`office-capability`，不归本包。）
