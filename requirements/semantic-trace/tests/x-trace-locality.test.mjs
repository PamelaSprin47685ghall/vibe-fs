import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as xTrace from '../../../dist/Context/Trace/XTraceSurface.js'

const managerSession = 'ses_xtrace_locality'

let sequence = 0
const nextEnvelope = (factValue, run = 'asst_manager_run') =>
  xTrace.envelope({ seq: (sequence += 1), session: managerSession, run, fact: factValue })

const tracePartEnvelope = ({
  sequence: cursorSequence,
  run = 'asst_manager_run',
  role = 'assistant',
  turn = 4,
  partIndex = 2,
  kind = 'tool_call',
  toolName = 'todowrite',
  textRef = 'blobs/todo-call',
  textDigest = 'digest:todo-call',
  provenance = `g:0/turn:${turn}/part:${partIndex}`,
  toolCallId = 'call_todo',
  hostToolPartId = 'part_todo',
} = {}) =>
  nextEnvelope(
    xTrace.fact('XTracePartAppended', {
      sessionId: managerSession,
      sequence: cursorSequence,
      role,
      turn,
      partIndex,
      kind,
      toolName,
      textRef,
      textDigest,
      provenance,
      providerRun: run,
      toolCallId,
      hostToolPartId,
    }),
    run,
  )

const assertLocality = (localized) => {
  assert.equal(localized.ok, true, localized.ok ? '' : JSON.stringify(localized.error))
  return localized.value
}

test('WHAT[SEMANTIC-TRACE-002] TODO-004 preserves a captured tool call identity on its durable XTrace range', () => {
  const folded = xTrace.fold([tracePartEnvelope({ sequence: 7 })])
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  const session = xTrace.session(folded.value, managerSession)
  const parts = xTrace.parts(session.xTrace)
  assert.equal(parts.length, 1)
  const part = parts[0]
  assert.equal(part.providerRun, 'asst_manager_run')
  assert.equal(part.toolCallId, 'call_todo')
  assert.equal(part.hostToolPartId, 'part_todo')
  assert.equal(part.cursor.sequence, 7)
})

test('WHAT[SEMANTIC-TRACE-002] TODO-004 captures the SDK-visible assistant run and Host ToolPart without index inference', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'xtrace-locality-'))
  const created = await journal.JournalSurface_boot(dir, 'rt_xtrace_locality', 4242, '2026-01-01T00:00:00Z')
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))

  try {
    const trace = await xTrace.captureMessageView(created.journal, managerSession, [
      {
        info: { id: 'asst_manager_run', role: 'assistant' },
        parts: [
          { type: 'text', text: 'I will update the plan.' },
          {
            id: 'part_todo',
            type: 'tool',
            tool: 'todowrite',
            callID: 'call_todo',
            state: { status: 'pending', input: { todos: [] } },
          },
        ],
      },
    ])
    const parts = xTrace.parts(trace)
    assert.equal(parts.length, 2)

    const todoPart = parts[1]
    assert.equal(todoPart.providerRun, 'asst_manager_run')
    assert.equal(todoPart.toolCallId, 'call_todo')
    assert.equal(todoPart.hostToolPartId, 'part_todo')
    assert.equal(todoPart.cursor.sequence, 2)
  } finally {
    journal.JournalSurface_dispose(created.journal)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[SEMANTIC-TRACE-002] stable snapshot capture keys one physical Host part independently of later semantic index drift', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'xtrace-stable-part-id-'))
  const created = await journal.JournalSurface_boot(dir, 'rt_xtrace_stable_part_id', 4242, '2026-01-01T00:00:00Z')
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))

  const tool = (id, callID, input) => ({
    id,
    type: 'tool',
    tool: 'todowrite',
    callID,
    state: { status: 'pending', input },
  })

  try {
    const first = await xTrace.captureSessionMessages(created.journal, managerSession, [
      {
        info: { id: 'asst_drifting_run', role: 'assistant' },
        parts: [
          { id: 'part_reasoning_0', type: 'reasoning', text: 'first' },
          tool('part_tool_1', 'call_tool_1', { planComplete: false, obligations: [] }),
          { id: 'part_reasoning_2', type: 'reasoning', text: 'second' },
          tool('part_target', 'call_target', { planComplete: false, obligations: [{ name: 'proof', work: 'ship' }] }),
        ],
      },
    ])
    assert.equal(first.ok, true, first.ok ? '' : JSON.stringify(first.error))
    assert.equal(first.value.parts.length, 4)

    const second = await xTrace.captureSessionMessages(created.journal, managerSession, [
      {
        info: { id: 'asst_drifting_run', role: 'assistant' },
        parts: [
          { id: 'part_reasoning_0', type: 'reasoning', text: 'first' },
          tool('part_tool_1', 'call_tool_1', { planComplete: false, obligations: [] }),
          { id: 'part_reasoning_2', type: 'reasoning', text: 'second' },
          { id: 'part_late_text', type: 'text', text: 'late materialized before the target' },
          tool('part_target', 'call_target', { planComplete: false, obligations: [{ name: 'proof', work: 'ship' }] }),
        ],
      },
    ])
    assert.equal(second.ok, true, second.ok ? '' : JSON.stringify(second.error))

    const traceParts = second.value.parts
    assert.equal(traceParts.length, 5, 'the newly materialized physical part is appended exactly once')
    assert.equal(
      traceParts.filter((part) => part.hostToolPartId === 'part_target').length,
      1,
      'semantic index drift must not duplicate the already captured physical ToolPart',
    )
    assert.equal(
      traceParts.filter((part) => part.textDigest === '7182fe44281c363584a813e84a2f20e0686a00de92f980cef91f807f8a62f886').length,
      1,
      'the late materialized text must not be lost behind the old positional provenance slot',
    )
  } finally {
    journal.JournalSurface_dispose(created.journal)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[SEMANTIC-TRACE-006] TODO-004 joins the persisted ToolPart to its exact durable XTrace range', () => {
  const messages = [
    {
      info: { id: 'asst_manager_run', role: 'assistant' },
      parts: [
        {
          id: 'part_todo',
          type: 'tool',
          tool: 'todowrite',
          callID: 'call_todo',
          state: { status: 'pending', input: { todos: [] } },
        },
      ],
    },
  ]
  const localized = xTrace.resolveLocality(
    managerSession,
    messages,
    [tracePartEnvelope({ sequence: 9 })],
    'call_todo',
  )
  const value = assertLocality(localized)

  assert.equal(value.reviewFrontier.sequence, 9)
  assert.equal(value.range.start.sequence, 9)
  assert.equal(value.range.endExclusive.sequence, 10)
  assert.equal(value.toolPartOrdinal, 1)
})

test('WHAT[SEMANTIC-TRACE-006] duplicate legacy captures of one identical physical tool collapse to its first durable cursor', () => {
  const messages = [
    {
      info: { id: 'asst_legacy_duplicate', role: 'assistant' },
      parts: [
        {
          id: 'part_legacy_duplicate',
          type: 'tool',
          tool: 'todowrite',
          callID: 'call_legacy_duplicate',
          state: { status: 'pending', input: { obligations: [{ name: 'proof', work: 'ship' }] } },
        },
      ],
    },
  ]

  const first = tracePartEnvelope({
    sequence: 17,
    run: 'asst_legacy_duplicate',
    partIndex: 3,
    textRef: 'blobs/legacy-duplicate',
    textDigest: 'digest:legacy-duplicate',
    toolCallId: 'call_legacy_duplicate',
    hostToolPartId: 'part_legacy_duplicate',
  })
  const shiftedReplay = tracePartEnvelope({
    sequence: 18,
    run: 'asst_legacy_duplicate',
    partIndex: 4,
    textRef: 'blobs/legacy-duplicate',
    textDigest: 'digest:legacy-duplicate',
    toolCallId: 'call_legacy_duplicate',
    hostToolPartId: 'part_legacy_duplicate',
  })

  const value = assertLocality(xTrace.resolveLocality(managerSession, messages, [first, shiftedReplay], 'call_legacy_duplicate'))
  assert.equal(value.reviewFrontier.sequence, 17)
  assert.equal(value.range.start.sequence, 17)
})

test('WHAT[SEMANTIC-TRACE-006] conflicting legacy captures of one physical tool remain ambiguous', () => {
  const messages = [
    {
      info: { id: 'asst_legacy_conflict', role: 'assistant' },
      parts: [
        {
          id: 'part_legacy_conflict',
          type: 'tool',
          tool: 'todowrite',
          callID: 'call_legacy_conflict',
          state: { status: 'pending', input: { obligations: [{ name: 'proof', work: 'ship' }] } },
        },
      ],
    },
  ]

  const localized = xTrace.resolveLocality(
    managerSession,
    messages,
    [
      tracePartEnvelope({
        sequence: 17,
        run: 'asst_legacy_conflict',
        partIndex: 3,
        textRef: 'blobs/legacy-conflict-a',
        textDigest: 'digest:legacy-conflict-a',
        toolCallId: 'call_legacy_conflict',
        hostToolPartId: 'part_legacy_conflict',
      }),
      tracePartEnvelope({
        sequence: 18,
        run: 'asst_legacy_conflict',
        partIndex: 4,
        textRef: 'blobs/legacy-conflict-b',
        textDigest: 'digest:legacy-conflict-b',
        toolCallId: 'call_legacy_conflict',
        hostToolPartId: 'part_legacy_conflict',
      }),
    ],
    'call_legacy_conflict',
  )

  assert.equal(localized.ok, false)
  assert.equal(localized.error.code, 'XTraceAmbiguous')
})

test('WHAT[SEMANTIC-TRACE-006] TODO-004 localizes a pending before-hook ToolPart from snapshot before XTrace capture', () => {
  const messages = [
    {
      info: { id: 'asst_pending_run', role: 'assistant' },
      parts: [
        {
          id: 'part_pending_todo',
          type: 'tool',
          tool: 'todowrite',
          callID: 'call_pending_todo',
          state: { status: 'pending', input: { obligations: [{ name: 'proof', work: 'ship it' }] } },
        },
      ],
    },
  ]
  const localized = xTrace.resolveLocality(
    managerSession,
    messages,
    [
      tracePartEnvelope({
        sequence: 8,
        run: 'asst_prior_run',
        turn: 3,
        partIndex: 1,
        kind: 'text',
        toolName: undefined,
        textRef: 'blobs/prior-text',
        textDigest: 'digest:prior-text',
        toolCallId: undefined,
        hostToolPartId: undefined,
      }),
    ],
    'call_pending_todo',
  )
  const value = assertLocality(localized)

  assert.equal(value.providerRun, 'asst_pending_run')
  assert.equal(value.hostToolPartId, 'part_pending_todo')
  assert.equal(value.reviewFrontier.sequence, 9)
  assert.equal(value.range.start.sequence, 9)
  assert.equal(value.range.endExclusive.sequence, 10)
  assert.equal(value.toolPartOrdinal, 1)
})

test('WHAT[SEMANTIC-TRACE-006] TODO-004 pending empty todowrite stubs are not semantic sibling calls', () => {
  const messages = [
    {
      info: { id: 'asst_pending_stub_run', role: 'assistant' },
      parts: [
        {
          id: 'part_stub_a',
          type: 'tool',
          tool: 'todowrite',
          callID: 'call_stub_a',
          state: { status: 'pending', input: {} },
        },
        {
          id: 'part_stub_b',
          type: 'tool',
          tool: 'todowrite',
          callID: 'call_stub_b',
          state: { status: 'pending', input: {} },
        },
        {
          id: 'part_current',
          type: 'tool',
          tool: 'todowrite',
          callID: 'call_current',
          state: {
            status: 'pending',
            input: {
              planComplete: true,
              workingOn: 'ship',
              obligations: [{ name: 'ship', work: 'Ship the reviewed road.' }],
            },
          },
        },
      ],
    },
  ]
  const localized = xTrace.resolveLocality(
    managerSession,
    messages,
    [
      tracePartEnvelope({
        sequence: 8,
        run: 'asst_prior_run',
        turn: 3,
        partIndex: 1,
        kind: 'text',
        toolName: undefined,
        textRef: 'blobs/prior-text',
        textDigest: 'digest:prior-text',
        toolCallId: undefined,
        hostToolPartId: undefined,
      }),
    ],
    'call_current',
  )
  const value = assertLocality(localized)

  assert.deepEqual(value.todowriteCallIdsInMessage, ['call_current'])

  const captured = xTrace.resolveLocality(
    managerSession,
    messages,
    [
      tracePartEnvelope({
        sequence: 12,
        run: 'asst_pending_stub_run',
        partIndex: 3,
        toolCallId: 'call_current',
        hostToolPartId: 'part_current',
      }),
    ],
    'call_current',
  )
  assert.deepEqual(assertLocality(captured).todowriteCallIdsInMessage, ['call_current'])
})

test('WHAT[SEMANTIC-TRACE-006] TODO-004 a populated sibling todowrite remains a real protocol sibling', () => {
  const messages = [
    {
      info: { id: 'asst_real_sibling_run', role: 'assistant' },
      parts: [
        {
          id: 'part_real_a',
          type: 'tool',
          tool: 'todowrite',
          callID: 'call_real_a',
          state: { status: 'pending', input: { planComplete: false, workingOn: '', obligations: [] } },
        },
        {
          id: 'part_real_b',
          type: 'tool',
          tool: 'todowrite',
          callID: 'call_real_b',
          state: { status: 'pending', input: { planComplete: true, workingOn: '', obligations: [] } },
        },
      ],
    },
  ]
  const localized = xTrace.resolveLocality(
    managerSession,
    messages,
    [
      tracePartEnvelope({
        sequence: 8,
        run: 'asst_prior_run',
        turn: 3,
        partIndex: 1,
        kind: 'text',
        toolName: undefined,
        textRef: 'blobs/prior-text',
        textDigest: 'digest:prior-text',
        toolCallId: undefined,
        hostToolPartId: undefined,
      }),
    ],
    'call_real_b',
  )
  const value = assertLocality(localized)

  assert.deepEqual(value.todowriteCallIdsInMessage, ['call_real_a', 'call_real_b'])
})

test('WHAT[SEMANTIC-TRACE-006] TODO-004 pending before-hook ReviewFrontier includes last assistant text in the same message', () => {
  const messages = [
    {
      info: { id: 'asst_pending_run', role: 'assistant' },
      parts: [
        { type: 'text', text: 'I will update the plan.' },
        {
          id: 'part_pending_todo',
          type: 'tool',
          tool: 'todowrite',
          callID: 'call_pending_todo',
          state: { status: 'pending', input: { obligations: [{ name: 'proof', work: 'ship it' }] } },
        },
      ],
    },
  ]
  const localized = xTrace.resolveLocality(
    managerSession,
    messages,
    [
      tracePartEnvelope({
        sequence: 8,
        run: 'asst_prior_run',
        turn: 3,
        partIndex: 1,
        kind: 'text',
        toolName: undefined,
        textRef: 'blobs/prior-text',
        textDigest: 'digest:prior-text',
        toolCallId: undefined,
        hostToolPartId: undefined,
      }),
    ],
    'call_pending_todo',
  )
  const value = assertLocality(localized)

  // next-assigned = 9 is the last assistant text; tool-call occupies 10 = Before(Tk)
  assert.equal(value.reviewFrontier.sequence, 10)
  assert.equal(value.range.start.sequence, 10)
  assert.equal(value.range.endExclusive.sequence, 11)
})

test('WHAT[SEMANTIC-TRACE-006] TODO-004 pending ReviewFrontier does not double-count a current-message prefix already captured in XTrace', () => {
  const messages = [
    {
      info: { id: 'asst_pending_captured_prefix', role: 'assistant' },
      parts: [
        { type: 'reasoning', text: 'checking the current account' },
        { type: 'text', text: 'I will update the plan.' },
        {
          id: 'part_pending_captured_prefix',
          type: 'tool',
          tool: 'todowrite',
          callID: 'call_pending_captured_prefix',
          state: { status: 'pending', input: { obligations: [{ name: 'proof', work: 'ship it' }] } },
        },
      ],
    },
  ]
  const localized = xTrace.resolveLocality(
    managerSession,
    messages,
    [
      tracePartEnvelope({
        sequence: 8,
        run: 'asst_prior_run',
        turn: 3,
        partIndex: 1,
        kind: 'text',
        toolName: undefined,
        textRef: 'blobs/prior-text',
        textDigest: 'digest:prior-text',
        toolCallId: undefined,
        hostToolPartId: undefined,
      }),
      tracePartEnvelope({
        sequence: 9,
        run: 'asst_pending_captured_prefix',
        turn: 4,
        partIndex: 0,
        kind: 'reasoning',
        toolName: undefined,
        textRef: 'blobs/current-reasoning',
        textDigest: 'digest:current-reasoning',
        toolCallId: undefined,
        hostToolPartId: undefined,
      }),
      tracePartEnvelope({
        sequence: 10,
        run: 'asst_pending_captured_prefix',
        turn: 4,
        partIndex: 1,
        kind: 'text',
        toolName: undefined,
        textRef: 'blobs/current-text',
        textDigest: 'digest:current-text',
        toolCallId: undefined,
        hostToolPartId: undefined,
      }),
    ],
    'call_pending_captured_prefix',
  )
  const value = assertLocality(localized)

  assert.equal(value.reviewFrontier.sequence, 11, 'Before(Tk) is one-past the already captured semantic prefix')
})

test('WHAT[SEMANTIC-TRACE-006] TODO-008 ManagerCheckpointLWR range includes last assistant text before todowrite', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'xtrace-locality-lwr-'))
  const created = await journal.JournalSurface_boot(dir, 'rt_xtrace_locality_lwr', 4242, '2026-01-01T00:00:00Z')
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))

  try {
    await xTrace.captureOpening(created.journal, managerSession, 'task', [])
    const trace = await xTrace.captureMessageView(created.journal, managerSession, [
      {
        info: { id: 'asst_manager_run', role: 'assistant' },
        parts: [
          { type: 'text', text: 'I will update the plan.' },
          {
            id: 'part_todo',
            type: 'tool',
            tool: 'todowrite',
            callID: 'call_todo',
            state: { status: 'pending', input: { todos: [] } },
          },
        ],
      },
    ])
    const parts = xTrace.parts(trace)
    assert.equal(parts.length, 2)
    assert.equal(parts[0].cursor.sequence, 1)
    assert.equal(parts[1].cursor.sequence, 2)

    const bounded = await xTrace.lifecycleWorkRecordBounded(created.journal, managerSession, 1, 2)
    assert.equal(typeof bounded, 'string')
    assert.match(bounded, /I will update the plan/)
    assert.doesNotMatch(bounded, /^Opening\n/m)
  } finally {
    journal.JournalSurface_dispose(created.journal)
    rmSync(dir, { recursive: true, force: true })
  }
})
