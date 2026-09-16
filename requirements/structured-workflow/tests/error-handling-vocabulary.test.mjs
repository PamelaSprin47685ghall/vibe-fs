import assert from 'node:assert/strict'
import test from 'node:test'
import { TaskResultListSurface_traverseM } from '../../../dist/Foundation/FsToolkitFableCompat.js'
import * as outcomeSurface from '../../../dist/Foundation/OutcomeSurface.js'

test('WHAT[STRUCTURED-WORKFLOW-003] OutcomeSurface defines public vocabulary for outcome classification', () => {
  assert.ok(outcomeSurface.sendOutcomeKinds().includes('AdmittedWithReceipt'))
  assert.ok(outcomeSurface.isValidAgentRunResult('terminal text'))
  assert.equal(outcomeSurface.isValidAgentRunResult('   '), false)
})

test('WHAT[STRUCTURED-WORKFLOW-004] Fable async Result plumbing provides sequential short-circuiting traversal', async () => {
  const traversedOk = await TaskResultListSurface_traverseM((x) => Promise.resolve(x > 0), [1, 2, 3])
  assert.deepEqual(traversedOk, ['Ok', 1, 2, 3])

  const traversedErr = await TaskResultListSurface_traverseM((x) => Promise.resolve(x !== 2), [1, 2, 3])
  assert.deepEqual(traversedErr, ['Error', 2])
})
