import assert from 'node:assert/strict'
import * as journal from '../../../../dist/Persistence/Journal/Surface.js'
import * as record from '../../../../dist/Mission/WorkRecord/Surface.js'
export { withReopenableJournal } from '../../../semantic-trace/tests/support/journal.mjs'
export { record }

export async function commitFrame(handle, session, { from, through, body, id }) {
  const written = await journal.JournalSurface_writePayload(handle, body)
  assert.equal(written.ok, true)
  const result = await record.appendBlogObservation(handle, session, `run-${id}`, {
    bloggerSessionId: `blog-${session}`, requestId: `request-${id}`, frameEpoch: 0,
    previousIngestedThroughSequence: from, nextIngestedThroughSequence: through,
    previousCoverableTurnCutoffExclusive: 0, nextCoverableTurnCutoffExclusive: 0,
    nextCoveredPrefixDigest: '', textRef: written.blobRef, textDigest: written.blobDigest,
    toolCallIds: [], tipName: `tip-${id}`, observedPrefixEpoch: 0,
  })
  assert.equal(result.ok, true, result.error)
  return written
}

// Split a rendered LWR into its canonical sections. A section runs from its
// heading line to the next heading, so a clause can assert which section owns a
// byte sequence instead of only that the record contains it somewhere.
export function sectionsOf(rendered) {
  const headings = [...rendered.matchAll(/^(Opening|Chronicle|Recent work)\n/gm)]
  const sections = {}
  headings.forEach((heading, index) => {
    const start = heading.index + heading[0].length
    const next = headings[index + 1]
    const body = next ? rendered.slice(start, next.index) : rendered.slice(start)
    sections[heading[1]] = body.replace(/\n+$/, '')
  })
  return sections
}
