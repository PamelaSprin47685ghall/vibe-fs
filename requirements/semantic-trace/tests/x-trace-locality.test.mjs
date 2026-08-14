import assert from 'node:assert/strict'
import test from 'node:test'
import {
  agentJournal,
  blobDigest,
  blobRef,
  envelope,
  fact,
  fold,
  hostToolPartId,
  idValue,
  listItems,
  magicTodoLocality,
  mapEntries,
  providerProjection,
  providerRun,
  sessionId,
  sessionSnapshot,
  stream,
  toolCallId,
  xTraceCapture,
  lifecycleWorkRecordProjection,
} from '../../../tests/unit/support/domain.mjs'

const { XTraceProjection_parts: xTraceParts } = await import('../../../dist/Journal/XTraceProjection.js')

const managerSession = sessionId('ses_xtrace_locality')

test('TODO-004 preserves a captured tool call identity on its durable XTrace range', () => {
  const folded = fold.one(
    fold.empty,
    envelope({
      stream: stream.session(managerSession),
      run: 'asst_manager_run',
      fact: fact('XTracePartAppended', {
        SessionId: managerSession,
        CursorSequence: 7n,
        Role: 'assistant',
        Turn: 4,
        PartIndex: 2,
        Kind: 'tool_call',
        ToolName: 'todowrite',
        TextRef: blobRef('blobs/todo-call'),
        TextDigest: blobDigest('digest:todo-call'),
        Provenance: 'g:0/turn:4/part:2',
        ProviderRun: providerRun('asst_manager_run'),
        ToolCallId: toolCallId('call_todo'),
        HostToolPartId: hostToolPartId('part_todo'),
      }),
    }),
  )

  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  const sessions = mapEntries(folded.value.AgentProjections.Sessions)
  assert.equal(sessions.length, 1)
  const part = sessions[0][1].XTrace.Parts.head
  assert.equal(idValue.providerRun(part.ProviderRun), 'asst_manager_run')
  assert.equal(idValue.toolCall(part.ToolCallId), 'call_todo')
  assert.equal(idValue.hostToolPart(part.HostToolPartId), 'part_todo')
  assert.equal(Number(part.Cursor.Sequence), 7)
})

test('TODO-004 captures the SDK-visible assistant run and Host ToolPart without index inference', async () => {
  const created = await agentJournal.create({ directory: 'xtrace-locality-capture' })
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))

  try {
    const captured = providerProjection.decodeCapturedMessageView([
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

    const trace = await xTraceCapture.captureMessageView(created.journal, managerSession, captured)
    const parts = listItems(xTraceParts(trace))
    assert.equal(parts.length, 2)

    const todoPart = parts[1]
    assert.equal(idValue.providerRun(todoPart.ProviderRun), 'asst_manager_run')
    assert.equal(idValue.toolCall(todoPart.ToolCallId), 'call_todo')
    assert.equal(idValue.hostToolPart(todoPart.HostToolPartId), 'part_todo')
    assert.equal(Number(todoPart.Cursor.Sequence), 2)
  } finally {
    created.dispose()
  }
})

test('TODO-004 joins the persisted ToolPart to its exact durable XTrace range', () => {
  const projection = fold.one(
    fold.empty,
    envelope({
      stream: stream.session(managerSession),
      run: 'asst_manager_run',
      fact: fact('XTracePartAppended', {
        SessionId: managerSession,
        CursorSequence: 9n,
        Role: 'assistant',
        Turn: 4,
        PartIndex: 2,
        Kind: 'tool_call',
        ToolName: 'todowrite',
        TextRef: blobRef('blobs/todo-call'),
        TextDigest: blobDigest('digest:todo-call'),
        Provenance: 'g:0/turn:4/part:2',
        ProviderRun: providerRun('asst_manager_run'),
        ToolCallId: toolCallId('call_todo'),
        HostToolPartId: hostToolPartId('part_todo'),
      }),
    }),
  )
  assert.equal(projection.ok, true, projection.ok ? '' : JSON.stringify(projection.error))

  const messages = sessionSnapshot.projectMessages([
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
  ])
  const localized = magicTodoLocality.resolve(managerSession, messages, projection.value, toolCallId('call_todo'))

  assert.equal(localized.ok, true, localized.ok ? '' : JSON.stringify(localized.error))
  assert.equal(Number(localized.value.ReviewFrontier.Sequence), 9)
  assert.equal(Number(localized.value.Range.Start.Sequence), 9)
  assert.equal(Number(localized.value.Range.EndExclusive.Sequence), 10)
  assert.equal(localized.value.ToolPartOrdinal, 1)
})

test('TODO-004 localizes a pending before-hook ToolPart from snapshot before XTrace capture', () => {
  const projection = fold.one(
    fold.empty,
    envelope({
      stream: stream.session(managerSession),
      run: 'asst_prior_run',
      fact: fact('XTracePartAppended', {
        SessionId: managerSession,
        CursorSequence: 8n,
        Role: 'assistant',
        Turn: 3,
        PartIndex: 1,
        Kind: 'text',
        ToolName: undefined,
        TextRef: blobRef('blobs/prior-text'),
        TextDigest: blobDigest('digest:prior-text'),
        Provenance: 'g:0/turn:3/part:1',
        ProviderRun: providerRun('asst_prior_run'),
        ToolCallId: undefined,
        HostToolPartId: undefined,
      }),
    }),
  )
  assert.equal(projection.ok, true, projection.ok ? '' : JSON.stringify(projection.error))

  const messages = sessionSnapshot.projectMessages([
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
  ])

  const localized = magicTodoLocality.resolve(
    managerSession,
    messages,
    projection.value,
    toolCallId('call_pending_todo'),
  )

  assert.equal(localized.ok, true, localized.ok ? '' : JSON.stringify(localized.error))
  assert.equal(idValue.providerRun(localized.value.ProviderRun), 'asst_pending_run')
  assert.equal(idValue.hostToolPart(localized.value.HostToolPartId), 'part_pending_todo')
  assert.equal(Number(localized.value.ReviewFrontier.Sequence), 9)
  assert.equal(Number(localized.value.Range.Start.Sequence), 9)
  assert.equal(Number(localized.value.Range.EndExclusive.Sequence), 10)
  assert.equal(localized.value.ToolPartOrdinal, 1)
})

test('TODO-004 pending before-hook ReviewFrontier includes last assistant text in the same message', () => {
  const projection = fold.one(
    fold.empty,
    envelope({
      stream: stream.session(managerSession),
      run: 'asst_prior_run',
      fact: fact('XTracePartAppended', {
        SessionId: managerSession,
        CursorSequence: 8n,
        Role: 'assistant',
        Turn: 3,
        PartIndex: 1,
        Kind: 'text',
        ToolName: undefined,
        TextRef: blobRef('blobs/prior-text'),
        TextDigest: blobDigest('digest:prior-text'),
        Provenance: 'g:0/turn:3/part:1',
        ProviderRun: providerRun('asst_prior_run'),
        ToolCallId: undefined,
        HostToolPartId: undefined,
      }),
    }),
  )
  assert.equal(projection.ok, true, projection.ok ? '' : JSON.stringify(projection.error))

  const messages = sessionSnapshot.projectMessages([
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
  ])

  const localized = magicTodoLocality.resolve(
    managerSession,
    messages,
    projection.value,
    toolCallId('call_pending_todo'),
  )

  assert.equal(localized.ok, true, localized.ok ? '' : JSON.stringify(localized.error))
  // next-assigned = 9 is the last assistant text; tool-call occupies 10 = Before(Tk)
  assert.equal(Number(localized.value.ReviewFrontier.Sequence), 10)
  assert.equal(Number(localized.value.Range.Start.Sequence), 10)
  assert.equal(Number(localized.value.Range.EndExclusive.Sequence), 11)
})

test('TODO-008 ManagerCheckpointLWR range includes last assistant text before todowrite', async () => {
  const created = await agentJournal.create({ directory: 'xtrace-locality-lwr-last-text' })
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))

  try {
    await xTraceCapture.captureOpening(created.journal, managerSession, 'task', [])
    const captured = providerProjection.decodeCapturedMessageView([
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
    const trace = await xTraceCapture.captureMessageView(created.journal, managerSession, captured)
    const parts = listItems(xTraceParts(trace))
    assert.equal(parts.length, 2)
    assert.equal(Number(parts[0].Cursor.Sequence), 1)
    assert.equal(Number(parts[1].Cursor.Sequence), 2)

    const bounded = await lifecycleWorkRecordProjection.lifecycleWorkRecordBounded(created.journal, managerSession, {
      StartInclusive: { Sequence: 1 },
      EndExclusive: { Sequence: 2 },
    })
    assert.equal(typeof bounded, 'string')
    assert.match(bounded, /I will update the plan/)
    assert.doesNotMatch(bounded, /^Opening\n/m)
  } finally {
    created.dispose()
  }
})
