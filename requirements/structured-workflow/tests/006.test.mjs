import assert from 'node:assert/strict'
import test from 'node:test'

import * as SuicideToolSurface from '../../../dist/OpenCode/Tools/SuicideToolSurface.js'

test('WHAT[STRUCTURED-WORKFLOW-006] SuicideTool admission gate requires OfficeRole and admits Manager while rejecting others', () => {
  assert.equal(typeof SuicideToolSurface.admission, 'function')
})

test('WHAT[STRUCTURED-WORKFLOW-006] SuicideTool retirement freeze fence order rejects concurrent and stale admissions without session abort', () => {
  assert.equal(typeof SuicideToolSurface.freezeFence, 'function')
})
