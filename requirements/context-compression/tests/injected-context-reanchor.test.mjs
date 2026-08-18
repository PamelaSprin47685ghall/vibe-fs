import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import * as pair from '../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js'
import * as grounding from '../../../dist/OpenCode/Host/RequirementGroundingSurface.js'

const terminalRead = (path) => [{
  info: { id: 'r1', role: 'assistant', providerID: 'anthropic' },
  parts: [{ type: 'tool', tool: 'read', callID: 'source-read', state: { status: 'completed', input: { filePath: path }, output: 'source\n', time: { start: 0, end: 0 } } }],
}]

const cursorRead = (path) => [{
  info: { id: 'r1', role: 'assistant', providerID: 'cursor' },
  parts: [{ type: 'tool', tool: 'read', callID: 'source-read', state: { status: 'completed', input: { filePath: path }, output: 'source\n', time: { start: 0, end: 0 } } }],
}]

const suffixCount = (text) => text.split(grounding.cursorSeparator).length - 1

const pairCallIds = (messages) => messages
  .filter((message) => pair.isPairProgrammingThought(message))
  .map((message) => message.parts[0].callID)

const groundingMessages = (messages) => messages.filter((message) => message.info?.source === grounding.source)

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wanxiang-injection-reanchor-'))
  mkdirSync(join(dir, 'requirements', 'alpha', 'tests'), { recursive: true })
  mkdirSync(join(dir, 'src'), { recursive: true })
  writeFileSync(join(dir, 'requirements', 'alpha', 'WHAT.md'), 'what\n', 'utf8')
  writeFileSync(join(dir, 'requirements', 'alpha', 'APPLIES-TO'), '/src/**\n', 'utf8')
  writeFileSync(join(dir, 'src', 'main.fs'), 'source\n', 'utf8')
  return { dir, source: join(dir, 'src', 'main.fs'), cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

test('WHAT[CONTEXT-COMPRESSION-019] CTX_019_reanchor_retires_old_pair_wire_but_keeps_history_and_allows_a_fresh_pair', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wanxiang-pair-reanchor-'))
  const opened = await pair.createJournal(dir)
  assert.equal(opened.ok, true)
  try {
    const session = 'ctx-019-pair'
    const raw = [{ info: { id: 'u1', role: 'user', providerID: 'anthropic' }, parts: [{ type: 'text', text: 'hello' }] }]
    const first = await pair.tryInjectWithJournal(opened.journal, session, pair.text, raw)
    assert.equal(first.ok, true)
    const firstIds = pairCallIds(first.value)
    assert.equal(firstIds.length, 1)
    assert.equal(pair.pairCount(opened.journal, session), 1)

    const reanchored = await pair.appendContextReanchored(opened.journal, session, 0n, 1n, 'compaction-1')
    assert.equal(reanchored.ok, true, reanchored.error)

    const next = await pair.tryInjectWithJournal(opened.journal, session, pair.text, first.value)
    assert.equal(next.ok, true, next.error)
    const nextIds = pairCallIds(next.value)
    assert.equal(nextIds.length, 1, 'new Y horizon must contain only its fresh pair')
    assert.notEqual(nextIds[0], firstIds[0], 'fresh horizon pair needs a fresh call identity')
    assert.equal(pair.pairCount(opened.journal, session), 2, 'durable audit history keeps both occurrences')
  } finally {
    pair.disposeJournal(opened.journal)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[CONTEXT-COMPRESSION-019] CTX_019_reanchor_retires_old_requirement_reads_then_same_digest_regrounds_on_the_next_real_trigger', async () => {
  const { dir, source, cleanup } = sandbox()
  const opened = await grounding.createJournal(dir)
  assert.equal(opened.ok, true)
  try {
    const session = 'ctx-019-grounding'
    const requested = await grounding.requestPaths(opened.journal, dir, session, [source])
    assert.equal(requested.needsGrounding, true)
    const first = await grounding.projectWithJournal(opened.journal, session, terminalRead(source))
    assert.equal(first.ok, true)
    const firstReads = groundingMessages(first.value)
    assert.ok(firstReads.length > 0)
    const firstCallIds = firstReads.map((message) => message.parts[0].callID)

    const reanchored = await grounding.appendContextReanchored(opened.journal, session, 0n, 1n, 'compaction-1')
    assert.equal(reanchored.ok, true, reanchored.error)
    assert.deepEqual(grounding.groundedIdentities(opened.journal, session), [])

    const afterReplacement = await grounding.projectWithJournal(opened.journal, session, first.value)
    assert.equal(afterReplacement.ok, true)
    assert.equal(groundingMessages(afterReplacement.value).length, 0, 'old grounding reads must not survive Y')

    const rerun = await grounding.requestPaths(opened.journal, dir, session, [source])
    assert.equal(rerun.needsGrounding, true)
    assert.equal(rerun.requested, 1)
    const second = await grounding.projectWithJournal(opened.journal, session, afterReplacement.value)
    const secondReads = groundingMessages(second.value)
    assert.ok(secondReads.length > 0)
    assert.notDeepEqual(
      secondReads.map((message) => message.parts[0].callID),
      firstCallIds,
      'same digest in a new horizon must use fresh read call identities',
    )
  } finally {
    grounding.disposeJournal(opened.journal)
    cleanup()
  }
})

test('WHAT[CONTEXT-COMPRESSION-019] CTX_019_cursor_reanchor_strips_old_pair_suffix_before_adding_the_new_horizon_pair', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wanxiang-cursor-pair-reanchor-'))
  const opened = await pair.createJournal(dir)
  assert.equal(opened.ok, true)
  try {
    const session = 'ctx-019-cursor-pair'
    const raw = [{
      info: { id: 'r1', role: 'assistant', providerID: 'cursor' },
      parts: [{ type: 'tool', tool: 'read', callID: 'source', state: { status: 'completed', input: {}, output: 'source\n', time: { start: 0, end: 0 } } }],
    }]
    const first = await pair.tryInjectWithJournal(opened.journal, session, pair.text, raw)
    assert.equal(first.ok, true)
    assert.equal(suffixCount(first.value[0].parts[0].state.output), 1)

    const reanchored = await pair.appendContextReanchored(opened.journal, session, 0n, 1n, 'compaction-1')
    assert.equal(reanchored.ok, true, reanchored.error)
    const second = await pair.tryInjectWithJournal(opened.journal, session, pair.text, first.value)
    assert.equal(second.ok, true, second.error)
    assert.equal(
      suffixCount(second.value[0].parts[0].state.output),
      1,
      'old Cursor pair suffix must be removed before the fresh horizon suffix is appended',
    )
    assert.equal(pair.pairCount(opened.journal, session), 2)
  } finally {
    pair.disposeJournal(opened.journal)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[CONTEXT-COMPRESSION-019] CTX_019_cursor_reanchor_strips_old_requirement_suffixes_until_a_real_path_trigger_regrounds', async () => {
  const { dir, source, cleanup } = sandbox()
  const opened = await grounding.createJournal(dir)
  assert.equal(opened.ok, true)
  try {
    const session = 'ctx-019-cursor-grounding'
    await grounding.requestPaths(opened.journal, dir, session, [source])
    const first = await grounding.projectWithJournal(opened.journal, session, cursorRead(source))
    assert.equal(first.ok, true)
    const firstOutput = first.value[0].parts[0].state.output
    assert.ok(suffixCount(firstOutput) > 0)

    const reanchored = await grounding.appendContextReanchored(opened.journal, session, 0n, 1n, 'compaction-1')
    assert.equal(reanchored.ok, true, reanchored.error)
    const afterReplacement = await grounding.projectWithJournal(opened.journal, session, first.value)
    assert.equal(afterReplacement.ok, true)
    assert.equal(
      afterReplacement.value[0].parts[0].state.output,
      'source\n',
      'Y horizon must not retain any old requirement Cursor suffix',
    )

    const rerun = await grounding.requestPaths(opened.journal, dir, session, [source])
    assert.equal(rerun.needsGrounding, true)
    const second = await grounding.projectWithJournal(opened.journal, session, afterReplacement.value)
    assert.equal(suffixCount(second.value[0].parts[0].state.output), suffixCount(firstOutput))
  } finally {
    grounding.disposeJournal(opened.journal)
    cleanup()
  }
})
