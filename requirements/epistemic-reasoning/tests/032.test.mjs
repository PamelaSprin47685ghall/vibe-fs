import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as office from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'
import * as sync from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'
import * as sessions from '../../../dist/OpenCode/Host/SessionsSurface.js'

test('WHAT[epistemic-reasoning-032] Sphinx uses the standard Engineer capability profile', () => {
  for (const permission of ['Read', 'Write', 'Edit', 'Fission', 'Sphinx']) {
    assert.equal(office.isAllowed('engineer', permission), true, permission)
  }
  assert.equal(office.isAllowed('engineer', 'Exec'), false)
  assert.equal(office.isAllowed('sphinx', 'Fission'), false)
  assert.equal(office.managerForkableOffices().includes('sphinx'), false)
})

test('WHAT[epistemic-reasoning-032] managed Engineer creation uses family root rather than the caller subsession', async t => {
  const directory = await mkdtemp(join(tmpdir(), 'sphinx-flat-engineer-'))
  t.after(() => rm(directory, { recursive: true, force: true }))
  const result = await sync.managedChildReconciliationScenario(directory, 'missing')
  assert.equal(result.error, '')
  assert.equal(result.createAgent, 'engineer')
  assert.equal(result.createCount, 1)
  assert.equal(result.createParent, 'managed-child-reconciliation', 'the delegate preserves its logical caller for inherited authority')
  assert.deepEqual(result.listedFamilies, ['host-family-root'])
  const physical = await sessions.flattenedChildAdapterProbe()
  assert.notEqual(physical.caller, physical.worker)
  assert.deepEqual(physical.physicalParents, [physical.root, physical.root])
  assert.equal(physical.workerFamily, physical.root)
})

test('WHAT[epistemic-reasoning-032] response is the exact accepted final output, not the WorkRecord or previous invocation', async t => {
  const directory = await mkdtemp(join(tmpdir(), 'sphinx-engineer-response-'))
  const owner = 'sphinx-owner'
  const runtime = await sync.create(directory, [{ sessionId: owner, agent: 'manager' }])
  t.after(async () => { sync.dispose(runtime); await rm(directory, { recursive: true, force: true }) })
  for (const [index, text] of ['OLD-ANSWER', '{"type":"Candidates","items":[]}'].entries()) {
    const pending = sync.invokeResponse(runtime, owner, `Charge ${index}`)
    await sync.awaitPromptCount(runtime, owner, 'Engineer', index + 1)
    assert.equal(sync.acceptPrompt(runtime, owner, 'Engineer', index), true)
    assert.equal(await sync.settle(runtime, owner, 'Engineer', text, `response-run-${index}`), true)
    const result = await pending
    assert.equal(result.ok, true)
    assert.equal(result.value, text)
  }
  assert.equal(sync.childCount(runtime), 1, 'ordinary reusable Engineer lifecycle remains in use')
})
