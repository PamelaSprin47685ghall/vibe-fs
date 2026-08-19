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

test('WHAT[REVIEW-ASSURANCE-002] HOST_reverify_normal_terminal_before_first_judgement_nudges_without_a_waiter_gap', { timeout: 3000 }, async () => {
  let live
  let reviewerDriver
  let sends = 0
  live = await liveOrchestrator({
    sessionBehaviour: {
      onSendPrompt: (reviewerId, prompt, _options, meta) => {
        sends += 1

        if (sends === 1) {
          queueMicrotask(() =>
            live.sessions.notifyTerminal(reviewerId, {
              kind: 'Completed',
              sessionId: reviewerId,
              authorityRoot: 'root-before-first',
              providerRun: 'run-before-first-terminal',
              role: 'Reviewer',
              terminalText: 'ended without judge',
              turnFormalText: 'ended without judge',
            }),
          )
          return
        }

        assert.equal(sends, 2, 'one clean terminal must produce exactly one immediate nudge')
        assert.match(prompt, /judge/i)
        assert.match(prompt, /上一轮回复没有调用 judge|previous.*judge/i)

        const response = reviewHost.deliverJudgement(
          reviewerId,
          meta.physical,
          'run-after-first-nudge',
          'call-after-first-nudge',
          'REVISE',
        )
        assert.ok(response, 'the original Finality CE judgement waiter must survive the clean terminal')

        reviewerDriver = (async () => {
          assert.deepEqual(await response, { ok: true, effect: 'Accepted' })
          live.sessions.notifyTerminal(reviewerId, {
            kind: 'Completed',
            sessionId: reviewerId,
            authorityRoot: 'root-before-first',
            providerRun: 'run-after-first-nudge',
            role: 'Reviewer',
            terminalText: 'revision requested',
            turnFormalText: 'revision requested',
          })
        })()
      },
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

    await reviewerDriver
    assert.equal(result.ok, false)
    assert.match(result.error, /Reviewer requested revision/)
    assert.equal(sends, 2)
  } finally {
    live.cleanup()
    rmSync(worktree, { recursive: true, force: true })
  }
})

test('WHAT[REVIEW-ASSURANCE-002] HOST_reverify_normal_terminal_before_second_judgement_nudges_and_confirms', { timeout: 3000 }, async () => {
  let live
  let reviewerDriver
  let sends = 0
  let firstPhysical
  let secondPhysical
  live = await liveOrchestrator({
    sessionBehaviour: {
      onSendPrompt: (reviewerId, prompt, _options, meta) => {
        sends += 1

        if (sends === 1) {
          firstPhysical = meta.physical
          const firstResponse = reviewHost.deliverJudgement(
            reviewerId,
            firstPhysical,
            'run-terminal-before-second-1',
            'call-terminal-before-second-1',
            'PERFECT',
          )
          assert.ok(firstResponse, 'first PERFECT must enter the direct-CE judgement slot')

          reviewerDriver = (async () => {
            assert.deepEqual(await firstResponse, { ok: true, effect: 'Challenge' })
            live.sessions.notifyTerminal(reviewerId, {
              kind: 'Completed',
              sessionId: reviewerId,
              authorityRoot: 'root-before-second',
              providerRun: 'run-terminal-before-second-1',
              role: 'Reviewer',
              terminalText: 'ended after challenge',
              turnFormalText: 'ended after challenge',
            })
          })()
          return
        }

        assert.equal(sends, 2, 'clean terminal after first PERFECT must immediately nudge')
        assert.match(prompt, /judge/i)
        secondPhysical = meta.physical
        assert.notEqual(secondPhysical, firstPhysical, 'nudge is a fresh physical continuation')

        const secondResponse = reviewHost.deliverJudgement(
          reviewerId,
          secondPhysical,
          'run-terminal-before-second-2',
          'call-terminal-before-second-2',
          'PERFECT',
        )
        assert.ok(secondResponse, 'the second judgement waiter must already exist before the nudge is sent')

        reviewerDriver = (async () => {
          assert.deepEqual(await secondResponse, { ok: true, effect: 'Accepted' })
          live.sessions.notifyTerminal(reviewerId, {
            kind: 'Completed',
            sessionId: reviewerId,
            authorityRoot: 'root-before-second',
            providerRun: 'run-terminal-before-second-2',
            role: 'Reviewer',
            terminalText: 'review confirmed after nudge',
            turnFormalText: 'review confirmed after nudge',
          })
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
    assert.equal(result.ok, true, result.ok ? '' : result.error)
    assert.equal(sends, 2)
    assert.notEqual(firstPhysical, secondPhysical)
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
