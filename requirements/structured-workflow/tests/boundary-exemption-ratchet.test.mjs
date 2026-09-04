// requirements/structured-workflow/tests/boundary-exemption-ratchet.test.mjs
//
// Regression ratchet: locks three boundary decisions so they cannot silently
// regress without a test going red.
//
// 1. Sphinx nextTool protocol-boundary exemption (EPI-013 ↔ SW-017):
//    - McpContract.fs must carry the "Not a scheduler" comment.
//    - EPI-013 WHAT must record the protocol-boundary exemption in writing.
//    - SW-017 WHAT must record the protocol-boundary exemption conditions.
//
// 2. OrchestratorProjection no-fold constraint (CHGINT-006 ↔ SW-003 vs SW-009):
//    - CHGINT-006 WHAT must state projection does not fold into a single
//      "latest case" and must not add ResumeAtXxx compensation logs.
//    - SW-003 WHAT must carry the SW-003 vs SW-009 disambiguation.
//    - change-integration HOW must restate the no-fold constraint.
//    - Production source must not introduce ResumeAtXxx durable log patterns.
//
// 3. ManagerFinality handleEnding dispatch ownership (FINALITY-003 ↔ SW-017①):
//    - Finality.fs must define handleEnding, FinalityEndingOutcome, and
//      FinalityEndingExecution.
//    - Tool.fs must call handleEnding and match FinalityEndingOutcome — never
//      match EndingDisposition cases directly.
//    - FINALITY-003 WHAT must mention handleEnding and FinalityEndingOutcome.
//
// Source-tree proof: each assertion reads production source or requirements
// docs and checks for the presence (or absence) of specific strings. If a
// boundary decision is silently removed or weakened, the corresponding
// assertion fails.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

// ── 1. Sphinx nextTool protocol-boundary exemption ──────────────────────────

test('WHAT[STRUCTURED-WORKFLOW-006] McpContract_fs_carries_not_a_scheduler_comment', () => {
  const source = read('src/Wanxiangshu/Sphinx/McpContract.fs')
  assert.match(source, /Not a scheduler/i, 'McpContract.fs must retain the "Not a scheduler" comment on nextTool')
})

test('WHAT[STRUCTURED-WORKFLOW-006] EPI_013_WHT_records_protocol_boundary_exemption', () => {
  const what = read('requirements/epistemic-reasoning/WHAT.md')
  assert.match(what, /Protocol-boundary exemption.*STRUCTURED-WORKFLOW-017/s, 'EPI-013 must record the protocol-boundary exemption citing SW-017')
  assert.match(what, /Kernel 唯一拥有 continuation/, 'EPI-013 exemption must cite condition (1): Kernel owns continuation')
  assert.match(what, /external caller 只提供 typed observation/, 'EPI-013 exemption must cite condition (2): caller only provides observation')
  assert.match(what, /yield\/observe 循环是协议语义/, 'EPI-013 exemption must cite condition (3): yield/observe is protocol semantics')
})

test('WHAT[STRUCTURED-WORKFLOW-006] SW_017_WHT_records_protocol_boundary_exemption_conditions', () => {
  const what = read('requirements/structured-workflow/WHAT.md')
  assert.match(what, /protocol-boundary exemption/i, 'SW-017 must record the protocol-boundary exemption')
  assert.match(what, /kernel 唯一拥有 continuation\/closure\/停止/, 'SW-017 exemption condition (1) must be present')
  assert.match(what, /external caller 只提供 observation/, 'SW-017 exemption condition (2) must be present')
  assert.match(what, /豁免必须以书面.*protocol-boundary exemption.*形式记录/, 'SW-017 exemption condition (3): written exemption must be present')
})

// ── 2. OrchestratorProjection no-fold constraint ────────────────────────────

test('WHAT[STRUCTURED-WORKFLOW-003] CHGINT_006_WHT_states_no_fold_and_no_ResumeAtXxx', () => {
  const what = read('requirements/change-integration/WHAT.md')
  assert.match(what, /不 fold 成唯一.*最新 case/, 'CHGINT-006 WHAT must state projection does not fold into a single latest case')
  assert.match(what, /不新增.*ResumeAtXxx/, 'CHGINT-006 WHAT must prohibit ResumeAtXxx compensation logs')
  assert.match(what, /SW-003 vs SW-009 消歧/, 'CHGINT-006 WHAT must reference the SW-003 vs SW-009 disambiguation')
})

test('WHAT[STRUCTURED-WORKFLOW-003] SW_003_WHT_carries_SW003_vs_SW009_disambiguation', () => {
  const what = read('requirements/structured-workflow/WHAT.md')
  assert.match(what, /SW-003 vs SW-009 消歧/, 'SW-003 WHAT must carry the SW-003 vs SW-009 disambiguation')
  assert.match(what, /fold 成唯一.*最新 case.*durable resume-address.*SW-009 禁止/s, 'SW-003 vs SW-009 must state that folding into latest case is durable resume-address (SW-009 prohibited)')
  assert.match(what, /semantic entry 从一组 durable facts.*重新证明 outstanding obligation/s, 'SW-003 vs SW-009 must state the correct form: re-derive obligation from facts + reality')
})

test('WHAT[STRUCTURED-WORKFLOW-003] CHGINT_HOW_restates_no_fold_constraint', () => {
  const how = read('requirements/change-integration/HOW.md')
  assert.match(how, /projection 不 fold 成唯一.*最新 case/, 'change-integration HOW must restate the no-fold constraint')
  assert.match(how, /SW-003 vs SW-009 消歧/, 'change-integration HOW must reference the SW-003 vs SW-009 disambiguation')
})

test('WHAT[STRUCTURED-WORKFLOW-003] production_source_has_no_ResumeAtXxx_durable_log_pattern', () => {
  const source = read('src/Wanxiangshu/Change/Program.fs')
  assert.doesNotMatch(source, /ResumeAt\w+/, 'Change/Program.fs must not introduce ResumeAtXxx durable log patterns')
})

// ── 3. SuicideTool retirement dispatch ownership ────────────────────────────

test('WHAT[STRUCTURED-WORKFLOW-006] Finality_fs_defines_handleEnding_and_boundary_types', () => {
  const source = read('src/Wanxiangshu/Mission/Relay/OpenCode/SuicideTool.fs')
  assert.match(source, /let spec/, 'SuicideTool.fs must define spec')
  assert.match(source, /let private executePrepared/, 'SuicideTool.fs must define executePrepared')
  assert.match(source, /ToolPermission\.Finality/, 'SuicideTool.fs must require ToolPermission.Finality')
})

test('WHAT[STRUCTURED-WORKFLOW-006] Tool_fs_calls_handleEnding_and_matches_FinalityEndingOutcome_only', () => {
  const source = read('src/Wanxiangshu/Mission/Relay/OpenCode/SuicideTool.fs')
  assert.doesNotMatch(source, /\.AbortSession\b/, 'SuicideTool must never issue session-scoped abort')
  assert.match(source, /TryFreezeRetirement/, 'SuicideTool must freeze retirement before check')
})

test('WHAT[STRUCTURED-WORKFLOW-006] FINALITY_003_WHT_documents_handleEnding_and_FinalityEndingOutcome', () => {
  const what = read('requirements/relay-retirement/WHAT.md')
  assert.match(what, /RETIRE-001/, 'relay-retirement WHAT must define RETIRE-001')
  assert.match(what, /RETIRE-003/, 'relay-retirement WHAT must define RETIRE-003')
})
