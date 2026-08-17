// Split from tests/unit/orchestrator/host.test.mjs (cutover Wave 2a); owner: review-assurance.
//
// HOST_reverify_* — the reverify/review-barrier surface: a fresh deep reviewer
// child per barrier, ReviewBarrierStarted durable BEFORE the first reviewer
// prompt, and fail-closed before lane start without a journal (REVIEW-008 fresh
// barrier semantics; matches review-guard.test.mjs).

import assert from 'node:assert/strict'
import { rmSync } from 'node:fs'
import test from 'node:test'

import {
  gitDir,
  liveOrchestrator,
} from '../../verification-system/tests/support/orchestrator-host-harness.mjs'
import * as hostSurface from '../../../dist/Change/Host/Surface.js'
import * as reviewHost from '../../../dist/Mission/Review/OpenCode/ReviewHostSurface.js'
import * as reviewJournal from '../../../dist/Persistence/Journal/ReviewJournalSurface.js'

test('WHAT[REVIEW-ASSURANCE-006] HOST_reverify_durably_opens_barrier_before_first_reviewer_prompt', async () => {
  let live
  let barrierVisibleAtSend = false
  let reviewPrompt = ''
  live = await liveOrchestrator({
    sessionBehaviour: {
      onSendPrompt: (reviewerId, prompt, _options, meta) => {
        reviewPrompt = prompt
        const projection = reviewJournal.sessionViewRaw(live.journal, reviewerId)
        barrierVisibleAtSend = projection?.barrier != null

        const acknowledgement = reviewHost.deliverJudgement(
          reviewerId,
          meta.physical,
          'run-order-probe',
          'call-order-probe',
          'REVISE',
        )
        assert.ok(acknowledgement, 'the direct CE must own the judgement inbox before starting the reviewer')
      },
      terminalAfterSend: 'stop-after-order-probe',
    },
  })
  const worktree = gitDir('rv-order')
  try {
    const result = await reviewHost.reverify(
      hostSurface.managerPort(live.host),
      'hostfw-order',
      'ses_mgr_order',
      worktree,
      'bar_order',
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

test('WHAT[REVIEW-ASSURANCE-002] HOST_reverify_accepts_second_PERFECT_after_typed_challenge_on_same_physical_prompt', async () => {
  const delivered = []
  const sent = []
  let reviewerDriver
  let live

  live = await liveOrchestrator({
    sessionBehaviour: {
      onSendPrompt: (reviewerId, prompt, _options, meta) => {
        const physical = meta.physical
        sent.push({ prompt, physical })
        assert.equal(sent.length, 1, 'Finality challenge is a judge tool result, not a second user prompt')

        const first = {
          run: 'run-perfect-1',
          call: 'call-perfect-1',
          physical,
        }
        delivered.push(first)

        const firstResponse = reviewHost.deliverJudgement(
          reviewerId,
          first.physical,
          first.run,
          first.call,
          'PERFECT',
        )
        assert.ok(firstResponse, 'first PERFECT must enter the armed direct-CE judgement slot')

        reviewerDriver = (async () => {
          const firstReply = await firstResponse
          assert.deepEqual(firstReply, { ok: true, effect: 'Challenge' })

          const second = {
            run: 'run-perfect-2',
            call: 'call-perfect-2',
            physical: first.physical,
          }
          delivered.push(second)

          const secondResponse = reviewHost.deliverJudgement(
            reviewerId,
            second.physical,
            second.run,
            second.call,
            'PERFECT',
          )
          assert.ok(secondResponse, 'Challenge() must be invoked only after the second judgement waiter is registered')
          assert.deepEqual(await secondResponse, { ok: true, effect: 'Accepted' })

          live.sessions.notifyTerminal(
            reviewerId,
            {
              kind: 'Completed',
              sessionId: reviewerId,
              authorityRoot: 'root-perfect',
              providerRun: 'run-perfect-2',
              role: 'Reviewer',
              terminalText: 'review confirmed',
              turnFormalText: 'review confirmed',
            },
          )
        })()
      },
    },
  })

  const worktree = gitDir('rv-perfect')
  try {
    const result = await reviewHost.reverify(
      hostSurface.managerPort(live.host),
      'hostfw-perfect',
      'ses_mgr_perfect',
      worktree,
      'bar_perfect',
    )

    await reviewerDriver
    assert.equal(result.ok, true, result.ok ? '' : result.error)
    assert.equal(sent.length, 1, 'the entire dual-PERFECT uses one physical user prompt')
    assert.equal(delivered.length, 2)
    assert.equal(
      delivered[0].physical,
      delivered[1].physical,
      'the second judgement must be downstream of the first tool-result challenge on the same prompt',
    )
    assert.notEqual(delivered[0].run, delivered[1].run)
    assert.notEqual(delivered[0].call, delivered[1].call)
  } finally {
    live.cleanup()
    rmSync(worktree, { recursive: true, force: true })
  }
})

test('WHAT[REVIEW-ASSURANCE-002] HOST_reverify_terminal_before_first_judgement_fails_closed_without_hanging', { timeout: 2000 }, async () => {
  const live = await liveOrchestrator({
    sessionBehaviour: {
      terminalAfterSend: 'reviewer-terminal-before-judge',
    },
  })
  const worktree = gitDir('rv-terminal-before-judge')
  try {
    const result = await reviewHost.reverify(
      hostSurface.managerPort(live.host),
      'hostfw-terminal-before-judge',
      'ses_mgr_terminal_before_judge',
      worktree,
      'bar_terminal_before_judge',
    )

    assert.equal(result.ok, false)
    assert.match(result.error, /Cannot await reviewer: reviewer-terminal-before-judge/)
  } finally {
    live.cleanup()
    rmSync(worktree, { recursive: true, force: true })
  }
})

test('WHAT[REVIEW-ASSURANCE-002] HOST_reverify_terminal_before_second_judgement_fails_closed_without_hanging', { timeout: 2000 }, async () => {
  let live
  let reviewerDriver
  live = await liveOrchestrator({
    sessionBehaviour: {
      onSendPrompt: (reviewerId, _prompt, _options, meta) => {
        const firstResponse = reviewHost.deliverJudgement(
          reviewerId,
          meta.physical,
          'run-terminal-before-second-1',
          'call-terminal-before-second-1',
          'PERFECT',
        )
        assert.ok(firstResponse, 'first PERFECT must enter the direct-CE judgement slot')

        reviewerDriver = (async () => {
          assert.deepEqual(await firstResponse, { ok: true, effect: 'Challenge' })
          live.sessions.notifyTerminal(reviewerId, { kind: 'Failed', error: 'reviewer-terminal-before-second-judge' })
        })()
      },
    },
  })
  const worktree = gitDir('rv-terminal-before-second-judge')
  try {
    const result = await reviewHost.reverify(
      hostSurface.managerPort(live.host),
      'hostfw-terminal-before-second-judge',
      'ses_mgr_terminal_before_second_judge',
      worktree,
      'bar_terminal_before_second_judge',
    )

    await reviewerDriver
    assert.equal(result.ok, false)
    assert.match(result.error, /Cannot await reviewer: reviewer-terminal-before-second-judge/)
  } finally {
    live.cleanup()
    rmSync(worktree, { recursive: true, force: true })
  }
})

test('WHAT[REVIEW-ASSURANCE-006] HOST_reverify_rejects_completed_terminal_with_unknown_role', async () => {
  let live
  live = await liveOrchestrator({
    sessionBehaviour: {
      onSendPrompt: (reviewerId) => {
        queueMicrotask(() =>
          live.sessions.notifyTerminal(reviewerId, {
            kind: 'Completed',
            sessionId: reviewerId,
            authorityRoot: 'root-invalid-role',
            providerRun: 'run-invalid-role',
            role: 'NotARealRole',
            terminalText: 'should not complete',
            turnFormalText: 'should not complete',
          }),
        )
      },
    },
  })
  const worktree = gitDir('rv-invalid-role')
  try {
    const result = await reviewHost.reverify(
      hostSurface.managerPort(live.host),
      'hostfw-invalid-role',
      'ses_mgr_invalid_role',
      worktree,
      'bar_invalid_role',
    )
    assert.equal(result.ok, false)
    assert.match(result.error, /invalid role/)
  } finally {
    live.cleanup()
    rmSync(worktree, { recursive: true, force: true })
  }
})

test('WHAT[REVIEW-ASSURANCE-006] HOST_reverify_forks_a_deep_reviewer_and_fails_closed_without_a_journal', async () => {
  const live = await liveOrchestrator({ journal: false })
  const worktree = gitDir('rvf')
  try {
    const result = await reviewHost.reverify(
      hostSurface.managerPort(live.host),
      'hostfw14',
      'ses_mgr14',
      worktree,
      'bar_14',
    )
    assert.equal(result.ok, false)
    assert.match(result.error, /Review journal is unavailable/, 'reverify without a journal fails closed before lane start')

    assert.equal(
      live.sessions.calls.filter(([name]) => name === 'CreateChildSession').length,
      0,
      'journal absence must fail before creating a reviewer child',
    )
    assert.equal(live.sessions.calls.filter(([name]) => name === 'SendPrompt').length, 0, 'no reviewer prompt is sent without a durable barrier')
    assert.equal(hostSurface.hasChild(live.host, 'hostfw14-reviewer-bar_14'), false)
  } finally {
    live.cleanup()
    rmSync(worktree, { recursive: true, force: true })
  }
})
