// Split from tests/unit/orchestrator/host.test.mjs (cutover Wave 2a); owner: review-assurance.
//
// HOST_reverify_* — the reverify/review-barrier surface: a fresh deep reviewer
// child per barrier, ReviewBarrierStarted durable BEFORE the first reviewer
// prompt, and fail-closed before lane start without a journal (REVIEW-008 fresh
// barrier semantics; matches review-guard.test.mjs). Job fork/continue/join
// assertions from the same source file moved to
// requirements/change-integration/tests/host.test.mjs.

import assert from 'node:assert/strict'
import { rmSync } from 'node:fs'
import test from 'node:test'

import {
  agentJournal,
  managerJobId,
  mapEntries,
  resultOf,
  sessionId,
  reviewBarrierId,
  worktreePath,
} from '../../verification-system/tests/support/domain.mjs'
import {
  gitDir,
  liveOrchestrator,
} from '../../verification-system/tests/support/orchestrator-host-harness.mjs'

test('HOST_reverify_durably_opens_barrier_before_first_reviewer_prompt', async () => {
  let live
  let barrierVisibleAtSend = false
  let reviewPrompt = ''
  live = await liveOrchestrator({
    sessionBehaviour: {
      onSendPrompt: (reviewerId, prompt) => {
        reviewPrompt = prompt
        const reviewerKey = reviewerId?.fields?.[0] ?? reviewerId
        const projection = mapEntries(agentJournal.snapshot(live.journal).AgentProjections.Sessions)
          .find(([sid]) => (sid?.fields?.[0] ?? sid) === reviewerKey)?.[1]
        barrierVisibleAtSend = projection?.ReviewGuard != null
      },
      terminalAfterSend: 'stop-after-order-probe',
    },
  })
  const worktree = gitDir('rv-order')
  try {
    const result = resultOf(
      await live.host.managerPort.Reverify(
        managerJobId('hostfw-order'),
        sessionId('ses_mgr_order'),
        worktreePath(worktree),
        reviewBarrierId('bar_order'),
      ),
    )
    assert.equal(result.ok, false, 'probe terminates the reviewer after observing send order')
    assert.equal(barrierVisibleAtSend, true, 'reviewer provider lane must not start before ReviewBarrierStarted is durable')
    assert.match(reviewPrompt, /judge tool/, 'orchestrator review must use the shared Reviewer opening resource')
    assert.doesNotMatch(reviewPrompt, /verdict tool/, 'orchestrator review must not retain the removed hard-coded tool name')
  } finally {
    live.cleanup()
    rmSync(worktree, { recursive: true, force: true })
  }
})

test('HOST_reverify_forks_a_deep_reviewer_and_fails_closed_without_a_journal', async () => {
  const live = await liveOrchestrator({ journal: false })
  const worktree = gitDir('rvf')
  try {
    const result = resultOf(
      await live.host.managerPort.Reverify(
        managerJobId('hostfw14'),
        sessionId('ses_mgr14'),
        worktreePath(worktree),
        reviewBarrierId('bar_14'),
      ),
    )
    assert.equal(result.ok, false)
    assert.match(result.error, /Cannot open review barrier.*AgentJournal/, 'reverify without a journal fails closed before lane start')

    const created = live.sessions.calls.filter(([name]) => name === 'CreateChildSession')
    assert.ok(created.length >= 1, 'a reviewer child session was prepared')
    assert.equal(live.sessions.calls.filter(([name]) => name === 'SendPrompt').length, 0, 'no reviewer prompt is sent before a durable barrier exists')
    assert.equal(live.host.runtime.children.has('hostfw14-reviewer-bar_14'), true)
  } finally {
    live.cleanup()
    rmSync(worktree, { recursive: true, force: true })
  }
})
