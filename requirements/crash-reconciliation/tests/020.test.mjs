import assert from 'node:assert/strict'
import test from 'node:test'
import * as roles from '../../../dist/Foundation/RolesSurface.js'
import * as recovery from '../../../dist/OpenCode/Host/LoadRecoverySurface.js'
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

test('WHAT[crash-reconciliation-020] production load decision leaves active child runs active without voiding', () => {
  for (const agent of ['engineer', 'devops']) {
    const state = recovery.create()
    link(state, { agent })
    const child = acceptRun(state, { agent })
    assert.equal(recovery.childView(state, 'parent', 'child').activeRun, child.logicalRun)
    const settlements = recovery.childSettlements(state)
    assert.equal(settlements.length, 0, 'no void facts emitted on load')
    assert.deepEqual(recovery.childView(state, 'parent', 'child'), {
      activeRun: child.logicalRun, lifecycle: 'Active', joinable: 0, horizonVisible: 1,
    })
    assert.equal(recovery.lookupChild(state, 'parent', 'work', false).session, 'child')
  }
})

test('WHAT[crash-reconciliation-020] an active child work run remains visible in horizon projection and is not settled away', () => {
  for (const agent of ['engineer', 'devops']) {
    const state = recovery.create()
    link(state, { agent })
    acceptRun(state, { agent })
    assert.equal(recovery.childView(state, 'parent', 'child').horizonVisible, 1)
    const settlements = recovery.childSettlements(state)
    assert.equal(settlements.length, 0, 'not settled away')
    assert.equal(recovery.childView(state, 'parent', 'child').horizonVisible, 1, 'horizonVisible stays 1')
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
    const port = hostPort(async () => dispatch.admittedWithReceipt('load-child-receipt'))
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
    // Reopen the actual writer before settlement; this does not simulate plugin activation or an OS crash.
    await body({ handle: await reopen(), directory, reopen, dispatch, profiles })
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

// Owner law since 60812f575 ("restart keeps unfinished child work visible"):
// load settlement no longer voids an orphaned child run. The unit laws above
// pin the pure decision; these two pin the actual journal: the orphaned runs
// keep their exact authority across reopen, and settlement publishes no
// durable fact for them (neither a void nor a completion).
integrationTest('WHAT[crash-reconciliation-020] actual child settlement keeps orphaned runs active across journal reopen without a void or a completion', async () => {
  await withDurableChildRuns(async ({ handle, directory, reopen, dispatch, profiles }) => {
    const before = await durableEvents(directory)
    for (const child of ['engineer', 'devops']) {
      assert.deepEqual(dispatch.projectionObservation(handle, child).activeLogicalRun, profiles.get(child))
    }
    await recovery.settleChildRuns(handle)
    for (const child of ['engineer', 'devops']) {
      assert.deepEqual(dispatch.projectionObservation(handle, child).activeLogicalRun, profiles.get(child),
        `${child} keeps its exact active run after settlement`)
    }
    const added = (await durableEvents(directory))
      .filter(event => !before.some(prior => prior.event_id === event.event_id))
      .map(event => event.payload.Fact)
    const childFacts = added.filter(fact => fact[0] === 'Agent' && fact[1]?.[0] === 'Execution')
    assert.deepEqual(childFacts, [], 'settlement appends no ChildWorkVoided, ChildRunVoided or completion fact')
    const reopened = await reopen()
    for (const child of ['engineer', 'devops']) {
      assert.deepEqual(dispatch.projectionObservation(reopened, child).activeLogicalRun, profiles.get(child),
        `${child} keeps its exact active run across reopen`)
    }
    const settled = await durableEvents(directory)
    await recovery.settleChildRuns(reopened)
    assert.deepEqual(await durableEvents(directory), settled, 'repeated settlement produces no durable fact')
  })
})

integrationTest('WHAT[crash-reconciliation-020] actual child settlement never attempts a child-run append, so a broken writer cannot close or void either run', async () => {
  await withDurableChildRuns(async ({ handle, directory, reopen, dispatch, profiles }) => {
    const { renameSync, mkdirSync, readdirSync, rmSync } = await import('node:fs')
    const { join } = await import('node:path')
    const events = join(directory, '.Herdchestra', 'wanxiangshu', 'events')
    const saved = join(directory, '.Herdchestra', 'wanxiangshu', 'saved-events')
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
    // Same fault as before the law change: any physical append would now land
    // on a directory and report an unknown outcome. Settlement must not reach it.
    for (const writer of stashed) renameSync(writer.live, writer.held)
    for (const writer of stashed) mkdirSync(writer.live)
    try {
      await recovery.settleChildRuns(handle)
      assert.deepEqual(['engineer', 'devops'].map(child => active(handle, child)), initial, 'settlement cannot close either run')
    } finally {
      for (const writer of stashed) rmSync(writer.live, { recursive: true, force: true })
      for (const writer of stashed) renameSync(writer.held, writer.live)
      rmSync(saved, { recursive: true, force: true })
    }
    assert.deepEqual(await durableEvents(directory), before, 'settlement publishes no durable fact')
    // The writer was never touched, so it is not poisoned: a real append still lands.
    const probe = await dispatch.acceptHumanRootSelection(handle, 'probe-manager', 'root-probe-manager', managerRootSelection)
    assert.equal(probe.ok, true, `writer must remain healthy after settlement: ${JSON.stringify(probe.error)}`)
    assert.ok((await durableEvents(directory)).length > before.length, 'the healthy writer publishes the probe fact')
    const reopened = await reopen()
    assert.deepEqual(['engineer', 'devops'].map(child => active(reopened, child)), initial)
    await recovery.settleChildRuns(reopened)
    assert.deepEqual(['engineer', 'devops'].map(child => active(reopened, child)), initial)
  })
})

test.todo('WHAT[crash-reconciliation-020] actual plugin activation durably settles orphan child work before ordinary execution, preserves exact live flights, and refuses append failure (GAP-149)')
test.todo('WHAT[crash-reconciliation-020] a real interrupted run and PTY input are not replayed after restart while unique DevOps authority and durable model Persona binding are retained (GAP-149; GAP-129; GAP-132)')
