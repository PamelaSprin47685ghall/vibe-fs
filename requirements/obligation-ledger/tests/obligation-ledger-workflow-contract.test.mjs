import assert from 'node:assert/strict'
import test from 'node:test'
import { createHash } from 'node:crypto'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as host from '../../../dist/Mission/Obligation/Todo/OpenCode/MagicTodoHostSurface.js'
import * as membrane from '../../../dist/Mission/Obligation/Todo/MagicTodoMembraneSurface.js'
import * as projectionSurface from '../../../dist/Mission/Obligation/Todo/MagicTodoProjectionSurface.js'

const sha256Hex = (value) => createHash('sha256').update(value).digest('hex')

test('WHAT[OBLIGATION-LEDGER-018] business sequencing prepares and accepts checkpoints through public membrane surface', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-ob-ledger-workflow-'))
  const boot = await journal.JournalSurface_boot(directory, 'rt_ob_workflow', 101, '2026-08-11T00:00:00Z')
  assert.equal(boot.ok, true)
  try {
    const handle = boot.journal
    const sessionId = 'ses-workflow-1'
    const incumbencyId = 'life-workflow-1'
    const callId = 'call-wf-1'
    const obligations = [{ name: 'task-1', horizon: 'near', work: 'Do work' }]

    const openRes = await membrane.MagicTodoMembraneSurface_openLife(handle, sessionId, incumbencyId)
    assert.equal(openRes.ok, true)

    const args = { planComplete: true, workingOn: 'task-1', obligations }
    const canonical = host.canonicalInput(args)
    const digest = host.canonicalInputDigest(sha256Hex, args)

    const prep = await membrane.MagicTodoMembraneSurface_prepare(
      handle, sessionId, callId, canonical, digest, true, obligations, 0,
    )
    assert.equal(prep.ok, true)

    const accepted = await membrane.MagicTodoMembraneSurface_accept(
      handle, prep.value.bridge, 'LiveAfterSuccess', digest, sha256Hex('output-wf'),
    )
    assert.equal(accepted.ok, true)
  } finally {
    journal.JournalSurface_dispose(boot.journal)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[OBLIGATION-LEDGER-018] hot-path queries use incremental projection facts on IncumbencyMagicTodoState', () => {
  const fact = (caseName, payload) => JSON.stringify({ case: caseName, ...payload })
  const prepared = fact('TodoWritePrepared', {
    ManagerSessionId: 'ses-1',
    IncumbencyId: 'test-life',
    TodoWriteId: 'tw-1',
    ToolCallId: 'call-1',
    ToolPartOrdinal: 1,
    BaseTodoRef: 'base-ref',
    BaseTodoDigest: 'base-digest',
    ProposedTodoRef: 'prop-ref',
    ProposedTodoDigest: 'prop-digest',
    PlanCompleteDeclared: true,
    ProviderInputDigest: 'in-digest',
    ReviewFrontier: { Sequence: 10 },
    SemanticVersion: 'magic-v1',
  })
  const accepted = fact('TodoWriteAccepted', {
    IncumbencyId: 'test-life',
    TodoWriteId: 'tw-1',
    ToolCallId: 'call-1',
    PreparedFactRef: 'evt-1',
    InputDigest: 'in-digest',
    OutputDigest: 'out-digest',
    PhysicalSuccessEvidence: 'LiveAfterSuccess',
    SemanticVersion: 'magic-v1',
  })
  const handle = projectionSurface.MagicTodoProjectionSurface_create()
  projectionSurface.MagicTodoProjectionSurface_fold(handle, 'evt-1', prepared)
  projectionSurface.MagicTodoProjectionSurface_fold(handle, 'evt-2', accepted)
  const view = projectionSurface.MagicTodoProjectionSurface_view(handle, 'test-life')
  assert.ok(view, 'view must exist for folded incumbency')
  assert.equal('firstAcceptedCheckpoint' in view, true)
  assert.equal('latestAcceptedCheckpoint' in view, true)
  assert.equal('firstPlanCommitment' in view, true)
  assert.equal('latestCommittedCheckpoint' in view, true)
  assert.equal('previousCommittedCheckpoint' in view, true)
  assert.equal('acceptedOrder' in view, false)
  assert.equal('acceptedIds' in view, false)
  assert.equal(view.firstAcceptedCheckpoint, 'tw-1')
  assert.equal(view.latestAcceptedCheckpoint, 'tw-1')
  assert.equal(view.firstPlanCommitment, 'tw-1')
  assert.equal(view.latestCommittedCheckpoint, 'tw-1')
})
