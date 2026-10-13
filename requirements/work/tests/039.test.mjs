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
  const root = mkdtempSync(join(tmpdir(), 'wxs-planning-023-'))
  mkdirSync(join(root, '.git'), { recursive: true })
  try {
    await body(workspace.acquire(join(root, '.git')), root)
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
}

const workOf = (store, workId) => store.TryCurrent('Lifecycle').get(workId)

const authorize = async (store, root, workId) => {
  const opened = await life.seedOpen(store, 'plan', workId, root, 0n)
  assert.equal(opened.ok, true, opened.error)
  for (let step = 0; step < 2; step += 1) {
    const before = workOf(store, workId)
    const reviewed = await life.seedReview(store, workId, 'Promoted')
    assert.equal(reviewed.ok, true, reviewed.error)
    const relayed = await surface.PlanningSurface.executeDeliver(
      store, root, workId, before.Active.Id, `${workId}-run-${step}`, `${workId}-call-${step}`, false,
      async () => { throw new Error('an unauthorised climb must not spawn') },
      BigInt((step + 1) * 10),
    )
    assert.equal(relayed.ok, true, relayed.error)
  }
  const finalReview = await life.seedReview(store, workId, 'Promoted')
  assert.equal(finalReview.ok, true, finalReview.error)
  return workOf(store, workId)
}

test('WHAT[work-039] the spawned execution session receives the plan artifact byte-for-byte and its id is recorded verbatim', async () => {
  await withWorkspace(async (store, root) => {
    const workId = 'work-23a'
    const content = '# Plan with tricky bytes\n\n- line with\ttab\n- 中文段落 🚀\n- trailing newline\n'
    const written = surface.PlanningSurface.rewritePlanAtomic(root, workId, content)
    assert.equal(written.ok, true)

    const active = await authorize(store, root, workId)
    const batches = []
    const append = store.Append.bind(store)
    store.Append = async (envelopes) => {
      const batch = []
      for (const envelope of envelopes) batch.push(envelope)
      batches.push(batch)
      return append(envelopes)
    }
    let spawnAgent = null
    let spawnPrompt = null
    const res = await surface.PlanningSurface.executeDeliver(
      store, root, workId, active.Active.Id, 'run-23a', 'call-23a', false,
      async (agent, prompt) => { spawnAgent = agent; spawnPrompt = prompt; return 'exec-verbatim-42' },
      30n,
    )

    assert.equal(res.ok, true, res.error)
    assert.equal(spawnAgent, 'manager')
    assert.equal(spawnPrompt, content)
    assert.equal(res.execSessionId, 'exec-verbatim-42')
    assert.equal(res.digest, written.digest)
    assert.equal(res.path, written.path)

    assert.equal(batches.length, 1)
    assert.deepEqual(batches[0].map((envelope) => envelope.EventType), ['IncumbencyRetired'])
    assert.equal(batches[0][0].Payload.outcome, 'Delivered')
    assert.equal(batches[0][0].Payload.execSessionId, 'exec-verbatim-42')
    assert.equal(batches[0][0].Payload.digest, written.digest)
    assert.equal(batches[0][0].Payload.path, written.path)
    assert.equal(batches[0].some((envelope) => envelope.EventType === 'ConfirmationCommitted'), false)

    const after = workOf(store, workId)
    assert.equal(after.DeliveredRecord.ExecSessionId, 'exec-verbatim-42')
    assert.equal(after.DeliveredRecord.Digest, written.digest)
    assert.equal(after.DeliveredRecord.Path, written.path)
    assert.equal(after.Active, undefined)
  })
})

test('WHAT[work-039] a failed spawn writes zero events and keeps the tenure active', async () => {
  await withWorkspace(async (store, root) => {
    const workId = 'work-23b'
    const written = surface.PlanningSurface.rewritePlanAtomic(root, workId, '# Plan content')
    assert.equal(written.ok, true)
    const active = await authorize(store, root, workId)
    const batches = []
    const append = store.Append.bind(store)
    store.Append = async (envelopes) => {
      const batch = []
      for (const envelope of envelopes) batch.push(envelope)
      batches.push(batch)
      return append(envelopes)
    }
    const res = await surface.PlanningSurface.executeDeliver(
      store, root, workId, active.Active.Id, 'run-23b', 'call-23b', false,
      async () => { throw new Error('session spawn failed') },
      30n,
    )
    assert.equal(res.ok, false)
    assert.match(res.error, /spawn failed/i)
    assert.equal(batches.length, 0)
    const stayed = workOf(store, workId)
    assert.equal(stayed.Active.Id, active.Active.Id)
    assert.equal(stayed.CurrentDelivered, false)
    assert.equal(stayed.DeliveredRecord ?? null, null)
  })
})
