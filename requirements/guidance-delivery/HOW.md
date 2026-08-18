# guidance-delivery — HOW（实现模型与约束）

> 非 normative。WHAT 命题的落点见 `HOW.md`；本文件解释 `src/` 里每个概念
> 的精确位置、约束与失败模式，末尾是「历史与弃权」。

## 1. 交付决策：`src/Wanxiangshu/Session/EnforcerTipGuidance.fs`

```fsharp
type TipGuidance =
    { TipName: string
      Presentation: TipPresentation        // Full | IdentityOnly
      Text: string }                        // Full = header + main.md；Identity = "tip: <name>"
```

- `resolveTipGuidance journal mainOrBloggerSession`（GD-002/003/004/006）：
  1. `tryOwnerMainSession`：Blogger satellite id 经 `SessionAssociationProjection`
     解析到 owner Main；无 association → None（不发明 guidance）。
  2. `latestOwnerTipField`：取该 Main 最近已提交 RecentTip 的 FieldName。
  3. 目录查规则（`EnforcerCatalog.tryFindByField`，`RuntimeResources`），无 → None。
  4. `hasFullTipDelivered` 读 `TipDeliveryProjection`：
     - 未 Full → `TipPresentation.Full`，文本 = 语言化 header + `rule.MainText`；
       异步 append `HostFact.TipGuidanceDelivered { Full }`（推进 Frontier）。
     - 已 Full → `IdentityOnly`，文本 = `tip: <name>`；不推进 Frontier、不写
       durable bool。
- `recordFullTipDelivered`：append 失败只发 `tip-guidance-delivery-append-failed`
  diagnostic（交付仍以 Full 文本继续），后续 Identity 不会静默搁浅。
- `latestTipGuidance` / `latestTipNudge`（GD-007）：同义别名，返回 resolve 的 Text。

> 注意（诚实性）：当前实现把「Coverage 可恢复」近似为「该 Main session 的
> TipDeliveryProjection 未经历 reanchor 清空」——`TipSemanticCoverage` 是
> `TipDeliveryProjection.applyReanchor` 的语义（HOST-006 `ContextReanchored` 清空
> Full 历史），没有独立的字节级 horizon 探测。两轴在投影层分离（GD-001），
> 机器可证明的部分见 §2 与 HOW.md。

## 2. 投影：`src/Wanxiangshu/Feedback/Enforcer/Guidance/DeliveryProjection.fs`

```fsharp
type TipDeliveryProjectionState = { FullDeliveredTips: Set<string> }
```

- `apply tipName presentation state`：Full → 加入集合；IdentityOnly → audit-only
  （不加入，防止把重复身份误记为「全文永久可恢复」）。
- `applyReanchor state` → `empty`：HOST-006 重锚清空 Full 历史（Coverage 丢失），
  但这是**同轴内**的清空；occurrence 维度的单调性由「Full 事实被追加」表达，
  reanchor 本身不追加新 Full 事实 → 重发全文 = restoration，不是新 occurrence
  （GD-005；`TDP_004/005` 锁定）。
- `hasFullDelivered`：判定函数，null/空 tipName → false。

## 3. 历史字节：`src/Wanxiangshu/OpenCode/Host/PairProgramming/GuidelineProjection.fs`（GD-011）

```fsharp
type PairProgrammingGuideline =
    { Ordinal: int64; CallId: ToolCallId; MarkerText: string
      CallGap: TranscriptGap; ResultGap: TranscriptGap }
```

- `apply ordinal callId markerText callGap resultGap state`：三拒绝——ordinal ≠
  next（`NonSequentialOrdinal`）、CallId 重复（`DuplicateCallId`）、placement 重复
  （`DuplicatePlacement`，SessionId 隐含 + CallGap + ResultGap 至多一对）。
- `pairs`：存储 newest-first，返回 oldest-first（replay 顺序）。
- `MarkerText` 原样存储 → replay byte-identical（HOST-013「当时实际看到的精确 payload」）。新 occurrence 在 append 前已包成 `<skill_content name="">…</skill_content>`；历史 raw MarkerText 保持旧 wire。substrate 是 Journal fold，不是私有 delivery 文件。

## 4. marker 注入：`src/Wanxiangshu/OpenCode/Host/PairProgrammingThoughtTransform.fs`

- `tryInject`：把**已经完成组装的** pair body 包成 `<skill_content name="">…</skill_content>`，再生成 synthetic `skill({ name: "" })` tool-call/tool-result pair，并锚到 transcript 的 CallGap/ResultGap；Cursor 不造 synthetic message，只把同一 final MarkerText 追加到既有 terminal result 分隔符后。Main 侧没有 fake-user message（GD-009）。
- occurrence 组装在 `PluginTransforms` + `PairProgrammingCalibration`：`latest tip guidance` → TIME-007 `elapsed` → DELEG-022 `remaining expected tool calls` → canonical pair-programming guideline，各动态 owner 只做 O(1) projection read；无 estimate 时省略该 fragment。
- `composeWithElapsed` 的结果立即交给 `tryInject`，成功后由 `PairProgrammingGuidelineAnchored.MarkerText` 原样 durable；replay 不再调用 elapsed/estimate renderer。

## 5. 失败模式速查（红了说明什么）

| 症状 | 断裂的命题 | 排查入口 |
|---|---|---|
| 重复 resolve 仍给全文 | GD-003 | `resolveTipGuidance` 是否跳过 `hasFullTipDelivered` |
| IdentityOnly 后 reanchor 仍给身份 | GD-005 | `applyReanchor` 是否被接线（ContextReanchored fold） |
| restart 后第一次判定漂移 | GD-004 | 交付决策是否读进程内存 |
| main.md 进了 Blogger system | GD-008 | `composeBloggerSystemPromptFor` 是否混入 MainText |
| 历史 marker 字节被改写 | GD-011 | `GuidelineProjection` 是否按原文存储/重放 |

## 6. 验证命令

```text
node --test requirements/guidance-delivery/tests/<file>   # 单文件（每文件必须绿）
node requirements/verification-system/tests/run.mjs                                    # 全单元（cutover 时由 lead 执行）
```

## 7. 依赖

- `behavior-diagnosis`：交付消费已成立的 diagnosis occurrence（RecentTip）。
- `participant-horizon`：Coverage 是 horizon-relative 概念；本包只区分
  Frontier/Coverage 两轴，不定义 horizon admission 一般律。
- `durable-events`：交付事实（TipGuidanceDelivered）与历史 pair 是 durable fold
  的输入。

## 8. 历史与弃权

| 源 | 裁决 | 记录 |
|---|---|---|
| 历史 change（rulebook）§27 | GARBAGE（双消费者裁决：Blogger 历史 ≠ Main 指令面） | 历史 change（rulebook）§27 |
| `delivered-tips.json` / process-local HashSet / 文件 tip ledger | GARBAGE（交付 substrate 只能是 EventStore fold） | 历史 change（rulebook）§16 |
| Main fake-user enforcement overlay / NudgeAnchored / NudgeConsumed | GARBAGE（clean break；交付不 mint authority） | 历史 change（enforcer）§10；历史 why/enforcer 条款 |
| 每次 Full 或仅 Identity | GARBAGE（被拒方案：烧上下文 or 首次不可执行） | 历史 why/enforcer 备选与被拒 |
| 单一 durable bool 压 Frontier+Coverage | GARBAGE（reanchor 后误删/假装仍在） | 历史 change（rulebook）§17 |
| 历史 pair 随 main.md 版本改写 | GARBAGE（byte-identical replay 冻结） | 历史 change（rulebook）§17 |
| enforcer-cross-family-collision.mjs | 已删除（2026-08-15）：机械 A40 替代噪音大于价值，按用户要求移除；A40 归人类 tournament，本包不再设机器载体（原 PROOF-MAP Phase D 裁决作废） | 本文件历史；CHANGELOG |
| `enforcer-rulebook-gate.mjs` | 已退休空壳（2026-08-12）；本包不依赖任何 prose 形状门 | 历史 HANDOFF §24 |
| 当前实现中 TipSemanticCoverage 与 TipDeliveryProjection 同投影（applyReanchor 清空） | HOW（horizon 可恢复性以投影近似表达；字节级 horizon 探测是未来增强，不改变两轴分离合同） | 本文件 §1 诚实性注 |

## 验证与测试落点

> 每条 WHAT 命题恰好一行落点。类型：`MOVE` = 已物理移入本包 `tests/`（删除原文件，
> 单跑绿）；`REUSE` = 留在原处（多-owner 或不宜移动），记精确锚点 + cutover 拆分计划
> （`SPLIT@cutover`）；`NEW` = 本包新写。运行命令：`node --test <file>`。
> 本包无 semantic-anchors.mjs anchor id（该 catalog 只有 ROLE/TOOL/OFFICE 三组）。

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| GD-001 交付前沿 ≠ 语义覆盖（两轴分离） | `tests/tip-delivery-projection.test.mjs` `WHAT[GD-001] TDP_006_frontier_and_coverage_are_two_axes_not_one_bool`；`tests/tip-guidance-delivery.test.mjs` `WHAT[GD-002] ENFORCER_TIP_DELIVERY_002_second_resolve_same_tip_is_identity_only` / `WHAT[GD-005] ENFORCER_TIP_DELIVERY_006_context_reanchor_clears_full_so_next_is_full_again`；`tests/tip-delivery-projection.test.mjs` `WHAT[GD-004] TDP_001_empty_state_has_nothing_delivered` / `WHAT[GD-003] TDP_002_full_marks_tip_delivered_identity_only_does_not` / `WHAT[GD-005] TDP_004_reanchor_voids_full_history_so_next_resolve_refulls` / `WHAT[GD-005] TDP_005_reanchor_does_not_advance_occurrence_frontier` | MOVE + NEW | `node --test tests/tip-delivery-projection.test.mjs` |
| GD-002 首次交付 = Full main.md | `tests/tip-guidance-delivery.test.mjs` `WHAT[GD-002] ENFORCER_TIP_DELIVERY_001_first_resolve_is_full_main_md` / `WHAT[GD-002] ENFORCER_PROMPT_017_full_tip_guidance_uses_owner_session_zh_cn_rulebook` | MOVE | `node --test tests/tip-guidance-delivery.test.mjs` |
| GD-003 重复交付 = IdentityOnly，不重复全文 | `tests/tip-guidance-delivery.test.mjs` `WHAT[GD-003] ENFORCER_TIP_DELIVERY_002`（`tip: <name>`、不含 main.md、Full 长于 Identity）；`tests/tip-delivery-projection.test.mjs` `WHAT[GD-003] TDP_002_full_marks_tip_delivered_identity_only_does_not` / `WHAT[GD-003] TDP_003_blank_or_null_tip_name_is_ignored` | MOVE + NEW | `node --test tests/tip-guidance-delivery.test.mjs` |
| GD-004 交付决策只 fold durable facts，restart-safe | `tests/tip-guidance-delivery.test.mjs` `WHAT[GD-004] ENFORCER_TIP_DELIVERY_003_latestTipGuidance_matches_resolve_text`；`tests/latest-tip-nudge.test.mjs` `WHAT[GD-004] ENFORCER_TIP_NUDGE_001_latest_tip_first_delivery_is_full_main_md`（二次调用即 Identity，证明判定已 durable 化）；`tests/tip-delivery-projection.test.mjs` `WHAT[GD-004] TDP_001_empty_state_has_nothing_delivered` / `WHAT[GD-004] TDP_002_full_marks_tip_delivered_identity_only_does_not` | MOVE + NEW | `node --test tests/latest-tip-nudge.test.mjs` |
| GD-005 reanchor：语义恢复 ≠ 新 occurrence | `tests/tip-guidance-delivery.test.mjs` `WHAT[GD-005] ENFORCER_TIP_DELIVERY_006`（reanchor 后必须再 Full）；`tests/tip-delivery-projection.test.mjs` `WHAT[GD-005] TDP_004_reanchor_voids_full_history_so_next_resolve_refulls` / `WHAT[GD-005] TDP_005_reanchor_does_not_advance_occurrence_frontier` | MOVE + NEW | `node --test tests/tip-delivery-projection.test.mjs` |
| GD-006 owner 解析与 None 语义 | `tests/tip-guidance-delivery.test.mjs` `WHAT[GD-006] ENFORCER_TIP_DELIVERY_004_blogger_session_id_resolves_owner_main` / `WHAT[GD-006] ENFORCER_TIP_DELIVERY_005_missing_tip_returns_none`；`tests/latest-tip-nudge.test.mjs` `WHAT[GD-006] ENFORCER_TIP_NUDGE_002_missing_recent_tip_returns_none` / `WHAT[GD-006] ENFORCER_TIP_NUDGE_003_missing_owner_returns_none` | MOVE | `node --test tests/tip-guidance-delivery.test.mjs` |
| GD-007 `latestTipGuidance`/`latestTipNudge` 同义 | `tests/tip-guidance-delivery.test.mjs` `WHAT[GD-007] ENFORCER_TIP_DELIVERY_003b_latestTipNudge_is_same_bytes_as_latestTipGuidance`（viaLatest === viaAlias）；`tests/latest-tip-nudge.test.mjs` `WHAT[GD-007] ENFORCER_TIP_NUDGE_001b_latestTipNudge_is_same_bytes_as_latestTipGuidance` | MOVE | `node --test tests/latest-tip-nudge.test.mjs` |
| GD-008 detection/remediation audience 分离 | `tests/audience-separation.test.mjs` `WHAT[GD-008] AUDIENCE_001_main_md_sections_never_enter_blogger_system_prompt` / `WHAT[GD-008] AUDIENCE_002_corpus_level_detection_and_remediation_do_not_leak` / `WHAT[GD-008] AUDIENCE_003_previous_tip_history_is_not_main_authority`；`tests/tip-v2-delivery.test.mjs` `WHAT[GD-008] ENFORCER_TIP_13_work_record_contains_previous_enforcer_tip_blocks` / `WHAT[GD-008] ENFORCER_TIP_14_prompt_has_anti_repeat_and_severe_exception` | NEW + REUSE | `node --test tests/audience-separation.test.mjs` |
| GD-009 交付不创建 interaction authority | `tests/latest-tip-nudge.test.mjs` `WHAT[GD-009] CTX_002_GUIDELINE_001_marker_without_nudge_is_guideline_text` / `WHAT[GD-009] CTX_002_GUIDELINE_002_marker_with_nudge_uses_double_newline`（auto-injected tool pair 机制，非 user message）；`tests/tip-guidance-delivery.test.mjs` `WHAT[GD-002] ENFORCER_TIP_DELIVERY_001`（交付形状 = tip header + main.md）；`tests/audience-separation.test.mjs` `WHAT[GD-008] AUDIENCE_003` | MOVE + NEW | `node --test tests/latest-tip-nudge.test.mjs` |
| GD-011 已投递 auto-injected 字节按原文冻结 | `tests/guideline-projection.test.mjs` `WHAT[GD-011] GP_002_apply_records_pair_and_restores_marker_bytes` / `WHAT[GD-011] GP_003_non_sequential_ordinal_is_rejected` / `WHAT[GD-011] GP_004_duplicate_call_id_is_rejected` / `WHAT[GD-011] GP_005_duplicate_placement_is_rejected` / `WHAT[GD-011] GP_006_replay_restores_pairs_oldest_first`；`tests/latest-tip-nudge.test.mjs` `WHAT[GD-009] CTX_002_GUIDELINE_001/002`（marker 正文透传） | NEW + MOVE | `node --test tests/guideline-projection.test.mjs` |
| GD-012 新 occurrence 消费当前 calibration projection 后冻结 MarkerText | `tests/pair-calibration.test.mjs` `WHAT[GD-012] GD_012_DELEG_022_no_estimate_means_no_dynamic_fragment`（no-estimate omission）/ `WHAT[GD-012] GD_012_each_new_occurrence_can_render_a_new_remaining_without_rewriting_old_text`（occurrence-by-occurrence remaining）/ `WHAT[GD-012] GD_012_dynamic_fragment_is_between_tip_and_canonical_guideline` + REUSE `requirements/time-capability/tests/pair-session-elapsed.test.mjs`（fresh elapsed / historical immutable / tip→elapsed→estimate→guideline order） | NEW + REUSE（FROZEN 2026-08-14） | **按用户要求冻结后未执行**；实现后不改 oracle |

### semantic anchor id

本包不拥有任何 `scripts/checks/semantic-anchors.mjs` anchor id。

### cutover 待办（SPLIT@cutover）

- `requirements/guidance-delivery/tests/tip-v2-delivery.test.mjs`：`ENFORCER_TIP_13/14`（previous
  tip 呈现与 anti-repeat 提示）拆入本包；其余归 `behavior-diagnosis` /
  `context-compression`。
- `tests/unit/enforcer/enforcer-cycle-protocol.test.mjs`：nudge/repair 族
  （ENFORCER-060..068）跨 diagnosis/delivery——本包只引用与交付相关的断言
  （见 `behavior-diagnosis/HOW.md` BD-017 的 SPLIT 记录），cutover 时按
  convergence 文件归属统一迁移。
- 交付路径的「字节级 horizon 探测」（Coverage 精确测量）是未来增强，当前以
  `TipDeliveryProjection.applyReanchor` 近似（见 `HOW.md` §1 诚实性注），
  不改变两轴分离合同。
