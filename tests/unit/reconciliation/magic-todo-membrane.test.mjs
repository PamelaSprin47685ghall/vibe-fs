import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import {
  LocalizedToolCall,
  XTraceRange,
  awaitMaterializedInput,
} from '../../../dist/Application/Reconciliation/MagicTodoLocality.js'
import { MagicTodoHostHooks_create } from '../../../dist/Application/Reconciliation/MagicTodoMembrane.js'
import { Obligation } from '../../../dist/Domain/MagicTodo.js'
import {
  SessionMessage,
  SessionToolPart,
  SnapshotToolPartState,
} from '../../../dist/Infrastructure/OpenCode/Host/SessionSnapshotPort.js'
import {
  agentJournal,
  blobDigest,
  blobRef,
  hostToolPartId,
  magicTodoJournal,
  magicTodoMembrane,
  managerLifeId,
  managerLifecycle,
  physicalUser,
  providerRun,
  sessionId,
  stream,
  toolCallId,
  toList,
} from '../support/domain.mjs'

const openLife = (journal, session, life) => {
  const appended = agentJournal.appendManagerLifecycle(
    stream.session(session),
    managerLifecycle('LifeOpened', {
      SessionId: session,
      LifeId: life,
      OpeningUserMessageId: physicalUser('msg-opening'),
      OpeningTextRef: blobRef('blob-opening'),
      OpeningTextDigest: blobDigest('digest-opening'),
      OpeningCursorSequence: 1n,
    }),
    journal,
  )
  assert.equal(appended.ok, true, appended.ok ? '' : String(appended.error))
}

const locality = ({ call, inputCanonical, state = new SnapshotToolPartState(0, []) }) => {
  const frontier = new magicTodoJournal.XTraceCursor(7n)
  return new LocalizedToolCall(
    providerRun('msg-provider-run'),
    hostToolPartId('prt-todowrite'),
    call,
    'todowrite',
    inputCanonical,
    state,
    toList([call]),
    1,
    frontier,
    new XTraceRange(frontier, new magicTodoJournal.XTraceCursor(8n)),
  )
}

const withJournal = (body) => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-magic-todo-membrane-'))
  const created = agentJournal.create({ directory, runtime: 'rt_magic_todo_membrane' })
  assert.equal(created.ok, true, created.ok ? '' : String(created.error))
  try {
    return body(created.journal)
  } finally {
    created.dispose()
    rmSync(directory, { recursive: true, force: true })
  }
}

test('HOST-019 before returns without waiting for snapshot or Journal IO', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-magic-todo-before-latency-'))
  const created = agentJournal.create({ directory, runtime: 'rt_magic_todo_before_latency' })
  assert.equal(created.ok, true, created.ok ? '' : String(created.error))

  let releaseSnapshot
  const snapshot = {
    GetMessages: () =>
      new Promise((resolve) => {
        releaseSnapshot = resolve
      }),
  }

  try {
    const hooks = MagicTodoHostHooks_create(created.journal, snapshot)
    const output = {
      args: {
        obligations: [{ name: 'diagnose', work: 'Fix the todowrite snapshot race.' }],
      },
    }
    const before = hooks.Before(
      { tool: 'todowrite', sessionID: 'ses-before-latency', callID: 'call-before-latency' },
      output,
    )

    const outcome = await Promise.race([
      before.then(() => 'returned'),
      new Promise((resolve) => setTimeout(() => resolve('blocked'), 25)),
    ])

    assert.equal(outcome, 'returned', 'before must not await the deferred snapshot read')
    assert.equal('obligations' in output.args, false)
    assert.equal(output.args.todos[0].content, 'diagnose: Fix the todowrite snapshot race.')
  } finally {
    releaseSnapshot?.({ tag: 1, fields: ['test cleanup'] })
    created.dispose()
    rmSync(directory, { recursive: true, force: true })
  }
})

test('HOST-019 prepare rejects a pending ToolPart whose provider input is still empty', () => {
  withJournal((journal) => {
    const session = sessionId('ses-magic-todo-pending-input')
    const life = managerLifeId('life-magic-todo-pending-input')
    const call = toolCallId('call-magic-todo-pending-input')
    openLife(journal, session, life)

    const result = magicTodoMembrane.prepare(
      journal,
      session,
      locality({ call, inputCanonical: '{}' }),
      'provider-input-digest',
      [new Obligation('diagnose', 'Fix the todowrite snapshot race.')],
    )

    assert.equal(result.ok, false)
    assert.equal(result.error.cases()[result.error.tag], 'SnapshotInputMismatch')
  })
})

const snapshotMessage = (call, inputCanonical) =>
  new SessionMessage(
    'msg-provider-run',
    'assistant',
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
    false,
    false,
    undefined,
    [],
    [
      new SessionToolPart(
        hostToolPartId('prt-todowrite'),
        call,
        'todowrite',
        inputCanonical,
        new SnapshotToolPartState(0, []),
      ),
    ],
  )

test('HOST-019 before waits until Host materializes the exact provider input', async () => {
  const session = sessionId('ses-magic-todo-await-input')
  const call = toolCallId('call-magic-todo-await-input')
  const expected = '{"obligations":[{"name":"diagnose","work":"Fix the todowrite snapshot race."}]}'
  let reads = 0
  const snapshot = {
    GetMessages: async () => {
      reads += 1
      return {
        tag: 0,
        fields: [toList([snapshotMessage(call, reads === 1 ? '{}' : expected)])],
      }
    },
  }

  const result = await awaitMaterializedInput(
    snapshot,
    session,
    locality({ call, inputCanonical: '{}' }),
    expected,
  )

  assert.equal(result.tag, 0)
  assert.equal(reads, 2, 'pending input must be reread rather than admitted as {}')
  assert.equal(result.fields[0].InputCanonical, expected)
})

test('HOST-019 waiting fails closed when the materialized provider input differs', async () => {
  const session = sessionId('ses-magic-todo-await-conflict')
  const call = toolCallId('call-magic-todo-await-conflict')
  const snapshot = {
    GetMessages: async () => ({
      tag: 0,
      fields: [
        toList([
          snapshotMessage(
            call,
            '{"obligations":[{"name":"other","work":"Different provider input."}]}',
          ),
        ]),
      ],
    }),
  }

  const result = await awaitMaterializedInput(
    snapshot,
    session,
    locality({ call, inputCanonical: '{}' }),
    '{"obligations":[{"name":"diagnose","work":"Fix the todowrite snapshot race."}]}',
  )

  assert.equal(result.tag, 1)
  assert.equal(result.fields[0].cases()[result.fields[0].tag], 'InputMismatch')
})

test('HOST-019 materialized snapshot input must still match tool.execute.before args', () => {
  withJournal((journal) => {
    const session = sessionId('ses-magic-todo-conflicting-input')
    const life = managerLifeId('life-magic-todo-conflicting-input')
    const call = toolCallId('call-magic-todo-conflicting-input')
    openLife(journal, session, life)

    const result = magicTodoMembrane.prepare(
      journal,
      session,
      locality({
        call,
        inputCanonical: '{"obligations":[{"name":"other","work":"Different provider input."}]}',
      }),
      'provider-input-digest',
      [new Obligation('diagnose', 'Fix the todowrite snapshot race.')],
    )

    assert.equal(result.ok, false)
    assert.equal(result.error.cases()[result.error.tag], 'SnapshotInputMismatch')
  })
})
