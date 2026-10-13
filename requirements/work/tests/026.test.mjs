import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'
import * as surface from '../../../dist/Mission/Planning/Surface.js'
import * as workspace from '../../../dist/OpenCode/Host/WorkspaceEventStoreSurface.js'

const life = lifecycle.LifecycleSurface ?? lifecycle

const withWorkspace = async (body) => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-planning-008-'))
  const commonDir = join(root, '.git')
  mkdirSync(commonDir, { recursive: true })
  try {
    await body(workspace.acquire(commonDir), root)
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
}

const workOf = (store, workId) => store.TryCurrent('Lifecycle').get(workId)

const batchesOf = (store) => {
  const batches = []
  const append = store.Append.bind(store)
  store.Append = async (envelopes) => {
    const batch = []
    for (const envelope of envelopes) batch.push(envelope)
    batches.push(batch)
    return append(envelopes)
  }
  return batches
}

// S1 Promoted -> relay -> S2 Promoted -> relay -> S3 Promoted is the only
// ladder the unified fold authorises. Review and relay both go through the kernel.
const authorize = async (store, root, workId) => {
  const opened = await life.seedOpen(store, 'plan', workId, root, 0n)
  assert.equal(opened.ok, true, opened.error)
  for (let step = 0; step < 2; step += 1) {
    const before = workOf(store, workId)
    const reviewed = await life.seedReview(store, workId, 'Promoted')
    assert.equal(reviewed.ok, true, `promotion ${step} failed: ${reviewed.error}`)
    const relayed = await surface.PlanningSurface.executeDeliver(
      store, root, workId, before.Active.Id, `${workId}-run-${step}`, `${workId}-call-${step}`, false,
      async () => { throw new Error('an unauthorised climb must not spawn') },
      BigInt((step + 1) * 10),
    )
    assert.equal(relayed.ok, true, `climb relay ${step} failed: ${relayed.error}`)
    assert.equal(relayed.relayed, true)
  }
  const finalReview = await life.seedReview(store, workId, 'Promoted')
  assert.equal(finalReview.ok, true, finalReview.error)
  const authorized = workOf(store, workId)
  assert.equal(String(authorized.Active.OpeningStage), 'S3')
  assert.equal(String(authorized.Active.Review.Outcome), 'Promoted')
  assert.equal(String(authorized.Active.Review.Stage), 'S3')
  return authorized
}

test('WHAT[work-026] authorized deliver records PlanDelivered with execSessionId after spawning a manager session', async () => {
  await withWorkspace(async (store, root) => {
    const workId = 'work-d1'
    const content = '# Final Delivered Plan\n1. Step A\n2. Step B'
    const written = surface.PlanningSurface.rewritePlanAtomic(root, workId, content)
    assert.equal(written.ok, true)

    const active = await authorize(store, root, workId)
    const batches = batchesOf(store)
    let spawnAgent = null
    let spawnPrompt = null
    const res = await surface.PlanningSurface.executeDeliver(
      store, root, workId, active.Active.Id, 'run-d1', 'call-d1', false,
      async (agent, prompt) => { spawnAgent = agent; spawnPrompt = prompt; return 'exec-session-d1' },
      30n,
    )

    assert.equal(res.ok, true, res.error)
    assert.equal(res.relayed, false)
    assert.equal(res.delivered, true)
    assert.equal(res.retirementOutcome, 'Delivered')
    assert.equal(res.digest, written.digest)
    assert.equal(res.path, written.path)
    assert.ok(res.path.endsWith('plan.md'))
    assert.equal(res.execSessionId, 'exec-session-d1')
    assert.equal(spawnAgent, 'manager')
    assert.equal(spawnPrompt, content)

    assert.equal(batches.length, 1, 'authorised delivery is one append')
    assert.deepEqual(batches[0].map((envelope) => envelope.EventType), ['IncumbencyRetired'])
    assert.equal(batches[0][0].Payload.outcome, 'Delivered')
    assert.equal(batches[0][0].Payload.execSessionId, 'exec-session-d1')
    assert.equal(batches[0][0].Payload.digest, written.digest)
    assert.equal(batches[0][0].Payload.path, written.path)
    assert.equal(batches[0].some((envelope) => envelope.EventType === 'ConfirmationCommitted'), false)
    assert.equal(batches[0].some((envelope) => envelope.EventType === 'IncumbencyOpened'), false)

    const after = workOf(store, workId)
    assert.equal(after.Active, undefined)
    assert.equal(after.CurrentDelivered, true)
    assert.equal(String(after.DeliveredRecord.Outcome), 'Delivered')
    assert.equal(after.DeliveredRecord.ExecSessionId, 'exec-session-d1')
    assert.equal(after.DeliveredRecord.Digest, written.digest)
    assert.equal(after.DeliveredRecord.Path, written.path)
    assert.equal(after.DeliveredRecord.ProjectionCut ?? null, null, 'one-shot delivery has no projection cut')
  })
})

test('WHAT[work-026] authorized deliver is rejected when the plan artifact is empty or the spawn fails', async () => {
  await withWorkspace(async (store, root) => {
    const missing = await authorize(store, root, 'work-missing')
    const missingBatches = batchesOf(store)
    const resMissing = await surface.PlanningSurface.executeDeliver(
      store, root, 'work-missing', missing.Active.Id, 'run-missing', 'call-missing', false,
      async () => 'exec-never',
      30n,
    )
    assert.equal(resMissing.ok, false)
    assert.match(resMissing.error, /empty or missing|not found/i)
    assert.equal(missingBatches.length, 0)
    assert.equal(workOf(store, 'work-missing').Active.Id, missing.Active.Id)

    const workId = 'work-d2'
    const written = surface.PlanningSurface.rewritePlanAtomic(root, workId, '# Plan content')
    assert.equal(written.ok, true)
    const active = await authorize(store, root, workId)
    const batches = batchesOf(store)
    const resSpawnFail = await surface.PlanningSurface.executeDeliver(
      store, root, workId, active.Active.Id, 'run-d2', 'call-d2', false,
      async () => { throw new Error('session spawn failed') },
      30n,
    )
    assert.equal(resSpawnFail.ok, false)
    assert.match(resSpawnFail.error, /spawn failed/i)
    assert.equal(batches.length, 0, 'a failed spawn writes zero events')
    const stayed = workOf(store, workId)
    assert.equal(stayed.Active.Id, active.Active.Id)
    assert.equal(stayed.CurrentDelivered, false)
  })
})

test('WHAT[work-026] authorized deliver without retirement evidence fails closed with no spawn', async () => {
  await withWorkspace(async (store, root) => {
    const workId = 'work-d3'
    const written = surface.PlanningSurface.rewritePlanAtomic(root, workId, '# Plan content')
    assert.equal(written.ok, true)
    const active = await authorize(store, root, workId)
    const batches = batchesOf(store)
    let spawnCalls = 0
    const res = await surface.PlanningSurface.executeDeliver(
      store, root, workId, active.Active.Id, 'run-d3', 'call-d3', false,
      async () => { spawnCalls += 1; return 'exec-never' },
      undefined,
    )
    assert.equal(res.ok, false)
    assert.match(res.error, /retirement cursor evidence is required/i)
    assert.equal(spawnCalls, 0)
    assert.equal(batches.length, 0)
    assert.equal(workOf(store, workId).Active.Id, active.Active.Id)
    assert.equal(workOf(store, workId).CurrentDelivered, false)
  })
})
