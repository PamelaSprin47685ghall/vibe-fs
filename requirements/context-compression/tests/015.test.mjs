import assert from 'node:assert/strict'
import test from 'node:test'
import * as blogEntry from '../../../dist/Context/Companion/BlogEntryCommittedSurface.js'
import * as blogProjection from '../../../dist/Context/Companion/BlogProjectionSurface.js'

test('WHAT[CONTEXT-COMPRESSION-015] busy_or_failed_does_not_advance_coverage', () => {
  assert.equal(blogEntry.advanceCoverageOnFailure, false)
  assert.equal(blogEntry.advanceCoverageWhenBusy, false)
})

test('WHAT[CONTEXT-COMPRESSION-015] COMPANION_008_entry_appends_frame_and_advances_coverage_together', () => {
  assert.ok(blogProjection.appendsFrameAndAdvancesCoverageTogether)
})

test('WHAT[CONTEXT-COMPRESSION-015] COMPANION_008_empty_or_xml_only_entry_does_not_advance_coverage', () => {
  assert.equal(blogProjection.advancesOnXmlOnly, false)
})

test('WHAT[CONTEXT-COMPRESSION-015] COMPANION_008_successful_blog_observation_advances_coverage_atomically', () => {
  assert.ok(blogProjection.advancesAtomically)
})
