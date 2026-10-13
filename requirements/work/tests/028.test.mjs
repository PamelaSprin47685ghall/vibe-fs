import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'
import * as surface from '../../../dist/Mission/Planning/Surface.js'
import * as workspace from '../../../dist/OpenCode/Host/WorkspaceEventStoreSurface.js'

const life = lifecycle.LifecycleSurface ?? lifecycle

const withStore = async (body) => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-planning-028-'))
  const commonDir = join(root, '.git')
  mkdirSync(commonDir, { recursive: true })
  try {
    await body(workspace.acquire(commonDir), root)
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
}

const workOf = (store, workId) => store.TryCurrent('Lifecycle').get(workId)

test('WHAT[work-028] ask delegates the intent to the bound DevOps and returns its work record', async () => {
  await withStore(async (store) => {
    const opened = await life.seedOpen(store, 'plan', 'work-ask-1', '/root', 0n)
    assert.equal(opened.ok, true, opened.error)

    const batches = []
    const append = store.Append.bind(store)
    store.Append = async (envelopes) => {
      const batch = []
      for (const envelope of envelopes) batch.push(envelope.EventType)
      batches.push(batch)
      return append(envelopes)
    }

    let delegatedIntent = null
    const delegate = async (intent) => {
      delegatedIntent = intent
      return '# DevOps work record\nInvestigated build artifact paths; they match expectations.'
    }

    const res = await surface.PlanningSurface.executeAsk(store, 'work-ask-1', 'Check whether build artifact paths match expectation', delegate)

    assert.equal(res.ok, true, `ask failed: ${res.error}`)
    assert.notEqual(res.kind, 'waiting_for_user', 'ask must not return a waiting-for-user suspension')
    assert.equal(delegatedIntent, 'Check whether build artifact paths match expectation')
    assert.equal(
      typeof res.workRecord === 'string' && res.workRecord.includes('DevOps work record'),
      true,
      'ask must return the DevOps work record verbatim (no model summarization)',
    )

    // An unbound work gets its DevOps binding persisted on first ask.
    assert.deepEqual(batches, [['DevOpsBound']])
    const bound = workOf(store, 'work-ask-1').DevOps
    assert.equal(bound?.[0]?.startsWith('devops-'), true, 'ask must bind the fixed DevOps when the work is unbound')

    // Empty intent is rejected
    const resEmpty = await surface.PlanningSurface.executeAsk(store, 'work-ask-1', '   ', delegate)
    assert.equal(resEmpty.ok, false)
    assert.match(resEmpty.error, /cannot be empty/i)
  })
})

test('WHAT[work-028] a second ask is rejected while one delegation is pending', async () => {
  await withStore(async (store) => {
    const opened = await life.seedOpen(store, 'plan', 'work-ask-2', '/root', 0n)
    assert.equal(opened.ok, true, opened.error)

    // The first delegation never resolves; it stays pending.
    const pendingDelegate = () => new Promise(() => {})

    surface.PlanningSurface.executeAsk(store, 'work-ask-2', 'First investigation', pendingDelegate)
    const second = await surface.PlanningSurface.executeAsk(store, 'work-ask-2', 'Second investigation', pendingDelegate)

    assert.equal(second.ok, false, 'a second ask must be rejected while a delegation is pending')
    assert.match(second.error, /pending|already/i)
  })
})

test('WHAT[work-028] ask is rejected after delivery until the work is reopened', async () => {
  await withStore(async (store, root) => {
    const opened = await life.seedOpen(store, 'plan', 'work-ask-3', root, 0n)
    assert.equal(opened.ok, true, opened.error)
    for (let step = 0; step < 2; step += 1) {
      const before = workOf(store, 'work-ask-3')
      const reviewed = await life.seedReview(store, 'work-ask-3', 'Promoted')
      assert.equal(reviewed.ok, true, reviewed.error)
      const relayed = await surface.PlanningSurface.executeDeliver(
        store, root, 'work-ask-3', before.Active.Id, `run-${step}`, `call-${step}`, false,
        async () => { throw new Error('spawn must not run while climbing an unauthorised tenure') },
        BigInt((step + 1) * 10),
      )
      assert.equal(relayed.ok, true, relayed.error)
    }
    const active = workOf(store, 'work-ask-3')
    const promoted = await life.seedReview(store, 'work-ask-3', 'Promoted')
    assert.equal(promoted.ok, true, promoted.error)
    const planPath = surface.PlanningSurface.resolvePlanPath(root, 'work-ask-3')
    assert.equal(planPath.ok, true, planPath.error)
    const written = surface.PlanningSurface.rewritePlanAtomic(root, 'work-ask-3', '# delivered plan\n')
    assert.equal(written.ok, true, written.error)
    const delivered = await surface.PlanningSurface.executeDeliver(
      store, root, 'work-ask-3', active.Active.Id, 'run-d', 'call-d', false,
      async () => 'exec-1',
      30n,
    )
    assert.equal(delivered.ok, true, delivered.error)
    assert.equal(delivered.delivered, true)

    const res = await surface.PlanningSurface.executeAsk(store, 'work-ask-3', 'Any investigation?', async () => 'unused')
    assert.equal(res.ok, false)
    assert.match(res.error, /already been delivered/i)
  })
})
