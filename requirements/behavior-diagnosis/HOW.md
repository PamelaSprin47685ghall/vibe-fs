# behavior-diagnosis — HOW（实现模型与约束）

> 非 normative。WHAT 命题的落点见 `HOW.md`；本文件解释 `src/` 里每个概念
> 的精确位置、约束与失败模式，末尾是「历史与弃权」。

## 1. 领域纯内核

### 1.1 `src/Wanxiangshu/Domain/EnforcerCatalog.fs`

```fsharp
type EnforcerRule =
    { Name: string          // 目录 basename = TipIdentity
      EnforcerText: string  // resources/enforcer/<name>/enforcer.md 全文
      MainText: string      // resources/enforcer/<name>/main.md 全文
      RuleId: string        // durable id; clean break = Name
      FieldName: string     // provider enum 值; = Name
      LexicalOrder: int }   // 目录顺序 1..N（只描述装载/enum 顺序）
```

- `EnforcerCatalog.validate`（BD-003）：schemaVersion=1、非空、三身份唯一且相等、
  序连续、正文非空。失败返回 `Error string`，装载层转抛 → fail fast。
- `EnforcerCatalog.tryFindByField`（BD-007）：trim 后精确 `FieldName`/`Name` 命中；
  无 fuzzy、无近似、无默认。
- `EnforcerCatalog.fieldNames`：按 LexicalOrder 输出 provider enum 清单。

### 1.2 `src/Wanxiangshu/Domain/EnforcerCodec.fs`（BD-006/007/008）

```fsharp
type CanonicalBlogCall = { Text: string option; Evidence: string option; Tip: EnforcerTip }
```

- `decodeCall rules rawArgs`：只认 `entry`（兼容旧 `text`）、`tip`、`evidence`；
  其余 property 忽略（ENFORCER-024）。缺/空/非 string `tip` → `MissingTipError`；
  未知 tip → `UnknownTip <value>`。
- `hasValidText`：entry trim 后非空才算有效文本。

> 注意（诚实性）：历史 what/enforcer 条款 ENFORCER-004/020 声称「无 `evidence`
> 字段」，但当前 codec **仍保留 optional `evidence`**（合并时精确去重、上限
> 128 KiB）。本包按当前世界记录：evidence 不改变 occurrence 身份（BD-009），
> 「evidence 删除」是文档与代码之间的漂移，见 §7 弃权。

### 1.3 `src/Wanxiangshu/Enforcer/Cycle/Model.fs`（BD-009）

Provider cycle 的 cardinality gate 位于 Host continuation 边界：raw assistant step 必须恰好一个
`chronicle` part。0 次与 2+ 次不进入 merge/commit；terminal 后直接复用 BD-017 的 protocol repair。
其中 2+ 次通常仍会因 Host tool loop 进入下一次 transform；**0 次不会**，所以 zero-tool terminal 的
repair 入口由 `SessionIdle → ReconcilePass → HostTurnObserver` 驱动，不能等一个不存在的后续 transform。
`EnforcerCycle` 只处理已通过该 gate 的单调用 canonical value，不再承担多调用业务归并语义。

### 1.4 `src/Wanxiangshu/Domain/RulebookObservation.fs`（BD-015）

- `ObservationUnit`：可选 TipName + 可选 FrameDigest/Body 的配对单元。
- `WorkLogObservation`：TipName + CycleId + 可选 FrameDigest（tip-anchored）。
- `pairTipsAndFrames`：前向 zip；剩余 tips 或 frames unpaired 追加。
- `ofTipsAndFrames`：zip tip 身份 × frame digest；剩余 tips 保留（digest=None），
  剩余 frames 丢弃（不发明 tip）。

## 2. 资源装载与 Blogger system 合成（BD-002/004/005）

`src/Wanxiangshu/Infrastructure/Resources/EnforcerCatalogResource.fs`：

- `loadFor lang`：枚举 `resources/enforcer/*/` 子目录（basename = TipName），
  kebab-case 校验，按语言读叶子（en：`enforcer.md`+`main.md`；zh-CN：
  `enforcer.zh-CN.md`+`main.zh-CN.md`），缺文件/空文本抛异常，最后过
  `EnforcerCatalog.validate`；任何失败 → 启动异常。
- `composeBloggerSystemPromptFor lang base rules`：base + `# Enforcer Rulebook` +
  按 LexicalOrder 的 `## <Name>` + enforcer.md 全文，`"\n\n"` 拼接。derived only，
  不写回仓库。`main.md` **从不**进入 Blogger system（audience 分离，见
  `guidance-delivery`）。

## 3. Cycle 提交与恢复（BD-010/011/012/013/017）

### 3.1 校验：`src/Wanxiangshu/Session/EnforcerCycleDecode.fs`

- 内容硬界：`MaxBlogTextBytes = 512 * 1024`、`MaxEvidenceBytes = 128 * 1024`。不存在
  多调用 merge cap：2+ raw `chronicle` 在 canonical cycle 构造前已转 protocol repair。
- cardinality（BD-009）：先按 raw assistant parts 计 `chronicle` 调用数；terminal 时必须 =1。
  0/2+ → protocol repair，不进入 `validateCycle`/commit。
- 身份/边界校验（BD-010/011）：通过 cardinality 后，空 messageId / 越界 →
  `Diagnostic.fatal "enforcer-cycle-failed"`（fail closed）。`EnforcerHost` 同名的
  `MaxBlogTextBytes` 等常量必须与 Decode 保持一致（单一来源是 Decode）。

### 3.2 提交：`src/Wanxiangshu/Session/EnforcerCycleCommit.fs`

```fsharp
type CycleCommitOutcome = KnownCommitted | KnownNotCommitted of string | CommitUnknown of string
```

- `commitCycle`：先查 receipt（已存在 → `KnownCommitted`，幂等）；无 staged context
  → `KnownNotCommitted`；PERSIST-010 precheck（staged ingest/cutoff/epoch vs 投影
  不一致）→ `KnownNotCommitted`（可恢复弃置，绝不先写事实再被 fold 拒绝）；
  blobs 先写（text、evidence），再 append 单条 `BlogObservationCommitted`；
  `WriteUnknown` → `CommitUnknown`（fail-closed reconcile，不盲重试模型）。

### 3.3 协调：`src/Wanxiangshu/Session/EnforcerHost.fs` + `EnforcerContinuation.fs`

- `handleContinuation`：薄分发（emptyCallsBranch / commitBranch / firstRequestBranch）。
- `EnforcerContinuation`：三分支 + `CycleDisposition`；成功提交后 Park 或注入；transform **没有物理
  InteractionRepairNudge capability**。`NoRecovery` invalid terminal 只原样 Project，第一次 nudge 留给 idle owner；
  `status=error` 且非 interrupted 的 chronicle 同样只 Project，让 Host tool result 驱动 Blogger 自纠。只有
  nudge 已实际发生之后的新 invalid terminal 才可在 transform 进入 AABB/Fallback。Nudge claim identity 绑定
  exact `BloggerRequestId + ProviderRunIdentity`；transform AABB marker 同时保存 requestKey + target terminal。
  同一 terminal 在任一阶段重放都只投影，不重复发送、不推进预算；只有 nudge 后的下一 invalid terminal
  才进入 AABB。进入 AABB 后，每个新的 invalid terminal 都推进一次 shared fallback failure；projection 仍可
  继续则再发送一次 request-scoped AABB，只有 durable fallback exhaustion 才 fatal。旧 request 的 claim 不参与新 request。
- `HostTurnObserver`：`Role.Blogger` 的 zero-tool idle terminal 不再进入 ordinary
  `MissingClosingReport`。它调用 `InteractionRepairWorkflow.repairBloggerProtocol`：只有 exact live
  `BloggerRequest` 才拥有 repair budget；第一次 invalid terminal 通过 fresh idle permit 发送
  `blogger-missing-tool` nudge；同 terminal 重放幂等；新的 invalid terminal 消费 fresh idle permit，
  `FallbackLedger` 只负责记录 confirmed failure，随后仍必须发送该 request 的 `blogger-aabb` repair——
  即使首发 AABB 前这次记账返回 generic `RecoveryExhausted` 也不得抢走已经赢得的首发 AABB。`blogger-aabb`
  claim 保留 request id + target terminal；同 terminal 的重复 idle/transform 幂等；后续不同的新 invalid
  terminal 再次记 confirmed failure，`RecoveryAdvanced` 才继续下一发 AABB，真实 `RecoveryExhausted`
  才终止 Blogger cycle。无 live request 的历史 idle 只观察，不发送。
- cold/reconcile recovery 只从 `SessionMessage.ToolParts` 读取 `ToolName=chronicle` + completed state；
  `MessagePart.ToolResult` 已丢 tool name，禁止用于判断修复是否成功。`ClaimSequences` 只负责 durable
  occasion/audit；probe 还必须核对当前 request-scoped dispatch lifecycle，已 `Abandoned` 的 AABB claim
  不能恢复成 `AabbRepairIssued`。transform 注入的 synthetic repair 也写回 requestKey + target terminal
  侧信道。纯 completed-assistant transcript `rejudgeFromEvidence` 仍不得凭空发明 AABB。
- `EnforcerFrameRecovery.fs`：`tryLiveCycleContext`（commit 只用 live InFlight）、
  `tryReloadRequestContext`（durable open materialization 恢复）、
  `lastCoveredSequence` / `coveredPrefixDigest`（出生门，BD-013）。

### 3.4 投影：`src/Wanxiangshu/Feedback/Enforcer/Projection.fs`（BD-014）

- `RecentTipLimit = 8`；`applyFromEntry`（每 cycle 一个 tip，按 ProviderRun 幂等）；
  `applySquash count`（co-truncate 最老 `min(count, tips)`）；`recentTips`
  oldest → newest；`tryFindByProviderRun`。
- `src/Wanxiangshu/Feedback/Enforcer/Observation.fs`（BD-015/016）：
  `observationsOf` / `observationsOfSession` / `observationsAfterSquash` 把
  Enforcement 与 Blog 两个投影 zip 成配对 Observation 视图——命名 fold，非第二
  store；物理事实仍是 `BlogObservationCommitted` / `BlogObservationsSquashed`。

## 4. 失败模式速查（红了说明什么）

| 症状 | 断裂的命题 | 排查入口 |
|---|---|---|
| 新增规则后 `catalog.test.mjs` 失败 | BD-003 | `EnforcerCatalog.validate` 或目录/叶子 |
| 未知 tip 被接受 | BD-007 | `tryFindByField` 是否被改成 fuzzy |
| 2+ `chronicle` 仍然提交/推进 coverage | BD-009 | raw cardinality gate 是否在 commit 前被绕过 |
| 重复 ToolCallId 竟提交了 | BD-010 | `EnforcerCycleDecode` 身份校验 |
| frame 与 coverage 不同步 | BD-012 | `EnforcerCycleCommit.commitCycle` 原子性 |
| squash 后 tips 独立存活 | BD-016 | `EnforcementProjection.applySquash` / Fold 接线 |
| 零推进窗口被启动 | BD-013 | `mainContextFromChunk` 出生门 |

## 5. 验证命令

```text
node --test requirements/behavior-diagnosis/tests/<file>   # 单文件（每文件必须绿）
node requirements/verification-system/tests/run.mjs                                    # 全单元（cutover 时由 lead 执行）
```

## 6. 依赖

- `semantic-trace`：诊断建立在 XTrace 覆盖推进的事实上（BD-013 出生门读
  XTraceProjection）。
- 消费方：`guidance-delivery`（把本包产生的 occurrence 变成 Main 可恢复交付）。

## 7. 历史与弃权

| 源 | 裁决 | 记录 |
|---|---|---|
| 旧 `SSOT/15` score-vector / throttle / NudgeAnchored / Main overlay | GARBAGE（clean break，ENFORCER-072/073） | 历史 why/enforcer；历史 change（enforcer）§10 |
| `catalog.json` 与 `enforcement-a01` 旧 id | GARBAGE（目录即身份取代） | 历史 change（rulebook）§0/§23 |
| 历史 what/enforcer 声称「无 evidence 字段」 | HOW 漂移：当前 codec 仍保留 optional evidence（merge 去重 + 128 KiB 界）；occurrence 身份不因 evidence 改变 | 本文件 §1.2；cutover 时需与文档统一 |
| `scripts/checks/enforcer-rulebook-gate.mjs` | 已退休空壳（2026-08-12）；tip-SSOT proof 由 `tests/unit/enforcer/**` catalog 测试承担，不再有 prose 形状机器门 | 历史 HANDOFF §24 |
| enforcer.md 写作宪法 A4–A30（mandatory headings / token budget / sibling 校准） | HOW（authoring 规范，非 runtime 合同）；不再有机械门 | 历史 change（rulebook）Appendix A |
| Blogger 生命周期物理所有权（HasFlight / HasParked / PendingOffer / DrainWindow） | 归 Blogger convergence 交叉（`context-compression` 侧）；本包只消费 cycle 提交事实 | `requirements/behavior-diagnosis/tests/blogger-cycle-atomic-fact.test.mjs` |

## 验证与测试落点

> 每条 WHAT 命题恰好一行落点。类型：`MOVE` = 已物理移入本包 `tests/`（删除原文件，
> 单跑绿）；`REUSE` = 留在原处（多-owner 或不宜移动），记精确锚点 + cutover 拆分计划
> （`SPLIT@cutover`）；`NEW` = 本包新写。运行命令：`node --test <file>`。
> 本包无 semantic-anchors.mjs anchor id（该 catalog 只有 ROLE/TOOL/OFFICE 三组）。

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| BD-001 目录即唯一规则真相（TipName=enum=RuleId） | `tests/catalog.test.mjs` `ENFORCER_170_catalog_has_exactly_120_rules` / `ENFORCER_170_tip_name_equals_rule_id_and_field` / `ENFORCER_172_field_names_match_the_rfc_spelling`；REUSE `requirements/behavior-diagnosis/tests/tip-v2-contract.test.mjs` `ENFORCER_TIP_01/02_and_16` | MOVE + REUSE | `node --test tests/catalog.test.mjs` |
| BD-002 装载 fail-fast、零 fallback | `tests/catalog-validation.test.mjs`（validate 拒绝族）；`tests/catalog.test.mjs`（120 规则/非空正文）；REUSE `requirements/behavior-diagnosis/tests/integration/resources/enforcer-rulebook.test.mjs`（打包资源路径） | MOVE + REUSE | `node --test tests/catalog.test.mjs` |
| BD-003 Domain 校验合同（schemaVersion/唯一/1..N/非空） | `tests/catalog-validation.test.mjs` `ENFORCER_170_validate_accepts_one_rule` … `ENFORCER_170_validate_rejects_identity_mismatch`（11 条全族） | MOVE | `node --test tests/catalog-validation.test.mjs` |
| BD-004 检测语料全量、确定性进 Blogger system | `tests/rulebook-system-composition.test.mjs` `BEHAVIOR_DIAGNOSIS_SYSTEM_001_composed_prompt_contains_every_tip_exactly_once` / `SYSTEM_002_composition_is_deterministic` / `SYSTEM_004_english_load_matches_packaged_rule_count` | NEW | `node --test tests/rulebook-system-composition.test.mjs` |
| BD-005 本地化叶子同样完整 | `tests/rulebook-system-composition.test.mjs` `SYSTEM_003_zh_cn_leaf_load_is_complete_and_nonempty` | NEW | `node --test tests/rulebook-system-composition.test.mjs` |
| BD-006 chronicle 参数合同（entry+tip 必需） | `tests/codec.test.mjs` `ENFORCER_023_missing_tip_fails` / `ENFORCER_023_empty_tip_fails` / `ENFORCER_022_text_and_evidence_are_reserved_not_tips` / `ENFORCER_022_has_valid_text_requires_nonempty_text` | MOVE | `node --test tests/codec.test.mjs` |
| BD-007 tip 精确映射，无 fuzzy | `tests/codec.test.mjs` `ENFORCER_023_unknown_tip_fails` / `ENFORCER_021_valid_field_maps_exact_rule_id` / `ENFORCER_021_tip_trims_whitespace_before_lookup` / `ENFORCER_024_fuzzy_or_misspelled_tip_is_not_mapped` | MOVE | `node --test tests/codec.test.mjs` |
| BD-008 无 score path | `tests/codec.test.mjs` `ENFORCER_024_extra_numeric_properties_are_ignored`；`tests/catalog.test.mjs` `ENFORCER_170_no_bridge_fields_on_rule`；REUSE `requirements/behavior-diagnosis/tests/tip-v2-contract.test.mjs` `ENFORCER_TIP_03_04_facade_surface_has_tip_not_numeric_scores` | MOVE + REUSE | `node --test tests/codec.test.mjs` |
| BD-009 provider run 恰好一次 `chronicle` | REUSE `requirements/behavior-diagnosis/tests/enforcer-cycle-protocol.test.mjs` `ENFORCER_042_multi_call_first_terminal_issues_one_nudge_and_does_not_commit` / `ENFORCER_042_multi_call_same_terminal_reentry_is_idempotent` / `ENFORCER_042_second_multi_call_terminal_after_nudge_triggers_aabb`；`tests/cycle-nudge.test.mjs` `ENFORCER_042_domain_has_no_multi_call_merge_surface` | REUSE + MOVE | `node --test requirements/behavior-diagnosis/tests/enforcer-cycle-protocol.test.mjs tests/cycle-nudge.test.mjs` |
| BD-010 Cycle provider-run 身份 fail-closed | `tests/identity-fail-closed.test.mjs` `ENFORCER_043_no_provable_provider_run_fails_closed`；`tests/cycle-nudge.test.mjs` `ENFORCER_043_valid_cycle_requires_nonempty_text` | MOVE | `node --test tests/identity-fail-closed.test.mjs tests/cycle-nudge.test.mjs` |
| BD-011 fail-closed 内容硬界（512KiB/128KiB） | `tests/bounds.test.mjs` `ENFORCER_043_canonical_text_over_512KiB_fails_closed` / `ENFORCER_043_canonical_evidence_over_128KiB_fails_closed` / `ENFORCER_042_bound_constants_match_utf8_byte_thresholds` | MOVE | `node --test tests/bounds.test.mjs` |
| BD-012 BlogObservationCommitted 唯一原子事实 | `tests/observation-projection.test.mjs` `OBS_PROJ_002_zip_recent_tips_with_blog_frame_digests`；REUSE `requirements/behavior-diagnosis/tests/blogger-cycle-atomic-fact.test.mjs` `C0_no_EnforcementCycleCommitted_fact`；REUSE `requirements/context-compression/tests/enforcer-cycle-convergence.test.mjs` `ENFORCER_host_completed_blog_with_live_request_commits_and_advances_coverage`（SPLIT@cutover：convergence 链归 context-compression 后，锚点随文件迁移） | MOVE + REUSE | `node --test tests/observation-projection.test.mjs` |
| BD-013 Coverage 严格推进门（出生门 + PERSIST-010 precheck） | `tests/coverage-birth-gate.test.mjs` `ENFORCER_045_mainContext_refuses_when_next_sequence_cannot_advance` / `ENFORCER_045_mainContext_refuses_unmapped_next_cursor` / `ENFORCER_045_mainContext_accepts_strict_advance`；REUSE `requirements/behavior-diagnosis/tests/enforcer-cycle-commit-branches.test.mjs` `ENFORCER_precheck_stale_ingest_abandons_then_catchup` / `ENFORCER_precheck_cutoff_mismatch_abandons` / `ENFORCER_precheck_epoch_mismatch_after_squash_abandons` | MOVE + REUSE | `node --test tests/coverage-birth-gate.test.mjs` |
| BD-014 每 cycle 恰好一个 tip；RecentTips 有界 8、oldest→newest、重放幂等 | REUSE `requirements/behavior-diagnosis/tests/tip-v2-contract.test.mjs` `ENFORCER_TIP_08_each_committed_cycle_records_exactly_one_tip` / `ENFORCER_TIP_09_replay_preserves_tip` / `ENFORCER_TIP_10_recent_tips_cap_at_8` / `ENFORCER_TIP_11_recent_tips_order_oldest_to_newest`（SPLIT@cutover：本断言族拆入本包） | REUSE | `node --test requirements/behavior-diagnosis/tests/tip-v2-contract.test.mjs` |
| BD-015 tip↔frame 配对，禁平行流 | `tests/observation-pair.test.mjs` `RULEBOOK_OBS_001_zip_equal_length_pairs_tip_then_frame` … `RULEBOOK_OBS_008_workLogFromUnits_uses_unit_digests`；`tests/observation-projection.test.mjs` `OBS_PROJ_001/004` | MOVE | `node --test tests/observation-pair.test.mjs` |
| BD-016 历史压缩不创造新 occurrence | `tests/observation-projection.test.mjs` `OBS_PROJ_003_squash_co_moves_tips_and_frames_as_observation`；REUSE `requirements/behavior-diagnosis/tests/tip-v2-contract.test.mjs` `ENFORCER_TIP_12_squash_co_truncates_recent_tips`（squash 调度语义归 context-compression，本包锁 co-move 不造事件）；REUSE `requirements/behavior-diagnosis/tests/paired-history-eval.test.mjs` `A42_PAIRED_HISTORY_001..004` | MOVE + REUSE | `node --test tests/observation-projection.test.mjs` |
| BD-017 无效 cycle 有界协议修复（idle-only 首发 nudge、tool-error 先 Host 自纠、request+terminal scoped AABB、generic fallback 不抢 AABB、abandoned claim 不冒充 issued） | REUSE `requirements/behavior-diagnosis/tests/enforcer-cycle-protocol.test.mjs` `ENFORCER_066_first_pure_terminal_issues_interaction_nudge` / `ENFORCER_067_second_different_pure_terminal_issues_aabb` / `ENFORCER_068_new_invalid_terminal_after_aabb_*`；`requirements/behavior-diagnosis/tests/enforcer-153-rejudge.test.mjs` `ENFORCER_065_chronicle_tool_error_defers_to_the_host_tool_loop_instead_of_repairing` / `ENFORCER_066_first_protocol_nudge_is_idle_owned_never_sent_from_transform` / `ENFORCER_153_snapshot_rejudge_recognizes_exactly_one_completed_chronicle` / `ENFORCER_153_hot_path_aabb_preserves_target_terminal_identity`；`requirements/behavior-diagnosis/tests/integration/blogger-nudge-plugin-repro.test.mjs` `REPRO_blogger_pure_prose_terminal_idle_should_nudge_without_another_transform` / `REPRO_blogger_aabb_is_sent_even_when_generic_fallback_reaches_exhaustion_on_that_failure`（通用 cursor 11→12 仍先真实发送 AABB，AABB 后新的 invalid terminal 才 fatal）/ `REPRO_blogger_second_prose_terminal_idle_spends_aabb_not_second_nudge` | REUSE + ADD | `node --test requirements/behavior-diagnosis/tests/enforcer-cycle-protocol.test.mjs requirements/behavior-diagnosis/tests/enforcer-153-rejudge.test.mjs requirements/behavior-diagnosis/tests/integration/blogger-nudge-plugin-repro.test.mjs` |

### semantic anchor id

本包不拥有任何 `scripts/checks/semantic-anchors.mjs` anchor id（catalog 只有
ROLE_SEMANTIC_ANCHORS / TOOL_DESCRIPTION_ANCHORS / OFFICE_CAPABILITY_ANCHORS，
归属 cognitive-environment / action-affordance / office-capability 等包）。

### cutover 待办（SPLIT@cutover）

- `requirements/behavior-diagnosis/tests/tip-v2-contract.test.mjs`：拆出 BD-014/BD-016 断言族
  入本包；其余（ENFORCER_TIP_13/14 呈现与 anti-repeat）归 `guidance-delivery`，
  squash 部分归 `context-compression`。
- `requirements/behavior-diagnosis/tests/enforcer-cycle-protocol.test.mjs`、
  `enforcer-cycle-commit-branches.test.mjs`：convergence 生命周期部分随
  `context-compression` 迁移；本包锚点（BD-012/013/017）随文件迁移后更新路径。
- `requirements/behavior-diagnosis/tests/observation-pair.test.mjs` 已在本轮 MOVE，现仅通过注册的 `Enforcer/ObservationSurface.js` 交换 JSON 观察单元；F# identity/list representation 不进入语义合同。
