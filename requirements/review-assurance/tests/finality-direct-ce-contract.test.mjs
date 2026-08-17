// Finality review temporal ownership: the dual-PERFECT protocol is one F# CE story.
// No transcript parsing, Journal-integrated program position, or review state machine may own its next step.

import assert from 'node:assert/strict'
import { existsSync, readFileSync } from 'node:fs'
import test from 'node:test'

const read = (path) => readFileSync(new URL(`../../../${path}`, import.meta.url), 'utf8')

const reverify = () => read('src/Wanxiangshu/Mission/Review/Barrier/Reverify.fs')
const verdict = () => read('src/Wanxiangshu/Mission/Review/Judgement/Verdict.fs')
const witness = () => read('src/Wanxiangshu/Mission/Review/Judgement/Witness.fs')
const projection = () => read('src/Wanxiangshu/Mission/Review/Barrier/Projection.fs')
const judgeTool = () => read('src/Wanxiangshu/Mission/Review/OpenCode/JudgeTool.fs')
const turnWorkflow = () => read('src/Wanxiangshu/Composition/Turn/Workflow.fs')
const project = () => read('src/Wanxiangshu/Wanxiangshu.fsproj')

test('WHAT[REVIEW-ASSURANCE-002] REVIEW_CE_001_finality_dual_perfect_has_no_persisted_program_position', () => {
  const text = [reverify(), verdict(), witness(), projection()].join('\n')
  for (const forbidden of [
    'ReviewStatus',
    'PerfectPending',
    'PendingChallenge',
    'ProviderInputSeal',
    'ChallengeUnproven',
    'PerfectChallengeIssued',
  ]) {
    assert.equal(text.includes(forbidden), false, `${forbidden} is a forbidden Finality review program-position encoding`)
  }
})

test('WHAT[REVIEW-ASSURANCE-007] REVIEW_CE_002_finality_confirmation_never_parses_provider_text_or_seals_it', () => {
  const controlPath = [reverify(), verdict(), witness(), projection(), judgeTool()].join('\n')
  for (const forbidden of ['toolResultDigests', 'ProviderWire', 'WireText', 'ProviderInputSeal', 'ChallengeContentDigest']) {
    assert.equal(controlPath.includes(forbidden), false, `${forbidden} cannot prove Finality causality`)
  }
  assert.equal(project().includes('Mission/Review/Assurance/Seal.fs'), false, 'the provider-input seal runtime is not compiled')
})

test('WHAT[REVIEW-ASSURANCE-001] REVIEW_CE_003_reverify_is_the_direct_ce_temporal_owner', () => {
  const source = reverify()
  assert.match(source, /task\s*\{/)
  assert.match(source, /host\.AwaitJudgement\(\)/)
  assert.match(source, /firstDelivery\.Challenge\(\)/)
  assert.match(source, /secondAwait/)
  assert.match(source, /VerdictWorkflow\.recordConfirmation/)
  assert.doesNotMatch(source, /ReviewJudgementReply|SendChallenge|AgentJournal\.snapshot|readStatus|readOutcome|classifyGuard/)
})

test('WHAT[REVIEW-ASSURANCE-010] REVIEW_CE_004_transient_reviewer_failures_remain_in_provider_recovery', () => {
  const source = turnWorkflow()

  assert.match(source, /Some Role\.Reviewer, _, ReconcileProgram\.TurnCompleted ->/)
  assert.match(source, /Some Role\.Reviewer, _, _ -> do! observeIdleOrdinary context/)
  assert.match(
    source,
    /match turn\.Role, turn\.Outcome with[\s\S]*?\| Some Role\.Reviewer, ReconcileProgram\.TurnCompleted ->[\s\S]*?ReviewerWorkflow\.observe[\s\S]*?\| Some Role\.Reviewer, _ -> do! observeOrdinary context/,
  )
})
