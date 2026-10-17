import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import * as journal from '../../../../dist/Persistence/Journal/Surface.js'
import * as record from '../../../../dist/Mission/WorkRecord/Surface.js'
export { withReopenableJournal } from '../../../semantic-trace/tests/support/journal.mjs'
export { record }

export function readBoundedInNewProcess(directory, session, range) {
  return JSON.parse(execFileSync('node', [
    fileURLToPath(new URL('./cold-record.mjs', import.meta.url)), directory, session, JSON.stringify(range),
  ], { encoding: 'utf8', env: { ...process.env, WXS_E2E_QUIET: '1' } }))
}

export async function commitFrame(handle, session, { from, through, body, id, textDigest }) {
  const written = await journal.JournalSurface_writePayload(handle, body)
  assert.equal(written.ok, true)
  const result = await record.appendBlogObservation(handle, session, `run-${id}`, {
    bloggerSessionId: `blog-${session}`, requestId: `request-${id}`, frameEpoch: 0,
    previousIngestedThroughSequence: from, nextIngestedThroughSequence: through,
    previousCoverableTurnCutoffExclusive: 0, nextCoverableTurnCutoffExclusive: 0,
    nextCoveredPrefixDigest: '', textRef: written.blobRef, textDigest: textDigest ?? written.blobDigest,
    toolCallIds: [], tipRuleId: `tip-${id}`, fieldNameAtCommit: `field-${id}`, observedPrefixEpoch: 0,
  })
  assert.equal(result.ok, true, result.error)
  return written
}
