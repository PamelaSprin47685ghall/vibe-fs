import assert from 'node:assert/strict'
import test from 'node:test'
import * as roles from '../../../dist/Foundation/RolesSurface.js'
import * as recovery from '../../../dist/OpenCode/Host/LoadRecoverySurface.js'
import * as physicalRecovery from '../../../dist/OpenCode/Host/PhysicalRecoverySurface.js'
import { fold, link, acceptRun, terminal, canonical } from './support/load-projection.mjs'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'
import { hostPort } from '../../interaction-authority/tests/support/authority.mjs'

test('WHAT[crash-reconciliation-020] current role vocabulary includes DevOps and Engineer but excludes legacy Coder', () => {
  assert.ok(roles.allRoleLabels.includes('devops'))
  assert.ok(roles.allRoleLabels.includes('engineer'))
  assert.equal(roles.allRoleLabels.includes('coder'), false)
})

test('WHAT[crash-reconciliation-020] in-process physical terminals preserve an active child logical run and handle', () => {
  for (const disposition of ['Cancelled', 'Failed', 'Completed']) {
    const state = recovery.create()
    link(state)
    const child = acceptRun(state)
    const before = recovery.childView(state, 'parent', 'child')
    const ended = terminal(disposition, child)
    const payload = JSON.parse(ended)[1][1][1]
    const started = payload.Evidence[1]
    fold(state, canonical('ChatExecution', 'Accepted', {
      SchemaVersion: 1, Key: payload.Key, Evidence: started.Accepted,
    }))
    fold(state, canonical('ChatExecution', 'ProviderStarted', {
      SchemaVersion: 1, Key: payload.Key, Evidence: started,
    }))
    fold(state, ended)
    assert.deepEqual(recovery.childView(state, 'parent', 'child'), before)
    assert.equal(before.activeRun, child.logicalRun)
    assert.equal(before.lifecycle, 'Active')
    assert.equal(before.joinable, 0)
  }
})

test('WHAT[crash-reconciliation-020] production load voids exact scoped work and clears its legacy active projection', () => {
  // An orphaned child run can no longer be executed by a fresh process, so
  // load settlement voids its exact admitted work. Physical acceptance and
  // the inherited child root establish that work through the production fold.
  for (const agent of ['engineer', 'devops']) {
    const state = recovery.create()
    link(state, { agent })
    const child = acceptRun(state, { agent })
    const before = recovery.childView(state, 'parent', 'child')
    assert.equal(before.activeRun, child.logicalRun)
    const settlements = recovery.childSettlements(state)
    assert.equal(settlements.length, 1, 'load voids the orphaned child run exactly once')
    const fact = JSON.parse(settlements[0])
    assert.equal(fact[0], 'Agent')
    assert.equal(fact[1][0], 'Execution')
    assert.equal(fact[1][1][0], 'ChildWorkVoided', 'the accepted child root admits the exact orphaned work')
    const payload = fact[1][1][1]
    assert.deepEqual(payload, {
      ParentSessionId: ['SessionId', 'parent'],
      Work: {
        Handle: ['Agent', ['AgentHandleId', 'work']],
        ChildSessionId: ['SessionId', 'child'],
        AuthorityRoot: ['AuthorityRootUserMessageId', child.authorityRoot],
      },
    })
    assert.equal(recovery.lookupChild(state, 'parent', 'work', false).session, 'child')
    assert.deepEqual(recovery.childView(state, 'parent', 'child'), before,
      'selecting the settlement does not apply a durable void')
    fold(state, settlements[0])
    // The work is Abandoned and its legacy active projection is cleared.
    // This does not prove typed AuthorityLogicalRunClosed or its source producer (GAP-123).
    assert.deepEqual(recovery.childView(state, 'parent', 'child'), {
      activeRun: '', lifecycle: 'Abandoned', joinable: 0, horizonVisible: 1,
    })
    assert.deepEqual(recovery.childSettlements(state), [], 'a folded void is not selected again')
  }
})

test('WHAT[crash-reconciliation-020] a voided scoped child stays horizon-visible after legacy projection cleanup', () => {
  for (const agent of ['engineer', 'devops']) {
    const state = recovery.create()
    link(state, { agent })
    const child = acceptRun(state, { agent })
    assert.equal(recovery.childView(state, 'parent', 'child').horizonVisible, 1)
    const settlements = recovery.childSettlements(state)
    assert.equal(settlements.length, 1, 'the void is published, not silently skipped')
    const fact = JSON.parse(settlements[0])
    assert.equal(fact[1][1][0], 'ChildWorkVoided')
    const payload = fact[1][1][1]
    assert.deepEqual(payload, {
      ParentSessionId: ['SessionId', 'parent'],
      Work: {
        Handle: ['Agent', ['AgentHandleId', 'work']],
        ChildSessionId: ['SessionId', 'child'],
        AuthorityRoot: ['AuthorityRootUserMessageId', child.authorityRoot],
      },
    })
    fold(state, settlements[0])
    assert.equal(recovery.childView(state, 'parent', 'child').activeRun, '')
    assert.equal(recovery.childView(state, 'parent', 'child').lifecycle, 'Abandoned')
    assert.equal(recovery.childView(state, 'parent', 'child').horizonVisible, 1, 'horizonVisible stays 1')
    assert.deepEqual(recovery.childSettlements(state), [], 'the visible void is settled exactly once')
  }
})


test('WHAT[crash-reconciliation-020] human roots and child runs without a durable handle are not selected for load settlement', () => {
  const human = recovery.create()
  const manager = acceptRun(human, { child: 'parent', agent: 'manager', human: true })
  assert.deepEqual(recovery.childSettlements(human), [])
  assert.equal(recovery.childView(human, 'parent', 'parent').activeRun, manager.logicalRun)
  const unlinked = recovery.create()
  const child = acceptRun(unlinked)
  assert.deepEqual(recovery.childSettlements(unlinked), [])
  assert.equal(recovery.childView(unlinked, 'parent', 'child').activeRun, child.logicalRun)
})

const managerRootSelection = {
  kind: 'RootSelection', ownerSession: null, ownerLogicalRun: null, ownerAuthorityRoot: null,
  participantIdentity: {
    participant: 'manager', role: 'manager', persona: 'Lead', personaCatalogVersion: 1, origin: 'ResolvedAtRoot',
  },
}

const withDurableChildRuns = async (body) => {
  const { mkdtempSync, mkdirSync, rmSync } = await import('node:fs')
  const { tmpdir } = await import('node:os')
  const { join } = await import('node:path')
  const journal = await import('../../../dist/Persistence/Journal/Surface.js')
  const dispatch = await import('../../../dist/Interaction/Dispatch/DispatchSurface.js')
  const authority = await import('../../../dist/Interaction/Authority/RuntimeSurface.js')
  const directory = mkdtempSync(join(tmpdir(), 'wxs-child-load-settle-'))
  const stateRoot = join(directory, '.Herdchestra')
  let handle
  let incarnation = 0
  const reopen = async () => {
    if (handle !== undefined) journal.JournalSurface_dispose(handle)
    handle = undefined
    incarnation += 1
    const opened = await journal.JournalSurface_bootWithWriterId(
      stateRoot, `load-writer-${incarnation}`, `load-runtime-${incarnation}`, process.pid, new Date().toISOString(),
    )
    assert.equal(opened.ok, true, JSON.stringify(opened.error))
    handle = opened.journal
    return handle
  }
  try {
    await reopen()
    const parent = await dispatch.acceptHumanRootSelection(handle, 'parent', 'root-parent', managerRootSelection)
    assert.equal(parent.ok, true, JSON.stringify(parent.error))
    const profiles = new Map()
    let sends = 0
    const port = hostPort(async () => {
      sends += 1
      return dispatch.admittedWithReceipt('load-child-receipt')
    })
    for (const agent of ['engineer', 'devops']) {
      const linked = await journal.JournalSurface_appendAgent(handle, { kind: 'Session', session: 'parent' }, null, {
        family: 'Execution', case: 'HandleLinked', payload: {
          ParentSessionId: 'parent', ChildSessionId: agent, Handle: `work-${agent}`,
          TargetAgent: agent, Byname: `work-${agent}`, CanonicalRole: agent, Ownership: 'DurableParentHandle',
        },
      })
      assert.equal(linked.ok, true, JSON.stringify(linked.error))
      const seed = authority.issueInheritedIdentitySeed(agent, parent.profile)
      assert.equal(seed.ok, true, seed.error)
      const sent = await dispatch.sendAgentOwnerRootAwait(port, handle, agent, `WORK-${agent}`, seed.value)
      assert.equal(sent.ok, true, sent.error)
      const accepted = await dispatch.acceptAgentOwnerRoot(handle, agent, sent.key, `physical-${agent}`)
      assert.equal(accepted.ok, true, JSON.stringify(accepted.error))
      profiles.set(agent, accepted.profile)
    }
    assert.equal(sends, profiles.size, 'each child has exactly one original physical dispatch')
    // Reopen the actual writer before settlement; this does not simulate plugin activation or an OS crash.
    await body({ handle: await reopen(), directory, commonDir: stateRoot, reopen, dispatch, profiles, sends: () => sends })
  } finally {
    if (handle !== undefined) journal.JournalSurface_dispose(handle)
    rmSync(directory, { recursive: true, force: true })
  }
}

const durableEvents = async (directory) => {
  const { readdirSync, readFileSync } = await import('node:fs')
  const { join } = await import('node:path')
  const events = join(directory, '.Herdchestra', 'wanxiangshu', 'events')
  return readdirSync(events).filter(name => name.endsWith('.ndjson')).sort().flatMap(name => {
    const content = readFileSync(join(events, name), 'utf8')
    assert.ok(content === '' || content.endsWith('\n'), 'every durable event must be a complete line')
    return content.split('\n').filter(Boolean).map(line => {
      const event = JSON.parse(line)
      assert.equal(typeof event.event_id, 'string')
      assert.notEqual(event.event_id, '')
      return event
    })
  })
}

// The settled contract: an orphaned child work can no longer be executed by a
// fresh process, so load settlement voids it. A void is not a completion, but
// it settles the parent's outstanding obligation: the work becomes
// Abandoned(Voided), the child no longer holds an active logical run, and a
// later settlement appends nothing. This observes legacy projection cleanup;
// it does not prove typed AuthorityLogicalRunClosed/source production (GAP-123).
integrationTest('WHAT[crash-reconciliation-020] actual child settlement voids the orphaned scoped child work exactly once and never re-dispatches', async () => {
  await withDurableChildRuns(async ({ handle, directory, commonDir, reopen, dispatch, profiles, sends }) => {
    const before = await durableEvents(directory)
    for (const child of ['engineer', 'devops']) {
      assert.deepEqual(dispatch.projectionObservation(handle, child).activeLogicalRun, profiles.get(child),
        `${child} holds a real active run before settlement`)
    }
    await physicalRecovery.settleChildRuns(handle, commonDir)
    for (const child of ['engineer', 'devops']) {
      assert.ok(!dispatch.projectionObservation(handle, child).activeLogicalRun,
        `${child} no longer holds an active logical run after the void`)
    }
    const added = (await durableEvents(directory))
      .filter(event => !before.some(prior => prior.event_id === event.event_id))
      .map(event => event.payload.Fact)
    const childFacts = added.filter(fact => fact[0] === 'Agent' && fact[1]?.[0] === 'Execution')
    assert.equal(childFacts.length, 2, 'settlement voids exactly one scoped child work per orphan')
    for (const fact of childFacts) {
      assert.equal(fact[1][1][0], 'ChildWorkVoided', 'a scoped child work is voided as ChildWorkVoided')
    }
    const payloads = childFacts.map(fact => fact[1][1][1])
    for (const child of ['engineer', 'devops']) {
      assert.deepEqual(payloads.filter(payload => payload.Work.ChildSessionId[1] === child), [{
        ParentSessionId: ['SessionId', 'parent'],
        Work: {
          Handle: ['Agent', ['AgentHandleId', `work-${child}`]],
          ChildSessionId: ['SessionId', child],
          AuthorityRoot: ['AuthorityRootUserMessageId', profiles.get(child).authorityRoot],
        },
      }], 'the void names exactly one actual orphaned work with its accepted root')
    }
    assert.equal(added.filter(fact => JSON.stringify(fact).includes('HandleCompleted')).length, 0,
      'a void is not a completion: no completion fact is written')
    assert.equal(sends(), profiles.size, 'a void never re-dispatches the child work')
    const reopened = await reopen()
    for (const child of ['engineer', 'devops']) {
      assert.ok(!dispatch.projectionObservation(reopened, child).activeLogicalRun,
        `${child} stays voided across reopen`)
    }
    const settled = await durableEvents(directory)
    await physicalRecovery.settleChildRuns(reopened, commonDir)
    assert.deepEqual(await durableEvents(directory), settled, 'repeated settlement produces no durable fact')
    assert.equal(sends(), profiles.size, 'repeated settlement never re-dispatches')
  })
})

integrationTest('WHAT[crash-reconciliation-020] a broken writer makes child settlement fail loudly and the runs stay open until a healthy writer can void them', async () => {
  await withDurableChildRuns(async ({ handle, directory, commonDir, reopen, dispatch, profiles, sends }) => {
    const { renameSync, mkdirSync, readdirSync, rmSync } = await import('node:fs')
    const { join } = await import('node:path')
    const events = join(directory, '.Herdchestra', 'wanxiangshu', 'events')
    const saved = join(directory, '.Herdchestra', 'wanxiangshu', 'saved-events')
    // Activate the writer so the fault reaches the actual void append.
    const admitted = await dispatch.acceptHumanRootSelection(handle, 'current-manager', 'root-current-manager', managerRootSelection)
    assert.equal(admitted.ok, true, JSON.stringify(admitted.error))
    const before = await durableEvents(directory)
    const active = (journal, child) => dispatch.projectionObservation(journal, child).activeLogicalRun
    const initial = ['engineer', 'devops'].map(child => active(handle, child))
    assert.deepEqual(initial, ['engineer', 'devops'].map(child => profiles.get(child)), 'real unsettled child work exists')
    mkdirSync(saved, { recursive: true })
    const writers = readdirSync(events).filter(name => name.endsWith('.ndjson'))
    assert.ok(writers.length > 0, 'the fault must replace a real writer file')
    const stashed = writers.map(name => ({ live: join(events, name), held: join(saved, name) }))
    // Any physical append now lands on a directory and reports an unknown
    // outcome: settlement must fail loudly instead of pretending it settled.
    for (const writer of stashed) renameSync(writer.live, writer.held)
    for (const writer of stashed) mkdirSync(writer.live)
    try {
      await assert.rejects(() => physicalRecovery.settleChildRuns(handle, commonDir), /append outcome unknown/i)
      assert.deepEqual(['engineer', 'devops'].map(child => active(handle, child)), initial,
        'a failed settlement closes neither run')
    } finally {
      for (const writer of stashed) rmSync(writer.live, { recursive: true, force: true })
      for (const writer of stashed) renameSync(writer.held, writer.live)
      rmSync(saved, { recursive: true, force: true })
    }
    assert.deepEqual(await durableEvents(directory), before, 'the refused append publishes no durable fact')
    const poisoned = await dispatch.acceptHumanRootSelection(handle, 'refused-manager', 'root-refused-manager', managerRootSelection)
    assert.equal(poisoned.ok, false)
    assert.match(JSON.stringify(poisoned.error), /append not attempted.*writer poisoned/i)
    await assert.rejects(() => physicalRecovery.settleChildRuns(handle, commonDir), /append not attempted.*writer poisoned/i)
    assert.deepEqual(['engineer', 'devops'].map(child => active(handle, child)), initial)
    assert.deepEqual(await durableEvents(directory), before, 'the poisoned writer must not retry physical append')
    const reopened = await reopen()
    assert.deepEqual(['engineer', 'devops'].map(child => dispatch.projectionObservation(reopened, child).activeLogicalRun), initial)
    await physicalRecovery.settleChildRuns(reopened, commonDir)
    for (const child of ['engineer', 'devops']) {
      assert.ok(!active(reopened, child), `${child} is voided once a healthy writer can append the void`)
    }
    assert.equal(sends(), profiles.size, 'settlement never re-dispatches the child work')
  })
})

test.todo('WHAT[crash-reconciliation-020] actual plugin activation durably settles orphan child work before ordinary execution, preserves exact live flights, and refuses append failure (GAP-149)')
test.todo('WHAT[crash-reconciliation-020] a real interrupted run and PTY input are not replayed after restart while unique DevOps authority and durable model Persona binding are retained (GAP-149; GAP-129; GAP-132)')
