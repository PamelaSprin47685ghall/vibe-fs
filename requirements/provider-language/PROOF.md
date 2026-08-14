# PROOF — provider-language

> 每条 WHAT 命题恰好一行落点。类型：`MOVE` = 物理移入本包；`REUSE` = 留在原处（多-owner，
> cutover 时按 `SPLIT@cutover` 拆分）；`NEW` = 本包新写。
> 运行命令统一为 `node --test <file>`（在仓库根执行）。

## 落点表

| 命题 | 落点测试（文件 + test 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| PROVIDER-LANGUAGE-001（二元类型 + locale 映射） | `requirements/provider-language/tests/provider-language.test.mjs` `PROMPT_017_ProviderLanguage_parse_en_and_zh_CN`（parse/label/resourceDirectory）、`PROMPT_017_provider_resource_language_roots_present`（relativePath 映射 en.md/zh-CN.md） | MOVE | `node --test requirements/provider-language/tests/provider-language.test.mjs` |
| PROVIDER-LANGUAGE-002（bind-once 不可变 + 异值 fail-closed） | 同上 `HOST_026_SessionProviderLanguage_bind_once_and_inherit`（同值 Ok、异值 `already bound` Error） | MOVE | 同上 |
| PROVIDER-LANGUAGE-003（child 继承，不重读全局） | 同上 `HOST_026_SessionProviderLanguage_bind_once_and_inherit`（`inheritFromOwner` → child `tryGet` = owner 语言） | MOVE | 同上 |
| PROVIDER-LANGUAGE-004（全局偏好只影响未来 session） | `requirements/provider-language/tests/provider-prose-and-preference.test.mjs` `preference_change_only_affects_future_sessions`（NEW：绑定后改全局，旧 session 不变、新 session 取新值） | NEW | `node --test requirements/provider-language/tests/provider-prose-and-preference.test.mjs` |
| PROVIDER-LANGUAGE-005（Class A/B/C 分类） | REUSE `tests/unit/verify/provider-prose-ownership.test.mjs` `gate_e_red_fixture_counts_english_and_chinese`（Class A 命中）+ `gate_e_heuristic_excludes_paths_and_identifiers`（Class B 路径/标识排除）；REUSE `tests/unit/verify/language-parity-gate.test.mjs` `ac20_identifier_parity_mismatch_reports_semantic_and_diff`（Class B 标识必须同形） | MOVE + REUSE | 见各文件行 |
| PROVIDER-LANGUAGE-006（locale 成对 + bound fail-closed） | REUSE `tests/unit/verify/language-parity-gate.test.mjs` `gate_c_parity_detects_missing_zh_cn` / `gate_c_parity_detects_missing_en` / `gate_c_repo_scan_is_green`；NEW `requirements/provider-language/tests/provider-prose-and-preference.test.mjs` `require_language_pair_fails_closed_on_missing_path` | REUSE + NEW | 见各文件行 |
| PROVIDER-LANGUAGE-007（placeholder parity + 填值不译 + 未替换 fail-closed） | REUSE `tests/unit/verify/language-parity-gate.test.mjs` `gate_c_placeholder_parity_equal_sets_pass` / `gate_c_placeholder_parity_mismatch_reports_diff` / `gate_c_extract_placeholders`；NEW `provider-prose-and-preference.test.mjs` `substitute_replaces_values_and_fails_closed` | REUSE + NEW | 见各文件行 |
| PROVIDER-LANGUAGE-008（tool prose 与 session 语言一致） | REUSE `tests/unit/verify/language-parity-gate.test.mjs` `gate_c_repo_scan_is_green`（全 semantic 面成对）+ MOVE `provider-language.test.mjs` `PROMPT_017_provider_resource_language_roots_present`（lang → locale leaf 装载映射） | REUSE + MOVE | 见各文件行 |
| PROVIDER-LANGUAGE-009（三向所有权 + 禁 match lang + Gate E ratchet） | `requirements/provider-language/tests/provider-prose-ownership.test.mjs`（MOVE）`gate_e_scan_roots_cover_gate0_owners` / `gate_e_red_fixture_counts_english_and_chinese`（禁散落 NL literal）/ `gate_e_green_fixture_is_zero_hits` / `gate_e_baseline_ratchet_blocks_regression` / `gate_e_repo_scan_with_generated_baseline_is_green` / `gate_e_zero_hits_is_closed` / `gate_e_committed_baseline_matches_repo` | MOVE | `node --test requirements/provider-language/tests/provider-prose-ownership.test.mjs` |
| PROVIDER-LANGUAGE-010（Role Law semantic-anchor 同 id 双语命中） | REUSE `tests/unit/verify/language-parity-gate.test.mjs` `gate_c_semantic_anchor_parity_detects_missing_zh_id`（fixture 缺 id → 红）+ `gate_c_semantic_anchor_catalog_requires_every_role_law`（role 目录必须在 catalog） | REUSE | `node --test tests/unit/verify/language-parity-gate.test.mjs` |
| PROVIDER-LANGUAGE-011（protocol identifiers 永不翻译） | REUSE `tests/unit/verify/language-parity-gate.test.mjs` `ac20_identifier_parity_equal_spans_pass` / `ac20_identifier_parity_mismatch_reports_semantic_and_diff` / `ac20_tip_and_tool_catalog_hits_must_match` / `ac20_extract_protocol_identifiers_unions_sources` | REUSE | `node --test tests/unit/verify/language-parity-gate.test.mjs` |

## MOVE 记录

| 源 → 目标 | 适配 | 验证 |
|---|---|---|
| `tests/unit/prompt/provider-language.test.mjs` → `requirements/provider-language/tests/provider-language.test.mjs` | `../support/domain.mjs` → `../../../tests/unit/support/domain.mjs` | `node --test` 4/4 绿 |
| `tests/unit/verify/provider-prose-ownership.test.mjs` → `requirements/provider-language/tests/provider-prose-ownership.test.mjs` | import 深度不变（同级） | `node --test` 8/8 绿 |

## SPLIT@cutover（REUSE 项拆 owner 计划）

- `tests/unit/verify/language-parity-gate.test.mjs`：
  - provider-language 拿走：`gate_c_*`（locale leaves / placeholder parity / semantic
    anchor parity 机制 / repo scan）、`ac20_*`（identifier parity）。
  - 留给 `office-capability` / `action-affordance`：`gate_f_*`（Office capability
    integrity）、`gate_c_tool_description_anchor_parity_*`（tool description 语义锚点）。
  - cutover：拆成两个文件，各归其包；本包保留 `requirements/provider-language/tests/language-parity-gate.test.mjs` 结构部分。
- `tests/unit/invariants/prompt-stability.test.mjs`（Gate D）：
  - provider-language 交叉引用行：SessionProviderLanguage 冻结由 `HOST_026_*`（MOVE 项）证明；
    本文件断言的是 persona/system 字节稳定（`participant-identity` + `prefix-stability`），
    不双 owner。

## 本包拥有的 semantic anchor id

**0 个。** provider-language 不拥有任何 `ROLE_SEMANTIC_ANCHORS` / `TOOL_DESCRIPTION_ANCHORS`
/ `OFFICE_CAPABILITY_ANCHORS` 语义 id（anchor 内容归 office/action/cognition/各域 owner）；
本包拥有的是「同 id 双语命中」的**结构 parity 机制**（`scanSemanticAnchorParity` 的
机制断言，落在 REUSE `gate_c_semantic_anchor_parity_detects_missing_zh_id`）。
