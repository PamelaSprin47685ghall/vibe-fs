// tests/unit/orchestrator/program.test.mjs — FLOW-002/003 OrchestratorProgram AST.
//
// M2: Domain program is reply-bearing data. TraceInterpreter walks it with a
// canned reply policy. Program builders (OrchestratorPrograms) assemble the
// production control-flow shapes. Production execution lives in
// Application/Orchestration/OrchestratorInterpreter — not Application/.../Program.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  commitHash,
  listItems,
  managerJobId,
  orchestratorProgram,
  sessionId,
  targetRef,
  worktreePath,
} from '../support/domain.mjs'

const JOB = {
  jobId: managerJobId('job-1'),
  managerSessionId: sessionId('ses-manager-1'),
  worktree: worktreePath('/tmp/wt-job-1'),
  targetRef: targetRef('refs/heads/main'),
}

test('ORCHESTRATOR_PROGRAM_001: trace of empty program', () => {
  const trace = listItems(orchestratorProgram.interpret(orchestratorProgram.empty))
  assert.deepEqual(trace, ['Return(None)'])
})

test('ORCHESTRATOR_PROGRAM_002: reply-bearing interpretWith drives Step continuations', () => {
  assert.equal(typeof orchestratorProgram.interpretWith, 'function')
  assert.equal(typeof orchestratorProgram.reply.unitOk, 'function')

  // Empty program still returns Return(None) under any reply policy.
  const emptyTrace = listItems(
    orchestratorProgram.interpretWith(() => orchestratorProgram.reply.unitOk(), orchestratorProgram.empty),
  )
  assert.deepEqual(emptyTrace, ['Return(None)'])
})

test('ORCHESTRATOR_PROGRAM_003: freshStart trace is AwaitManager → review → rebase → publish → release', () => {
  assert.equal(typeof orchestratorProgram.programs.freshStart, 'function')

  const program = orchestratorProgram.programs.freshStart(JOB)
  const trace = listItems(orchestratorProgram.interpretWithDefaults(program))

  assert.ok(trace.includes('AwaitManager(job-1)'), `trace: ${trace.join(' → ')}`)
  assert.ok(
    trace.some((step) => step.startsWith('ReviewRound(job-1, pre-rebase, 0)')),
    `pre-rebase review missing; trace: ${trace.join(' → ')}`,
  )
  assert.ok(
    trace.some((step) => step.startsWith('RecordCandidateReady(job-1,')),
    `candidate ready missing; trace: ${trace.join(' → ')}`,
  )
  assert.ok(
    trace.some((step) => step.startsWith('ReadTargetHead(')),
    `target head missing; trace: ${trace.join(' → ')}`,
  )
  assert.ok(
    trace.some((step) => step.startsWith('RebaseOnto(')),
    `rebase missing; trace: ${trace.join(' → ')}`,
  )
  assert.ok(
    trace.some((step) => step.startsWith('ReviewRound(job-1, post-rebase, 0)')),
    `post-rebase review missing; trace: ${trace.join(' → ')}`,
  )
  assert.ok(
    trace.some((step) => step.startsWith('RecordRebasedReady(job-1,')),
    `rebased ready missing; trace: ${trace.join(' → ')}`,
  )
  assert.ok(
    trace.some((step) => step.startsWith('PublishUnderGate(job-1,')),
    `publish missing; trace: ${trace.join(' → ')}`,
  )
  assert.ok(
    trace.some((step) => step.startsWith('ReleaseWorktree(')),
    `release missing; trace: ${trace.join(' → ')}`,
  )
  assert.ok(
    trace.some((step) => step.startsWith('Return(Ok ')),
    `successful return missing; trace: ${trace.join(' → ')}`,
  )

  // Order: AwaitManager before pre-rebase review; pre-rebase before publish; publish before release.
  const awaitAt = trace.indexOf('AwaitManager(job-1)')
  const preReviewAt = trace.findIndex((step) => step.startsWith('ReviewRound(job-1, pre-rebase, 0)'))
  const publishAt = trace.findIndex((step) => step.startsWith('PublishUnderGate(job-1,'))
  const releaseAt = trace.findIndex((step) => step.startsWith('ReleaseWorktree('))
  assert.ok(awaitAt >= 0 && awaitAt < preReviewAt, 'AwaitManager must precede pre-rebase review')
  assert.ok(preReviewAt < publishAt, 'pre-rebase review must precede publish')
  assert.ok(publishAt < releaseAt, 'publish must precede worktree release')
})

test('ORCHESTRATOR_PROGRAM_004: TargetMoved on publish re-enters rebaseReviewPublish at next round', () => {
  assert.equal(typeof orchestratorProgram.programs.freshStart, 'function')
  assert.equal(typeof orchestratorProgram.reply.publishTargetMoved, 'function')
  assert.equal(typeof orchestratorProgram.reply.publishLanded, 'function')

  let publishCount = 0
  const reply = (command) => {
    const name = orchestratorProgram.commandName(command)
    if (name === 'PublishUnderGate') {
      publishCount += 1
      if (publishCount === 1) return orchestratorProgram.reply.publishTargetMoved()
      return orchestratorProgram.reply.publishLanded(commitHash('landed-head'))
    }
    return orchestratorProgram.defaultReply(command)
  }

  const program = orchestratorProgram.programs.freshStart(JOB)
  const trace = listItems(orchestratorProgram.interpretWith(reply, program))

  assert.equal(publishCount, 2, 'publish must run again after TargetMoved')
  assert.ok(
    trace.some((step) => step.startsWith('ReviewRound(job-1, post-rebase, 1)')),
    `round-1 post-rebase review missing after TargetMoved; trace: ${trace.join(' → ')}`,
  )
  assert.ok(
    trace.some((step) => step.startsWith('Return(Ok landed-head)')),
    `final land missing; trace: ${trace.join(' → ')}`,
  )
})

test('ORCHESTRATOR_PROGRAM_005: resume BackfillPublished is AppendFact → TerminateChildren → Release → Ok', () => {
  assert.equal(typeof orchestratorProgram.programs.resumeBackfillPublished, 'function')

  const program = orchestratorProgram.programs.resumeBackfillPublished(JOB, {
    rebased: commitHash('rebased-head'),
    resultingHead: commitHash('resulting-head'),
  })
  const trace = listItems(orchestratorProgram.interpretWithDefaults(program))

  assert.ok(trace.includes('AppendFact'), `AppendFact missing; trace: ${trace.join(' → ')}`)
  assert.ok(
    trace.includes('TerminateChildren(job-1)'),
    `TerminateChildren missing; trace: ${trace.join(' → ')}`,
  )
  assert.ok(
    trace.some((step) => step.startsWith('ReleaseWorktree(')),
    `release missing; trace: ${trace.join(' → ')}`,
  )
  assert.deepEqual(
    trace.filter((step) => step.startsWith('Return(')),
    ['Return(Ok resulting-head)'],
  )

  const appendAt = trace.indexOf('AppendFact')
  const terminateAt = trace.indexOf('TerminateChildren(job-1)')
  const releaseAt = trace.findIndex((step) => step.startsWith('ReleaseWorktree('))
  assert.ok(appendAt < terminateAt && terminateAt < releaseAt, 'backfill order: fact → terminate → release')
})

test('ORCHESTRATOR_PROGRAM_006: resume FailClosed is pure Return(Error)', () => {
  assert.equal(typeof orchestratorProgram.programs.resumeFailClosed, 'function')

  const program = orchestratorProgram.programs.resumeFailClosed(
    'GetTargetHead failed; ORCH-008 forbids falling back to HEAD',
  )
  const trace = listItems(orchestratorProgram.interpretWithDefaults(program))
  assert.deepEqual(trace, [
    'Return(Error GetTargetHead failed; ORCH-008 forbids falling back to HEAD)',
  ])
})

test('ORCHESTRATOR_PROGRAM_007: resume CleanUp is ReleaseWorktree → Return(None)', () => {
  assert.equal(typeof orchestratorProgram.programs.resumeCleanUp, 'function')

  const program = orchestratorProgram.programs.resumeCleanUp(JOB)
  const trace = listItems(orchestratorProgram.interpretWithDefaults(program))
  assert.deepEqual(trace, [
    `ReleaseWorktree(${'/tmp/wt-job-1'})`,
    'Return(None)',
  ])
})

test('ORCHESTRATOR_PROGRAM_008: resume AttemptPublish TargetMoved re-enters rebase loop', () => {
  assert.equal(typeof orchestratorProgram.programs.resumeAttemptPublish, 'function')

  let publishCount = 0
  const reply = (command) => {
    const name = orchestratorProgram.commandName(command)
    if (name === 'PublishUnderGate') {
      publishCount += 1
      if (publishCount === 1) return orchestratorProgram.reply.publishTargetMoved()
      return orchestratorProgram.reply.publishLanded(commitHash('after-retry'))
    }
    return orchestratorProgram.defaultReply(command)
  }

  const program = orchestratorProgram.programs.resumeAttemptPublish(JOB, {
    expectedHead: commitHash('expected-head'),
  })
  const trace = listItems(orchestratorProgram.interpretWith(reply, program))

  assert.equal(publishCount, 2)
  assert.ok(
    trace.some((step) => step.startsWith('PublishUnderGate(job-1,')),
    `publish missing; trace: ${trace.join(' → ')}`,
  )
  assert.ok(
    trace.some((step) => step.startsWith('ReviewRound(job-1, post-rebase, 0)')),
    `TargetMoved must re-enter rebase/review; trace: ${trace.join(' → ')}`,
  )
  assert.ok(
    trace.some((step) => step.startsWith('Return(Ok after-retry)')),
    `landed return missing; trace: ${trace.join(' → ')}`,
  )
})
