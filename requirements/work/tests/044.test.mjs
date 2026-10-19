import assert from 'node:assert/strict'
import test from 'node:test'
import { deliverOnce, relayHandover } from './support/contract.mjs'
import * as scopeMod from '../../../dist/OpenCode/Tools/ToolRuntimeScopeSurface.js'
import { withAdmittedChildren, withSharedAdmittedChildren } from '../../managed-session-lifecycle/tests/support/admitted-child-work.mjs'
import { join } from 'node:path'
import * as ownershipHost from '../../../dist/Mission/Lifecycle/Surface.js'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as journalPort from '../../../dist/Persistence/Journal/Surface.js'
import { acceptOwner, hostPort } from '../../interaction-authority/tests/support/authority.mjs'

const evaluateBlockers =
  scopeMod.evaluateRetirementBlockers ||
  (scopeMod.ToolRuntimeScopeSurface && scopeMod.ToolRuntimeScopeSurface.evaluateRetirementBlockers)

const coldBlockers = scopeMod.coldRetirementBlockers ?? scopeMod.ToolRuntimeScopeSurface?.coldRetirementBlockers

test('WHAT[work-044] Relayed keeps the fixed DevOps and its received work for the successor', () => {
  const relayed = deliverOnce({ sequence: ['S1', 'S2', 'S3'], blockers: [], deliveryAuthorized: false, head: 7, projection: 'S1', incumbencyId: 'i' })
  const handover = relayHandover(relayed, { devopsId: 'd', received: ['job-1'] })
  assert.equal(handover.ok, true)
  assert.equal(handover.orphaned, false)
  assert.deepEqual(handover.kept, ['job-1'])
  assert.equal(handover.written.some((event) => event.type === 'DevOpsBound'), false)
})

test('WHAT[work-044] RetirementBlockersFor excludes fixed DevOps agent and PTYs while blocking on active Engineer work', async () => {
  assert.equal(typeof evaluateBlockers, 'function', 'evaluateRetirementBlockers must be exported')

  await withAdmittedChildren(
    'work-044-devops-handover',
    'manager-road-044',
    [
      { agentId: 'devops', sessionId: 'devops-child-044', role: 'devops' },
      { agentId: 'engineer-1', sessionId: 'engineer-child-044', role: 'engineer' },
    ],
    async (journal) => {
      // Only fixed DevOps active: 0 blockers for Manager tenure relay
      const onlyDevOps = await evaluateBlockers(journal, {
        managerSessionId: 'manager-road-044',
        devopsChildSessionId: 'devops-child-044',
        engineerChildSessionId: 'engineer-child-044',
        devopsPtys: ['devops-bg-pty'],
        engineerPtys: [],
        managerHasDevopsAgent: true,
        managerHasEngineerAgent: false,
      })
      assert.deepEqual(
        onlyDevOps,
        [],
        'fixed DevOps run and its PTYs survive across Manager relay and must not block retirement',
      )

      // Engineer active with PTY: blocks Manager retirement
      const withEngineer = await evaluateBlockers(journal, {
        managerSessionId: 'manager-road-044',
        devopsChildSessionId: 'devops-child-044',
        engineerChildSessionId: 'engineer-child-044',
        devopsPtys: ['devops-bg-pty'],
        engineerPtys: ['eng-active-pty'],
        managerHasDevopsAgent: true,
        managerHasEngineerAgent: true,
      })
      assert.equal(
        withEngineer.some((b) => b.includes('engineer-child-044')),
        true,
        'active Engineer child run must block Manager retirement',
      )
      assert.equal(
        withEngineer.some((b) => b.startsWith('fork:engineer-child-044:pty:')),
        true,
        'active Engineer PTY lease must block Manager retirement',
      )
      assert.equal(
        withEngineer.some((b) => b.includes('devops')),
        false,
        'fixed DevOps must still be excluded when Engineer blockers are present',
      )
    },
  )
})

test('WHAT[work-044] cold retirement reads durable child parent edges and retains Engineer PTYs while excluding fixed DevOps', async () => {
  await withSharedAdmittedChildren('work-044-cold-ownership', 'cold-manager', [
    { agentId: 'devops', sessionId: 'cold-devops', role: 'devops' },
    { agentId: 'engineer', sessionId: 'cold-engineer', role: 'engineer' },
  ], async (journal, _profiles, directory) => {
    for (const child of ['cold-devops', 'cold-engineer']) {
      const registry = `fork-pty:${child}`
      ownershipHost.executeOwnershipDurable(join(directory, '.Herdchestra'), registry, `open:${child}`, {
        type: 'OpenScope', scopeId: child, parentId: null,
      })
      ownershipHost.executeOwnershipDurable(join(directory, '.Herdchestra'), registry, `reserve:${child}`, {
        type: 'Reserve', resourceId: `pty:${child}`, scopeId: child, kind: 'process',
      })
    }
    assert.deepEqual(await coldBlockers(journal, directory, 'cold-manager'), [
      'ownership:cold-engineer:process:pty:cold-engineer',
    ])
  })
})

test('WHAT[work-044] cold ownership matches the child session when two parents use the same handle name', async () => {
  await withSharedAdmittedChildren('work-044-distinct-parents', 'a-manager', [
    { agentId: 'engineer-1', sessionId: 'a-child', role: 'engineer' },
  ], async (journal, _profiles, directory) => {
    const owner = await acceptOwner(journal, 'z-manager')
    const linked = await journalPort.JournalSurface_appendAgent(journal, { kind: 'Session', session: 'z-manager' }, null, {
      family: 'Execution', case: 'HandleLinked', payload: {
        ParentSessionId: 'z-manager', ChildSessionId: 'z-child', Handle: 'engineer-1',
        TargetAgent: 'engineer', Byname: 'engineer-1', CanonicalRole: 'engineer', Ownership: 'DurableParentHandle',
      },
    })
    assert.equal(linked.ok, true, JSON.stringify(linked.error))
    const seed = authority.issueInheritedIdentitySeed('engineer', owner)
    assert.equal(seed.ok, true, seed.error)
    const port = hostPort(async () => dispatch.admittedWithReceipt('z-child-work'))
    const sent = await dispatch.sendAgentOwnerRootAwait(port, journal, 'z-child', 'WORK-engineer-1', seed.value)
    assert.equal(sent.ok, true, sent.error)
    const accepted = await dispatch.acceptAgentOwnerRoot(journal, 'z-child', sent.key, 'physical-z-child')
    assert.equal(accepted.ok, true, JSON.stringify(accepted.error))
    ownershipHost.executeOwnershipDurable(join(directory, '.Herdchestra'), 'fork-pty:z-child', 'open:z-child', {
      type: 'OpenScope', scopeId: 'z-child', parentId: null,
    })
    ownershipHost.executeOwnershipDurable(join(directory, '.Herdchestra'), 'fork-pty:z-child', 'reserve:z-child', {
      type: 'Reserve', resourceId: 'pty:z-child', scopeId: 'z-child', kind: 'process',
    })
    assert.deepEqual(await coldBlockers(journal, directory, 'a-manager'), [])
    assert.deepEqual(await coldBlockers(journal, directory, 'z-manager'), [
      'ownership:z-child:process:pty:z-child',
    ])
  })
})
