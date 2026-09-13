import assert from 'node:assert/strict'
import test from 'node:test'
import {
  prepareCheckpoint,
  acceptCheckpoint,
  PreparationAttempt$2,
} from '../../../dist/Mission/Obligation/LedgerWorkflow.js'
import { FSharpResult$2 } from '../../../dist/fable_modules/fable-library-js.5.13.0/Result.js'
import * as projection from '../../../dist/Composition/Durable/MagicTodoProjection.js'
import * as projectionSurface from '../../../dist/Mission/Obligation/Todo/MagicTodoProjectionSurface.js'
import * as relay from '../../../dist/Mission/Relay/Surface.js'

test('WHAT[OBLIGATION-LEDGER-018] business sequencing is a direct F# CE, executing preparation and acceptance without foreign state machines', async () => {
  // 1. prepareCheckpoint executes admission directly and returns typed Ok
  let prepareAttempted = false
  const prepOk = await prepareCheckpoint(() => {
    prepareAttempted = true
    return Promise.resolve(new PreparationAttempt$2(0, ['prepared-checkpoint-data']))
  })
  assert.equal(prepareAttempted, true)
  assert.equal(prepOk.tag, 0)
  assert.equal(prepOk.fields[0], 'prepared-checkpoint-data')

  // 2. prepareCheckpoint short-circuits on failure with typed AttemptFailed
  const prepFailed = await prepareCheckpoint(() => {
    return Promise.resolve(new PreparationAttempt$2(1, ['admission-denied']))
  })
  assert.equal(prepFailed.tag, 1)
  assert.equal(prepFailed.fields[0].fields[0], 'admission-denied')

  // 3. acceptCheckpoint executes durability effect and returns typed Ok
  let acceptAttempted = false
  const accOk = await acceptCheckpoint(() => {
    acceptAttempted = true
    return Promise.resolve(new FSharpResult$2(0, ['accepted-checkpoint-data']))
  })
  assert.equal(acceptAttempted, true)
  assert.equal(accOk.tag, 0)
  assert.equal(accOk.fields[0], 'accepted-checkpoint-data')

  // 4. acceptCheckpoint maps error into typed AcceptFailed
  const accFail = await acceptCheckpoint(() => {
    return Promise.resolve(new FSharpResult$2(1, ['append-failed']))
  })
  assert.equal(accFail.tag, 1)
  assert.equal(accFail.fields[0].fields[0], 'append-failed')
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
