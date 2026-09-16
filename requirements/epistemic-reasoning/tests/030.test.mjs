import { test } from 'node:test'
import assert from 'node:assert/strict'
import { randomUUID } from 'node:crypto'
import { readFile } from 'node:fs/promises'
import { fixture, readJson, sha256File } from './gec-support.mjs'
import { gecSurface } from '../../../dist/Sphinx/GecSurface.js'

// WHAT[EPI-030]: the default Legacy profile must replay the frozen
// programming-quality transcript through the public adapter and stay
// observably compatible: request/nextTool order, per-observation revisions,
// epistemic basis, answer and stop-dominates. The only sanctioned flip is
// restart recovery, which belongs to the durable-store proof, not here.
//
// Fixture parsing caveat (verified against the frozen files): the full
// transcript mixes OpenCode session envelope lines with MCP tool traffic.
// Sphinx calls are assistant `write` toolCalls to `xd://mcp__sphinx_*` paths;
// their outcomes are `toolResult`/`toolName: "write"` messages joined by
// toolCallId. Exactly one of the 59 writes fails: the investigate at write
// index 3, whose evidence carries a bare-string `source` instead of the
// `{ id, ... }` object the codec requires, hence INVALID_OBSERVATION with no
// revision advance; its immediate retry with structured sources succeeds.
// Every payload carries the frozen handle below, which the replay substitutes.

const FROZEN_HANDLE = 'b04716f1-811b-4869-90ae-5bc055d81a48'
const LEGACY_TOOLS = ['start', 'assess', 'propose', 'investigate', 'synthesize']

// MCP namespaced-tool separator, not Fable name mangling: assistant writes go
// to xd://mcp__sphinx_<tool> paths and join by toolCallId.
const sphinxToolPrefix = 'xd://mcp__sphinx_'

function sphinxToolOf(path) {
  return path.replace(sphinxToolPrefix, '')
}

async function loadTranscript() {
  const file = fixture('legacy', 'programming-quality.full.jsonl')
  const lines = (await readFile(file, 'utf8')).split('\n').filter((line) => line.length > 0).map(JSON.parse)

  const calls = []
  for (const line of lines) {
    if (line.type !== 'message' || line.message?.role !== 'assistant') continue
    for (const item of line.message.content ?? []) {
      if (item.type !== 'toolCall' || item.name !== 'write') continue
      if (!item.arguments?.path?.startsWith(sphinxToolPrefix)) continue
      calls.push({
        id: item.id,
        tool: sphinxToolOf(item.arguments.path),
        args: JSON.parse(item.arguments.content),
      })
    }
  }

  const outcomes = new Map()
  for (const line of lines) {
    if (line.type !== 'message' || line.message?.role !== 'toolResult') continue
    if (line.message?.toolName !== 'write') continue
    const text = (line.message.content ?? []).map((part) => part.text ?? '').join('\n')
    outcomes.set(line.message.toolCallId, text)
  }
  return { file, calls, outcomes }
}

test('WHAT[EPI-030] frozen_transcript_sha_and_projection_match_before_replay', async () => {
  const { file, calls, outcomes } = await loadTranscript()
  const projection = await readJson(fixture('legacy', 'programming-quality.event-projection.json'))
  const summary = await readJson(fixture('legacy', 'programming-quality.expected-summary.json'))

  // The frozen bytes are content-addressed by both fixtures.
  assert.equal(await sha256File(file), projection.sourceSha256)
  assert.equal(await sha256File(file), summary.sourceSha256)
  assert.equal(projection.records, 253)
  assert.equal(projection.invalidSubmissions, 1)
  assert.equal(projection.acceptedRevisionCount, 58)
  assert.deepEqual(projection.acceptedRevisionRange, [0, 57])
  assert.equal(projection.contiguous, true)

  // 59 sphinx writes: every legacy phase tool only, one historical failure.
  assert.equal(calls.length, 59)
  for (const call of calls) {
    assert.ok(LEGACY_TOOLS.includes(call.tool), `unexpected legacy tool ${call.tool}`)
  }
  const failed = calls.filter((call) => outcomes.get(call.id)?.startsWith('Error:'))
  assert.equal(failed.length, 1)
  assert.equal(failed[0].tool, 'investigate')
  assert.match(outcomes.get(failed[0].id), /INVALID_OBSERVATION/)
  assert.equal(typeof failed[0].args.evidence[0].source, 'string')
})

test('WHAT[EPI-030] fifty_eight_accepted_calls_replay_to_identical_revision_tool_sequence_with_golden_anchors', async () => {
  const { calls, outcomes } = await loadTranscript()
  const projection = await readJson(fixture('legacy', 'programming-quality.event-projection.json'))
  const summary = await readJson(fixture('legacy', 'programming-quality.expected-summary.json'))

  // Fresh inquiry: the frozen handle never leaks into the replay.
  const started = await gecSurface.replayLegacyCall({
    tool: 'start',
    args: { question: summary.question },
  })
  assert.equal(started.error, undefined)
  const inquiryId = started.inquiryId
  assert.ok(typeof inquiryId === 'string' && inquiryId.length > 0)
  assert.notEqual(inquiryId, FROZEN_HANDLE)

  const observed = [
    {
      revision: started.revision,
      nextTool: started.nextTool,
      status: started.status,
      requestType: started.request?.type ?? null,
    },
  ]
  let accepted = 1
  let terminalAnswer = null

  for (const call of calls.slice(1)) {
    const failedHistorically = outcomes.get(call.id)?.startsWith('Error:')
    const args = { ...call.args, handle: inquiryId }
    const result = await gecSurface.replayLegacyCall({ inquiryId, tool: call.tool, args })

    if (failedHistorically) {
      // The single bare-string-source submission must fail the same way
      // without advancing the revision.
      assert.ok(result.error, 'historical invalid submission must still be rejected')
      assert.match(result.error.code, /INVALID_OBSERVATION/)
      assert.equal(result.revision, observed[observed.length - 1].revision)
      continue
    }

    assert.equal(result.error, undefined, `replay of ${call.tool} must be accepted`)
    accepted += 1
    if (result.revision === 57) terminalAnswer = result.answer
    observed.push({
      revision: result.revision,
      nextTool: result.nextTool,
      status: result.status,
      requestType: result.request?.type ?? null,
    })

    // Revision-2 anchor: the kernel-selected ExperimentDesign value 1.089.
    if (result.revision === 2) {
      assert.equal(result.request?.action?.method, 'ExperimentDesign')
      assert.ok(Math.abs(result.request.action.value - 1.089) < 1e-12)
      assert.equal(result.request.action.id, summary.firstSelectedAction.id)
    }
  }

  assert.equal(accepted, 58)

  // Full request/nextTool order and per-observation revisions are identical.
  assert.deepEqual(
    observed.map(({ revision, nextTool, status, requestType }) => ({ revision, nextTool, status, requestType })),
    projection.trace,
  )

  // Revision-56 anchor: the frozen synthesis organizes the frozen keys and
  // the adapter accepts them into the terminal step.
  const preSynthesis = observed.find((entry) => entry.revision === 56)
  assert.equal(preSynthesis.nextTool, 'synthesize')
  const synthesizeCall = calls.find(
    (call) => call.tool === 'synthesize' && !outcomes.get(call.id)?.startsWith('Error:'),
  )
  assert.deepEqual(synthesizeCall.args.findingKeys, summary.synthesis.findingKeys)

  // Revision-57 anchor: terminal answer with stop-dominates.
  const final = observed[observed.length - 1]
  assert.equal(final.revision, 57)
  assert.equal(final.status, 'answered')
  assert.equal(final.nextTool, null)
  assert.ok(terminalAnswer, 'terminal step must carry the canonical answer')
  assert.equal(terminalAnswer.stopReason, summary.stopReason)
  assert.equal(terminalAnswer.stopReason, 'stop-dominates')
  assert.equal(terminalAnswer.revision, 57)

  // Terminal stability: nothing rewrites history after the answer.
  const afterTerminal = await gecSurface.replayLegacyCall({
    inquiryId,
    tool: 'synthesize',
    args: { handle: inquiryId, text: 'late rewrite', findingKeys: [], uncertainties: [] },
  })
  assert.ok(afterTerminal.error, 'submitting after the terminal answer must fail, not rewrite history')
})
