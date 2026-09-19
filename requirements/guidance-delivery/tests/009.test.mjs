import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as guidance from '../../../dist/Enforcer/Guidance/TipSurface.js'
import * as pair from '../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js'
import * as resources from '../../../dist/Resources/PromptSurface.js'

resources.runtimeInstallFromPackage()

const latestTipNudge = guidance.latestNudge

const latestTipGuidance = guidance.latest

const tryInject = pair.tryInject

const main = 'ses-nudge-main'

const blogger = 'ses-nudge-blogger'

const appendObservation = (journal) =>
  guidance.appendObservation(journal, {
    session: main,
    bloggerSession: blogger,
    requestId: 'req-nudge-1',
    frameEpoch: 0,
    previousIngestedThrough: 0,
    nextIngestedThrough: 1,
    previousCutoff: 0,
    nextCutoff: 1,
    nextCoveredPrefixDigest: 'digest-nudge-1',
    textRef: 'blob-nudge-1',
    textDigest: 'sha-nudge-1',
    providerRun: 'run-nudge-1',
    toolCallIds: ['call-nudge-1'],
    tipRuleId: 'primitive-obsession',
    fieldNameAtCommit: 'primitive-obsession',
    observedPrefixEpoch: 0,
  })

const seed = async ({ withAssociation = true, withTip = true } = {}) => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-latest-tip-'))
  const created = await guidance.createJournal(directory)
  assert.equal(created.ok, true, created.error)
  const journal = created.journal

  if (withAssociation) {
    const linked = await guidance.appendCompanionLink(journal, {
      session: main,
      bloggerSession: blogger,
      bloggerAgent: 'blogger',
    })
    assert.equal(linked.ok, true, linked.error)
  }

  if (withTip) {
    const committed = await appendObservation(journal)
    assert.equal(committed.ok, true, committed.error)
  }

  return {
    journal,
    dispose: () => {
      guidance.disposeJournal(journal)
      rmSync(directory, { recursive: true, force: true })
    },
  }
}

const guideline = '# Pair programming auto-injected'

const SEP = '\0\uFEFF'

const anchor = [
  { info: { id: 'c1', role: 'assistant' }, parts: [{ type: 'tool', tool: 'read', callID: 'source', state: { status: 'pending', input: {}, time: { start: 0 } } }] },
  { info: { id: 'r1', role: 'assistant' }, parts: [{ type: 'tool', tool: 'read', callID: 'source', state: { status: 'completed', input: {}, output: 'source\n', time: { start: 0, end: 0 } } }] },
]

const markerOutput = (messages) => {
  // universal cursor mode: terminal real tool result carries output + SEP + marker
  const result = messages.at(-1)
  const output = result?.parts?.[0]?.state?.output
  if (typeof output !== 'string') return undefined
  const idx = output.indexOf(SEP)
  return idx >= 0 ? output.slice(idx + SEP.length) : undefined
}

test('WHAT[guidance-delivery-009] CTX_002_GUIDELINE_001_marker_without_nudge_is_guideline_text', async () => {
  const marker = pair.text
  const result = await tryInject('ses-gd-001', marker, anchor)
  assert.equal(result.ok, true, result.error)
  assert.equal(markerOutput(result.value), marker)
  assert.match(marker, /^# /)
})

test('WHAT[guidance-delivery-009] CTX_002_GUIDELINE_002_marker_with_nudge_is_one_instruction_plane', async () => {
  const nudge = 'A domain concept is crossing a boundary as a primitive. Introduce a distinct type so invalid substitutions become impossible.'
  const marker = `# ${nudge}\n${pair.text}`
  const result = await tryInject('ses-gd-002', marker, anchor)
  assert.equal(result.ok, true, result.error)
  assert.equal(markerOutput(result.value), marker)
  assert.ok(marker.indexOf('# A domain concept') < marker.indexOf(pair.text.trim()))
})
