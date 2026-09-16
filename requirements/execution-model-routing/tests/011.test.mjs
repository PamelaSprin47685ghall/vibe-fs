import assert from 'node:assert/strict'
import test from 'node:test'

import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'

const {
  createRuntime,
  acquireExecutionAdmission,
  releasePhysicalExecutionWith,
} = routing

const target = (model = 'provider/shared', reasoning = 'none') => ({ model, reasoning })

test('WHAT[EMR-011] rejects release with the wrong physical fence', async () => {
  const runtime = createRuntime(() => target())
  const acquisition = await acquireExecutionAdmission(
    runtime,
    'session-1',
    'physical-1',
    'coder',
    'alice',
    null,
  )
  assert.equal(acquisition.kind, 'Acquired')

  const wrong = releasePhysicalExecutionWith(
    runtime,
    'session-1',
    'physical-other',
    acquisition.lease,
  )
  assert.deepEqual(wrong, { kind: 'Conflict' })
})
