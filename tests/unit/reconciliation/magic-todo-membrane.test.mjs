import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import {
  LocalizedToolCall,
  XTraceRange,
  materializeInput,
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
  magicTodo,
  magicTodoHost,
  magicTodoJournal,
  magicTodoMembrane,
  mapEntries,
  managerLifeId,
  managerLifecycle,
  physicalUser,
  providerLanguage,
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
    const hooks = MagicTodoHostHooks_create(created.journal, snapshot, undefined)
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
    assert.equal('obligations' in output.args, true)
    assert.equal(Object.prototype.propertyIsEnumerable.call(output.args, 'todos'), false)
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

test('HOST-019 before materializes the exact provider input', () => {
  const call = toolCallId('call-magic-todo-await-input')
  const expected = '{"obligations":[{"name":"diagnose","work":"Fix the todowrite snapshot race."}]}'

  const result = materializeInput(
    locality({ call, inputCanonical: '{}' }),
    expected,
  )

  assert.equal(result.tag, 0)
  assert.equal(result.fields[0].InputCanonical, expected)
})

test('HOST-019 materialization fails closed when the provider input differs', () => {
  const call = toolCallId('call-magic-todo-await-conflict')

  const result = materializeInput(
    locality({
      call,
      inputCanonical: '{"obligations":[{"name":"other","work":"Different provider input."}]}',
      state: new SnapshotToolPartState(1, []),
    }),
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


const sha256Hex = (value) => createHash('sha256').update(value).digest('hex')

const checkpoint = (journal, session, callText, obligations) => {
  const call = toolCallId(callText)
  const args = { obligations }
  const inputCanonical = magicTodoHost.canonicalInput(args)
  const digest = magicTodoHost.canonicalInputDigest(sha256Hex, args)
  const submitted = obligations.map((row) => new Obligation(row.name, row.work))
  return {
    digest,
    result: magicTodoMembrane.prepare(
      journal,
      session,
      locality({ call, inputCanonical }),
      digest,
      submitted,
    ),
  }
}

test('TODO-006 T1 accept succeeds then T2 prepare is a lag-1 wait, not a fail-closed Admission', () => {
  withJournal((journal) => {
    const session = sessionId('ses-magic-todo-t1-t2-lag1')
    const life = managerLifeId('life-magic-todo-t1-t2-lag1')
    openLife(journal, session, life)
    providerLanguage.clearAllForTests()
    const bound = providerLanguage.bindOnce(session, providerLanguage.english)
    assert.equal(bound.ok, true, bound.ok ? '' : String(bound.error))

    try {
      const t1 = checkpoint(journal, session, 'call-magic-todo-t1', [
        { name: 'diagnose', work: 'Establish why the first todowrite succeeds.' },
      ])
      assert.equal(t1.result.ok, true, t1.result.ok ? '' : t1.result.error.cases()[t1.result.error.tag])

      const accepted = magicTodoMembrane.accept(
        journal,
        t1.result.value,
        magicTodoJournal.PhysicalSuccessEvidence.LiveAfterSuccess,
        t1.digest,
        sha256Hex('t1-physical-output'),
      )
      assert.equal(accepted.ok, true, accepted.ok ? '' : accepted.error.cases()[accepted.error.tag])
      assert.equal(accepted.value.NeedsEnsureReview, true)
      assert.equal(accepted.value.NeedsDedicatedEnlist, true)

      const snap = agentJournal.snapshot(journal)
      const lifeState = snap.AgentProjections.MagicTodo.ByLife.get('life-magic-todo-t1-t2-lag1')
      const checkpoints = mapEntries(lifeState.Checkpoints)
      assert.equal(checkpoints.length, 1)
      assert.equal(checkpoints[0][1].Accepted, true)
      assert.equal(checkpoints[0][1].Assignment == null, true)
      assert.equal(checkpoints[0][1].Concluded == null, true)

      const t2 = checkpoint(journal, session, 'call-magic-todo-t2', [
        { name: 'diagnose', work: 'Establish why the first todowrite succeeds.' },
        { name: 'fix', work: 'Keep later todowrite calls from failing red.' },
      ])
      assert.equal(t2.result.ok, false)
      assert.equal(
        t2.result.error.cases()[t2.result.error.tag],
        'AwaitingConsumableReview',
        'T2 must wait for ConsumableReview rather than fail-closed Admission/invalidOp',
      )
    } finally {
      providerLanguage.clearAllForTests()
    }
  })
})


test('TODO-006 T2 prepare succeeds once T1 process review is Concluded (lag-1 wait resolves, no invalidOp)', () => {
  withJournal((journal) => {
    const session = sessionId('ses-magic-todo-t1-t2-resolve')
    const life = managerLifeId('life-magic-todo-t1-t2-resolve')
    const callText = 'call-magic-todo-t1'
    openLife(journal, session, life)
    providerLanguage.clearAllForTests()
    const bound = providerLanguage.bindOnce(session, providerLanguage.english)
    assert.equal(bound.ok, true, bound.ok ? '' : String(bound.error))

    try {
      // T1 accepted.
      const t1 = checkpoint(journal, session, callText, [
        { name: 'diagnose', work: 'Establish why the first todowrite succeeds.' },
      ])
      assert.equal(t1.result.ok, true, t1.result.ok ? '' : t1.result.error.cases()[t1.result.error.tag])

      const accepted = magicTodoMembrane.accept(
        journal,
        t1.result.value,
        magicTodoJournal.PhysicalSuccessEvidence.LiveAfterSuccess,
        t1.digest,
        sha256Hex('t1-physical-output'),
      )
      assert.equal(accepted.ok, true, accepted.ok ? '' : accepted.error.cases()[accepted.error.tag])

      // T2 before the review concludes is a legal lag-1 wait, not invalidOp.
      const t2Early = checkpoint(journal, session, 'call-magic-todo-t2', [
        { name: 'diagnose', work: 'Establish why the first todowrite succeeds.' },
        { name: 'fix', work: 'Keep later todowrite calls from failing red.' },
      ])
      assert.equal(t2Early.result.ok, false)
      assert.equal(
        t2Early.result.error.cases()[t2Early.result.error.tag],
        'AwaitingConsumableReview',
        'T2 blocks on ConsumableReview while R1 is outstanding',
      )

      // Complete the process review: enlist the dedicated reviewer, assign R1,
      // and append ConsumableReview ≡ TodoReviewConcluded with a PERFECT verdict.
      const write = magicTodo.todoWriteId(sha256Hex, life, toolCallId(callText))
      const review = magicTodo.todoReviewId(sha256Hex, life, write)
      const reviewer = magicTodo.dedicatedReviewerId(sha256Hex, life)
      const reviewerSession = sessionId('ses-todo-reviewer-t1-t2-resolve')
      const cursor = (n) => new magicTodoJournal.XTraceCursor(BigInt(n))

      const enlisted = new magicTodoJournal.DedicatedTodoReviewerEnlisted(life, reviewer, reviewerSession)
      const assigned = new magicTodoJournal.TodoProcessReviewAssigned(
        life,
        write,
        review,
        reviewer,
        reviewerSession,
        cursor(8),
        cursor(7),
      )
      // PERFECT settlement is the already-encoded ProposedTodo blob (list wire, not account JSON).
      const proposed = t1.result.value.Prepared
      const concluded = new magicTodoJournal.TodoReviewConcluded(
        life,
        write,
        review,
        reviewer,
        reviewerSession,
        magicTodo.perfect,
        blobRef('lwr-r1'),
        blobDigest('lwr-r1-digest'),
        proposed.ProposedTodoRef,
        proposed.ProposedTodoDigest,
        cursor(10),
        providerRun('reviewer-provider-run'),
        toolCallId('reviewer-judge-call'),
      )

      for (const [caseName, payload] of [
        ['DedicatedTodoReviewerEnlisted', enlisted],
        ['TodoProcessReviewAssigned', assigned],
        ['TodoReviewConcluded', concluded],
      ]) {
        const appended = agentJournal.appendMagicTodo(
          stream.session(session),
          undefined,
          magicTodoJournal.MagicTodoFact(caseName, [payload]),
          journal,
        )
        assert.equal(appended.ok, true, appended.ok ? '' : String(appended.error))
      }

      // T2 now proceeds: the lag-1 wait resolved because ConsumableReview is durable.
      const t2 = checkpoint(journal, session, 'call-magic-todo-t2', [
        { name: 'diagnose', work: 'Establish why the first todowrite succeeds.' },
        { name: 'fix', work: 'Keep later todowrite calls from failing red.' },
      ])
      assert.equal(t2.result.ok, true, t2.result.ok ? '' : t2.result.error.cases()[t2.result.error.tag])
    } finally {
      providerLanguage.clearAllForTests()
    }
  })
})

test('HOST-021 after fail-closes deferred prepare rejection as invalidOp', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-magic-todo-after-failclose-'))
  const created = agentJournal.create({ directory, runtime: 'rt_magic_todo_after_failclose' })
  assert.equal(created.ok, true, created.ok ? '' : String(created.error))

  let releaseSnapshot
  const snapshot = {
    GetMessages: () =>
      new Promise((resolve) => {
        releaseSnapshot = resolve
      }),
  }

  try {
    const hooks = MagicTodoHostHooks_create(created.journal, snapshot, undefined)
    const output = {
      args: {
        obligations: [{ name: 'diagnose', work: 'Fix the todowrite snapshot race.' }],
      },
      output: 'builtin executor succeeded',
    }
    await hooks.Before(
      { tool: 'todowrite', sessionID: 'ses-after-failclose', callID: 'call-after-failclose' },
      output,
    )
    releaseSnapshot({ tag: 1, fields: ['forced snapshot miss'] })

    await assert.rejects(
      () =>
        hooks.After(
          { tool: 'todowrite', sessionID: 'ses-after-failclose', callID: 'call-after-failclose' },
          output,
        ),
      (error) => {
        const message = String(error && error.message ? error.message : error)
        assert.match(message, /Magic Todo deferred prepare failed/)
        assert.match(message, /snapshot unavailable/)
        return true
      },
    )
  } finally {
    created.dispose()
    rmSync(directory, { recursive: true, force: true })
  }
})
