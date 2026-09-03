# behavior-diagnosis — HOW

## 架构机制与核心模型

### 1. 规则库装载与合流模型

1. **Catalog 结构与合流**：
   - 规则目录由 built-in 资源与经校验的 `InstitutionalRuleBorn` durable 事件动态合流，生成统一的 `EnforcerCatalog`。
   - 纯函数 `validate` 校验规则唯一性、连续派生 LexicalOrder `1..N` 及双语正文非空性。
   - `resolveByField` 执行精确匹配与 Levenshtein 编辑距离归一化，并列时按 LexicalOrder 决胜。

2. **System Prompt 确定性合成**：
   - 按 LexicalOrder 拼接 `# Enforcer Rulebook` 与全部规则正文，保证相同 catalog 输入生成完全一致的 prompt 字节。
   - `main.md` 补救手册不进入 Blogger system prompt，保持检测与补救的受众隔离。

### 2. Cycle 校验、提交与投影

1. **基数门禁与解码**：
   - 原始 assistant 步骤中 `chronicle` 调用数必须精确为 1。0 次或 2+ 次直接转入协议修复。
   - 解码器解析 `entry`、`tip` 与可选 `evidence`，验证文本非空；`EnforcerCycle.validateContentBounds` 唯一拥有内容大小阈值、字节计数结果、typed rejection 与 text-first 拒绝顺序（文本 ≤ 512 KiB，证据 ≤ 128 KiB）。Decode 与 JS Surface 只注入 canonical UTF-8 `byteCount` 并消费同一纯 decision。
   - 若无活跃 Blogger cycle，产生类型化 `NoLiveCycle` 结果并清理过时会话。

2. **原子提交与 Coverage 出生门**：
   - 提交前校验待覆盖序列严格单调递增，且 staged cursor/cutoff/epoch 与当前投影一致。
   - 写入日志与证据 blob 后，原子追加 `BlogObservationCommitted` 事件，同步推进 coverage 与日志记录。
   - 提交后派生单一 RecentTip，在投影中维护容量为 8 的有序滑动窗口。

3. **配对与压缩**：
   - `pairTipsAndFrames` 将 tips 与 frame digests 组合为配对观察单元，提供自洽的历史诊断事实。
   - squash 操作以 1:1 比例协同折叠 frames 并共同裁剪 RecentTips，不产生新事件。

### 3. 有界协议修复与 Life 冻结

1. **有界 Nudge 与 AABB 机制**：
   - 首次无效 terminal 由 `SessionIdle` 触发专用 Nudge，每个 RequestId 仅限一次。
   - 再次无效必须由新的 ProviderRunIdentity 证明后才进入 AABB；同一 terminal 的重复 Nudge admission 返回 typed `AlreadyAdmitted` 并等待，不得作为失败推进恢复。
   - send admission 使用 typed outcome 区分 `Sent / AlreadyAdmitted / Superseded / Retired / NotSent / Failed`；`NotSent` 仅表示 acceptance 前确定拒绝，可安全释放 exact gate reservation，禁止通过错误字符串反解析幂等或重试状态。
   - 恢复重判仅承认包含 `chronicle` 的 completed 状态，杜绝模糊猜测。

2. **RulebookRevision 冻结**：
   - Blogger life 在创建时固定其绑定的 `RulebookRevision`，保持 system prompt、工具枚举与解码器版本完全一致。
   - 新规则产生仅更新全局最新 revision，当前活跃 cycle 继续在原 revision 下完成，待下一 fresh life 生效。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| BD-001 | `requirements/behavior-diagnosis/tests/catalog.test.mjs::WHAT[BD-001] ENFORCER_170_catalog_has_exactly_120_rules` |
| BD-002 | `requirements/behavior-diagnosis/tests/catalog.test.mjs::WHAT[BD-002] ENFORCER_170_rule_ids_are_unique` |
| BD-003 | `requirements/behavior-diagnosis/tests/catalog-validation.test.mjs::WHAT[BD-003] ENFORCER_170_validate_accepts_packaged_catalog_n_rules` |
| BD-004 | `requirements/behavior-diagnosis/tests/rulebook-system-composition.test.mjs::WHAT[BD-004] BEHAVIOR_DIAGNOSIS_SYSTEM_001_composed_prompt_contains_every_tip_exactly_once` |
| BD-005 | `requirements/behavior-diagnosis/tests/rulebook-system-composition.test.mjs::WHAT[BD-005] BEHAVIOR_DIAGNOSIS_SYSTEM_003_zh_cn_leaf_load_is_complete_and_nonempty` |
| BD-006 | `requirements/behavior-diagnosis/tests/codec.test.mjs::WHAT[BD-006] ENFORCER_023_missing_tip_fails` |
| BD-007 | `requirements/behavior-diagnosis/tests/codec.test.mjs::WHAT[BD-007] ENFORCER_023_nonempty_unknown_tip_resolves` |
| BD-008 | `requirements/behavior-diagnosis/tests/codec.test.mjs::WHAT[BD-008] ENFORCER_024_extra_numeric_properties_are_ignored` |
| BD-009 | `requirements/behavior-diagnosis/tests/enforcer-cycle-protocol.test.mjs::WHAT[BD-009] ENFORCER_061_exactly_one_valid_call_stops_physical_run` |
| BD-010 | `requirements/behavior-diagnosis/tests/identity-fail-closed.test.mjs::WHAT[BD-010] ENFORCER_043_no_provable_provider_run_fails_closed` |
| BD-011 | `requirements/behavior-diagnosis/tests/bounds.test.mjs::WHAT[BD-011] ENFORCER_043_canonical_text_over_512KiB_fails_closed`；`requirements/behavior-diagnosis/tests/bounds.test.mjs::WHAT[BD-011] bounds ownership gate rejects consumer and newly added decoys`；`scripts/checks/enforcer-bounds-owner.mjs` |
| BD-012 | `requirements/behavior-diagnosis/tests/observation-projection.test.mjs::WHAT[BD-012] OBS_PROJ_002_zip_recent_tips_with_blog_frame_digests` |
| BD-013 | `requirements/behavior-diagnosis/tests/coverage-birth-gate.test.mjs::WHAT[BD-013] ENFORCER_045_mainContext_refuses_when_next_sequence_cannot_advance` |
| BD-014 | `requirements/behavior-diagnosis/tests/tip-v2-contract.test.mjs::WHAT[BD-014] ENFORCER_TIP_08_each_committed_cycle_records_exactly_one_tip` |
| BD-015 | `requirements/behavior-diagnosis/tests/observation-pair.test.mjs::WHAT[BD-015] RULEBOOK_OBS_001_zip_equal_length_pairs_tip_then_frame` |
| BD-016 | `requirements/behavior-diagnosis/tests/observation-projection.test.mjs::WHAT[BD-016] OBS_PROJ_003_squash_co_moves_tips_and_frames_as_observation` |
| BD-017 | `requirements/behavior-diagnosis/tests/enforcer-cycle-protocol.test.mjs::WHAT[BD-017] ENFORCER_066_first_pure_terminal_issues_interaction_nudge` |
| BD-018 | `requirements/behavior-diagnosis/tests/rulebook-system-composition.test.mjs::WHAT[BD-018] BEHAVIOR_DIAGNOSIS_SYSTEM_005_rulebook_revision_freezes_system_prompt_and_tool_definitions` |
| BD-019 | `requirements/behavior-diagnosis/tests/m6-fatal-boundary.test.mjs::WHAT[BD-019] Enforcer fatal requires typed settlement and one injected fuse` |
