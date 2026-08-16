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
} from '../../../dist/Mission/Obligation/Todo/MagicTodoLocality.js'
import { MagicTodoHostHooks_create } from '../../../dist/Mission/Obligation/Todo/MagicTodoMembrane.js'
import { Obligation } from '../../../dist/Mission/Obligation/Todo/Model.js'
import {
  SessionMessage,
  SessionToolPart,
  SnapshotToolPartState,
} from '../../../dist/OpenCode/Host/SessionSnapshotPort.js'
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
} from '../../verification-system/tests/support/domain.mjs'

const openLife = async (journal, session, life) => {
  const appended = await agentJournal.appendManagerLifecycle(
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

const reviewRuntimeStub = {
  EnsureReview: () => Promise.resolve(),
  AwaitConsumableReview: () => Promise.resolve(),
}

const withJournal = async (body) => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-magic-todo-membrane-'))
  const created = await agentJournal.create({ directory, runtime: 'rt_magic_todo_membrane' })
  assert.equal(created.ok, true, created.ok ? '' : String(created.error))
  try {
    return await body(created.journal)
  } finally {
    created.dispose()
    rmSync(directory, { recursive: true, force: true })
  }
}

test('WHAT[OBLIGATION-LEDGER-025] before returns without waiting for snapshot or Journal IO', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-magic-todo-before-latency-'))
  const created = await agentJournal.create({ directory, runtime: 'rt_magic_todo_before_latency' })
  assert.equal(created.ok, true, created.ok ? '' : String(created.error))

  let releaseSnapshot
  const snapshot = {
    GetMessages: () =>
      new Promise((resolve) => {
        releaseSnapshot = resolve
      }),
  }

  try {
    const hooks = MagicTodoHostHooks_create(created.journal, snapshot, reviewRuntimeStub)
    const output = {
      args: {
        planComplete: false,
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
    // Leave the deferred snapshot promise unresolved: this test proves Before
    // does not wait for it. Resolving it as an Error would intentionally take
    // the new infrastructure-fatal path after the test has already finished.
    created.dispose()
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[OBLIGATION-LEDGER-009] malformed obligation shape is the provider-red class', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-magic-todo-syntax-red-'))
  const created = await agentJournal.create({ directory, runtime: 'rt_magic_todo_syntax_red' })
  assert.equal(created.ok, true, created.ok ? '' : String(created.error))

  const snapshot = { GetMessages: () => Promise.resolve({ tag: 1, fields: ['must not be reached'] }) }

  try {
    const hooks = MagicTodoHostHooks_create(created.journal, snapshot, reviewRuntimeStub)
    await assert.rejects(
      () =>
        hooks.Before(
          { tool: 'todowrite', sessionID: 'ses-syntax-red', callID: 'call-syntax-red' },
          {
            args: {
              planComplete: false,
              obligations: [
                { name: 'same', work: 'first' },
                { name: 'same', work: 'second' },
              ],
            },
          },
        ),
      (error) => {
        const message = String(error && error.message ? error.message : error)
        assert.match(message, /duplicate obligation name/i)
        assert.doesNotMatch(message, /Diagnostic\.fatal|infrastructure/i)
        return true
      },
    )
  } finally {
    created.dispose()
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[OBLIGATION-LEDGER-009] missing process-review runtime is infrastructure-fatal, not provider red', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-magic-todo-runtime-fatal-'))
  const created = await agentJournal.create({ directory, runtime: 'rt_magic_todo_runtime_fatal' })
  assert.equal(created.ok, true, created.ok ? '' : String(created.error))

  try {
    const hooks = MagicTodoHostHooks_create(
      created.journal,
      { GetMessages: () => Promise.resolve({ tag: 1, fields: ['unused'] }) },
      undefined,
    )

    await assert.rejects(
      () =>
        hooks.Before(
          { tool: 'todowrite', sessionID: 'ses-runtime-fatal', callID: 'call-runtime-fatal' },
          { args: { planComplete: true, obligations: [{ name: 'work', work: 'Do real mission work.' }] } },
        ),
      (error) => {
        const message = String(error && error.message ? error.message : error)
        assert.match(message, /unreachable after Diagnostic\.fatal/)
        assert.match(message, /process review runtime/)
        return true
      },
    )
  } finally {
    created.dispose()
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[OBLIGATION-LEDGER-025] prepare rejects a pending ToolPart whose provider input is still empty', async () => {
  await withJournal(async (journal) => {
    const session = sessionId('ses-magic-todo-pending-input')
    const life = managerLifeId('life-magic-todo-pending-input')
    const call = toolCallId('call-magic-todo-pending-input')
    await openLife(journal, session, life)

    const result = await magicTodoMembrane.prepare(
      journal,
      session,
      locality({ call, inputCanonical: '{}' }),
      'provider-input-digest',
      false,
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

test('WHAT[OBLIGATION-LEDGER-025] before materializes the exact provider input including planComplete', () => {
  const call = toolCallId('call-magic-todo-await-input')
  const expected = magicTodoHost.canonicalInput({
    planComplete: false,
    obligations: [{ name: 'diagnose', work: 'Fix the todowrite snapshot race.' }],
  })

  const result = materializeInput(
    locality({ call, inputCanonical: '{}' }),
    expected,
  )

  assert.equal(result.tag, 0)
  assert.equal(result.fields[0].InputCanonical, expected)
})

test('WHAT[OBLIGATION-LEDGER-025] materialization fails closed when the provider input differs', () => {
  const call = toolCallId('call-magic-todo-await-conflict')

  const result = materializeInput(
    locality({
      call,
      inputCanonical: magicTodoHost.canonicalInput({
        planComplete: false,
        obligations: [{ name: 'other', work: 'Different provider input.' }],
      }),
      state: new SnapshotToolPartState(1, []),
    }),
    magicTodoHost.canonicalInput({
      planComplete: false,
      obligations: [{ name: 'diagnose', work: 'Fix the todowrite snapshot race.' }],
    }),
  )

  assert.equal(result.tag, 1)
  assert.equal(result.fields[0].cases()[result.fields[0].tag], 'InputMismatch')
})

test('WHAT[OBLIGATION-LEDGER-025] materialized snapshot input must still match tool.execute.before args', async () => {
  await withJournal(async (journal) => {
    const session = sessionId('ses-magic-todo-conflicting-input')
    const life = managerLifeId('life-magic-todo-conflicting-input')
    const call = toolCallId('call-magic-todo-conflicting-input')
    await openLife(journal, session, life)

    const result = await magicTodoMembrane.prepare(
      journal,
      session,
      locality({
        call,
        inputCanonical: magicTodoHost.canonicalInput({
          planComplete: false,
          obligations: [{ name: 'other', work: 'Different provider input.' }],
        }),
      }),
      'provider-input-digest',
      false,
      [new Obligation('diagnose', 'Fix the todowrite snapshot race.')],
    )

    assert.equal(result.ok, false)
    assert.equal(result.error.cases()[result.error.tag], 'SnapshotInputMismatch')
  })
})


const sha256Hex = (value) => createHash('sha256').update(value).digest('hex')

const checkpoint = async (journal, session, callText, obligations, planComplete = true) => {
  const call = toolCallId(callText)
  const args = { planComplete, obligations }
  const inputCanonical = magicTodoHost.canonicalInput(args)
  const digest = magicTodoHost.canonicalInputDigest(sha256Hex, args)
  const submitted = obligations.map((row) => new Obligation(row.name, row.work))
  return {
    digest,
    result: await magicTodoMembrane.prepare(
      journal,
      session,
      locality({ call, inputCanonical }),
      digest,
      planComplete,
      submitted,
    ),
  }
}

const acceptPlanningFalseCheckpoint = async (journal, session, life, callText) => {
  const planning = await checkpoint(journal, session, callText, [
    { name: 'inspect-startup', work: 'Inspect startup paths so the implementation plan can be completed.' },
  ], false)
  assert.equal(planning.result.ok, true, planning.result.ok ? '' : planning.result.error.cases()[planning.result.error.tag])

  const accepted = await magicTodoMembrane.accept(
    journal,
    planning.result.value,
    magicTodoJournal.PhysicalSuccessEvidence.LiveAfterSuccess,
    planning.digest,
    sha256Hex('planning-false-physical-output'),
  )
  assert.equal(accepted.ok, true, accepted.ok ? '' : accepted.error.cases()[accepted.error.tag])
  return { planning, accepted }
}

test('WHAT[OBLIGATION-LEDGER-016] first accepted planComplete=false stays at the Planning Table without commitment', async () => {
  await withJournal(async (journal) => {
    const session = sessionId('ses-magic-todo-planning-false')
    const life = managerLifeId('life-magic-todo-planning-false')
    await openLife(journal, session, life)
    providerLanguage.clearAllForTests()
    const bound = providerLanguage.bindOnce(session, providerLanguage.english)
    assert.equal(bound.ok, true, bound.ok ? '' : String(bound.error))

    try {
      await acceptPlanningFalseCheckpoint(journal, session, life, 'call-magic-todo-planning-false')

      const lifeState = agentJournal.snapshot(journal).AgentProjections.MagicTodo.ByLife.get('life-magic-todo-planning-false')
      assert.equal(lifeState.FirstPlanCommitment, undefined)
    } finally {
      providerLanguage.clearAllForTests()
    }
  })
})

test('WHAT[OBLIGATION-LEDGER-026] accepted planComplete=false carries no T1 entrustment revelation (revelation is reserved for the first accepted true)', async () => {
  await withJournal(async (journal) => {
    const session = sessionId('ses-magic-todo-planning-false')
    const life = managerLifeId('life-magic-todo-planning-false')
    await openLife(journal, session, life)
    providerLanguage.clearAllForTests()
    const bound = providerLanguage.bindOnce(session, providerLanguage.english)
    assert.equal(bound.ok, true, bound.ok ? '' : String(bound.error))

    try {
      const { accepted } = await acceptPlanningFalseCheckpoint(journal, session, life, 'call-magic-todo-planning-false')
      assert.doesNotMatch(accepted.value.EnrichedResult, /Manager who will carry it is you|The road is yours/i)
    } finally {
      providerLanguage.clearAllForTests()
    }
  })
})

const acceptT1Checkpoint = async (journal, session, life, callText) => {
  const t1 = await checkpoint(journal, session, callText, [
    { name: 'diagnose', work: 'Establish why the first todowrite succeeds.' },
  ])
  assert.equal(t1.result.ok, true, t1.result.ok ? '' : t1.result.error.cases()[t1.result.error.tag])

  const accepted = await magicTodoMembrane.accept(
    journal,
    t1.result.value,
    magicTodoJournal.PhysicalSuccessEvidence.LiveAfterSuccess,
    t1.digest,
    sha256Hex('t1-physical-output'),
  )
  assert.equal(accepted.ok, true, accepted.ok ? '' : accepted.error.cases()[accepted.error.tag])
  return { t1, accepted }
}

const concludePerfectReview = async (journal, session, life, callText, t1) => {
  const write = magicTodo.todoWriteId(sha256Hex, life, toolCallId(callText))
  const review = magicTodo.todoReviewId(sha256Hex, life, write)
  const reviewer = magicTodo.dedicatedReviewerId(sha256Hex, life)
  const reviewerSession = sessionId(`ses-todo-reviewer-${callText}`)
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
  const proposed = t1.result.value.Prepared
  const reviewRecord = await agentJournal.writeBlob('R1 found no material issue.', journal)
  assert.equal(reviewRecord.ok, true, reviewRecord.ok ? '' : String(reviewRecord.error))
  const concluded = new magicTodoJournal.TodoReviewConcluded(
    life,
    write,
    review,
    reviewer,
    reviewerSession,
    magicTodo.perfect,
    reviewRecord.value.BlobRef,
    reviewRecord.value.BlobDigest,
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
    const appended = await agentJournal.appendMagicTodo(
      stream.session(session),
      undefined,
      magicTodoJournal.MagicTodoFact(caseName, [payload]),
      journal,
    )
    assert.equal(appended.ok, true, appended.ok ? '' : String(appended.error))
  }
}

test('WHAT[OBLIGATION-LEDGER-012] T1 accept derives the process-review duties (SSOT = TodoWriteAccepted)', async () => {
  await withJournal(async (journal) => {
    const session = sessionId('ses-magic-todo-t1-t2-lag1')
    const life = managerLifeId('life-magic-todo-t1-t2-lag1')
    await openLife(journal, session, life)
    providerLanguage.clearAllForTests()
    const bound = providerLanguage.bindOnce(session, providerLanguage.english)
    assert.equal(bound.ok, true, bound.ok ? '' : String(bound.error))

    try {
      const { accepted } = await acceptT1Checkpoint(journal, session, life, 'call-magic-todo-t1')
      assert.equal(accepted.value.NeedsEnsureReview, true)
      assert.equal(accepted.value.NeedsDedicatedEnlist, true)

      const snap = agentJournal.snapshot(journal)
      const lifeState = snap.AgentProjections.MagicTodo.ByLife.get('life-magic-todo-t1-t2-lag1')
      const checkpoints = mapEntries(lifeState.Checkpoints)
      assert.equal(checkpoints.length, 1)
      assert.equal(checkpoints[0][1].Accepted, true)
      assert.equal(checkpoints[0][1].Assignment == null, true)
      assert.equal(checkpoints[0][1].Concluded == null, true)
    } finally {
      providerLanguage.clearAllForTests()
    }
  })
})

test('WHAT[OBLIGATION-LEDGER-010] T1 accept makes the proposed account Current immediately, before any review', async () => {
  await withJournal(async (journal) => {
    const session = sessionId('ses-magic-todo-t1-t2-lag1')
    const life = managerLifeId('life-magic-todo-t1-t2-lag1')
    await openLife(journal, session, life)
    providerLanguage.clearAllForTests()
    const bound = providerLanguage.bindOnce(session, providerLanguage.english)
    assert.equal(bound.ok, true, bound.ok ? '' : String(bound.error))

    try {
      const { t1 } = await acceptT1Checkpoint(journal, session, life, 'call-magic-todo-t1')

      const lifeState = agentJournal.snapshot(journal).AgentProjections.MagicTodo.ByLife.get('life-magic-todo-t1-t2-lag1')
      assert.equal(lifeState.CurrentObligationsRef[0].fields[0], t1.result.value.Prepared.ProposedTodoRef.fields[0])
      assert.equal(lifeState.CurrentObligationsRef[1].fields[0], t1.result.value.Prepared.ProposedTodoDigest.fields[0])
    } finally {
      providerLanguage.clearAllForTests()
    }
  })
})

test('WHAT[OBLIGATION-LEDGER-026] first accepted planComplete=true reveals entrustment in the enriched result', async () => {
  await withJournal(async (journal) => {
    const session = sessionId('ses-magic-todo-t1-t2-lag1')
    const life = managerLifeId('life-magic-todo-t1-t2-lag1')
    await openLife(journal, session, life)
    providerLanguage.clearAllForTests()
    const bound = providerLanguage.bindOnce(session, providerLanguage.english)
    assert.equal(bound.ok, true, bound.ok ? '' : String(bound.error))

    try {
      const { accepted } = await acceptT1Checkpoint(journal, session, life, 'call-magic-todo-t1')
      assert.match(accepted.value.EnrichedResult, /Manager who will carry it is you|The road is yours/i)
    } finally {
      providerLanguage.clearAllForTests()
    }
  })
})

test('WHAT[OBLIGATION-LEDGER-013] T2 prepare while R1 is outstanding is a legal lag-1 wait, not a fail-closed Admission', async () => {
  await withJournal(async (journal) => {
    const session = sessionId('ses-magic-todo-t1-t2-lag1')
    const life = managerLifeId('life-magic-todo-t1-t2-lag1')
    await openLife(journal, session, life)
    providerLanguage.clearAllForTests()
    const bound = providerLanguage.bindOnce(session, providerLanguage.english)
    assert.equal(bound.ok, true, bound.ok ? '' : String(bound.error))

    try {
      await acceptT1Checkpoint(journal, session, life, 'call-magic-todo-t1')

      const t2 = await checkpoint(journal, session, 'call-magic-todo-t2', [
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


test('WHAT[OBLIGATION-LEDGER-014] T2 prepare is gated on a consumable TodoReviewConcluded, not on a mere verdict', async () => {
  await withJournal(async (journal) => {
    const session = sessionId('ses-magic-todo-t1-t2-resolve')
    const life = managerLifeId('life-magic-todo-t1-t2-resolve')
    const callText = 'call-magic-todo-t1'
    await openLife(journal, session, life)
    providerLanguage.clearAllForTests()
    const bound = providerLanguage.bindOnce(session, providerLanguage.english)
    assert.equal(bound.ok, true, bound.ok ? '' : String(bound.error))

    try {
      const { t1 } = await acceptT1Checkpoint(journal, session, life, callText)

      // T2 before the review concludes is a legal lag-1 wait, not invalidOp.
      const t2Early = await checkpoint(journal, session, 'call-magic-todo-t2', [
        { name: 'diagnose', work: 'Establish why the first todowrite succeeds.' },
        { name: 'fix', work: 'Keep later todowrite calls from failing red.' },
      ])
      assert.equal(t2Early.result.ok, false)
      assert.equal(
        t2Early.result.error.cases()[t2Early.result.error.tag],
        'AwaitingConsumableReview',
        'T2 blocks on ConsumableReview while R1 is outstanding',
      )

      // Complete the process review: ConsumableReview ≡ TodoReviewConcluded.
      await concludePerfectReview(journal, session, life, callText, t1)

      // T2 now proceeds: the lag-1 wait resolved because ConsumableReview is durable.
      const t2 = await checkpoint(journal, session, 'call-magic-todo-t2', [
        { name: 'diagnose', work: 'Establish why the first todowrite succeeds.' },
        { name: 'fix', work: 'Keep later todowrite calls from failing red.' },
      ])
      assert.equal(t2.result.ok, true, t2.result.ok ? '' : t2.result.error.cases()[t2.result.error.tag])

      const t2Accepted = await magicTodoMembrane.accept(
        journal,
        t2.result.value,
        magicTodoJournal.PhysicalSuccessEvidence.LiveAfterSuccess,
        t2.digest,
        sha256Hex('t2-physical-output'),
      )
      assert.equal(t2Accepted.ok, true, t2Accepted.ok ? '' : t2Accepted.error.cases()[t2Accepted.error.tag])
    } finally {
      providerLanguage.clearAllForTests()
    }
  })
})

test('WHAT[OBLIGATION-LEDGER-026] enriched result after a concluded PERFECT review is silent about the previous review', async () => {
  await withJournal(async (journal) => {
    const session = sessionId('ses-magic-todo-t1-t2-resolve')
    const life = managerLifeId('life-magic-todo-t1-t2-resolve')
    const callText = 'call-magic-todo-t1'
    await openLife(journal, session, life)
    providerLanguage.clearAllForTests()
    const bound = providerLanguage.bindOnce(session, providerLanguage.english)
    assert.equal(bound.ok, true, bound.ok ? '' : String(bound.error))

    try {
      const { t1 } = await acceptT1Checkpoint(journal, session, life, callText)
      await concludePerfectReview(journal, session, life, callText, t1)

      const t2 = await checkpoint(journal, session, 'call-magic-todo-t2', [
        { name: 'diagnose', work: 'Establish why the first todowrite succeeds.' },
        { name: 'fix', work: 'Keep later todowrite calls from failing red.' },
      ])
      assert.equal(t2.result.ok, true, t2.result.ok ? '' : t2.result.error.cases()[t2.result.error.tag])

      const t2Accepted = await magicTodoMembrane.accept(
        journal,
        t2.result.value,
        magicTodoJournal.PhysicalSuccessEvidence.LiveAfterSuccess,
        t2.digest,
        sha256Hex('t2-physical-output'),
      )
      assert.equal(t2Accepted.ok, true, t2Accepted.ok ? '' : t2Accepted.error.cases()[t2Accepted.error.tag])
      assert.match(t2Accepted.value.EnrichedResult, /Keep working/)
      assert.doesNotMatch(t2Accepted.value.EnrichedResult, /Previous checkpoint review|R1 found no material issue/)
    } finally {
      providerLanguage.clearAllForTests()
    }
  })
})

test('WHAT[OBLIGATION-LEDGER-010] T2 accepted account supersedes CurrentObligations', async () => {
  await withJournal(async (journal) => {
    const session = sessionId('ses-magic-todo-t1-t2-resolve')
    const life = managerLifeId('life-magic-todo-t1-t2-resolve')
    const callText = 'call-magic-todo-t1'
    await openLife(journal, session, life)
    providerLanguage.clearAllForTests()
    const bound = providerLanguage.bindOnce(session, providerLanguage.english)
    assert.equal(bound.ok, true, bound.ok ? '' : String(bound.error))

    try {
      const { t1 } = await acceptT1Checkpoint(journal, session, life, callText)
      await concludePerfectReview(journal, session, life, callText, t1)

      const t2 = await checkpoint(journal, session, 'call-magic-todo-t2', [
        { name: 'diagnose', work: 'Establish why the first todowrite succeeds.' },
        { name: 'fix', work: 'Keep later todowrite calls from failing red.' },
      ])
      assert.equal(t2.result.ok, true, t2.result.ok ? '' : t2.result.error.cases()[t2.result.error.tag])

      const t2Accepted = await magicTodoMembrane.accept(
        journal,
        t2.result.value,
        magicTodoJournal.PhysicalSuccessEvidence.LiveAfterSuccess,
        t2.digest,
        sha256Hex('t2-physical-output'),
      )
      assert.equal(t2Accepted.ok, true, t2Accepted.ok ? '' : t2Accepted.error.cases()[t2Accepted.error.tag])

      const afterT2 = agentJournal.snapshot(journal).AgentProjections.MagicTodo.ByLife.get(
        'life-magic-todo-t1-t2-resolve',
      )
      assert.equal(afterT2.CurrentObligationsRef[0].fields[0], t2.result.value.Prepared.ProposedTodoRef.fields[0])
    } finally {
      providerLanguage.clearAllForTests()
    }
  })
})

test('WHAT[OBLIGATION-LEDGER-011] REVISE is feedback only: next checkpoint sees the report and Current never rolls back', async () => {
  await withJournal(async (journal) => {
    const session = sessionId('ses-magic-todo-revise-feedback')
    const life = managerLifeId('life-magic-todo-revise-feedback')
    const callText = 'call-revise-t1'
    await openLife(journal, session, life)
    providerLanguage.clearAllForTests()
    const bound = providerLanguage.bindOnce(session, providerLanguage.english)
    assert.equal(bound.ok, true, bound.ok ? '' : String(bound.error))

    try {
      const t1 = await checkpoint(journal, session, callText, [
        { name: 'implementation', work: 'Implement the requested behavior.' },
      ])
      assert.equal(t1.result.ok, true)
      const accepted = await magicTodoMembrane.accept(
        journal,
        t1.result.value,
        magicTodoJournal.PhysicalSuccessEvidence.LiveAfterSuccess,
        t1.digest,
        sha256Hex('revise-t1-output'),
      )
      assert.equal(accepted.ok, true)

      const write = magicTodo.todoWriteId(sha256Hex, life, toolCallId(callText))
      const review = magicTodo.todoReviewId(sha256Hex, life, write)
      const reviewer = magicTodo.dedicatedReviewerId(sha256Hex, life)
      const reviewerSession = sessionId('ses-revise-reviewer')
      const cursor = (n) => new magicTodoJournal.XTraceCursor(BigInt(n))
      const reviewText = 'The account omitted the required runtime verification.'
      const reviewRecord = await agentJournal.writeBlob(reviewText, journal)
      assert.equal(reviewRecord.ok, true, reviewRecord.ok ? '' : String(reviewRecord.error))

      const facts = [
        [
          'DedicatedTodoReviewerEnlisted',
          new magicTodoJournal.DedicatedTodoReviewerEnlisted(life, reviewer, reviewerSession),
        ],
        [
          'TodoProcessReviewAssigned',
          new magicTodoJournal.TodoProcessReviewAssigned(
            life,
            write,
            review,
            reviewer,
            reviewerSession,
            cursor(8),
            cursor(7),
          ),
        ],
        [
          'TodoReviewConcluded',
          new magicTodoJournal.TodoReviewConcluded(
            life,
            write,
            review,
            reviewer,
            reviewerSession,
            magicTodo.revise,
            reviewRecord.value.BlobRef,
            reviewRecord.value.BlobDigest,
            // Historical settlement fields deliberately point at the old base.
            // TODO-005 requires projection to ignore them as Current writers.
            t1.result.value.Prepared.BaseTodoRef,
            t1.result.value.Prepared.BaseTodoDigest,
            cursor(10),
            providerRun('revise-review-provider-run'),
            toolCallId('revise-review-judge'),
          ),
        ],
      ]

      for (const [caseName, payload] of facts) {
        const appended = await agentJournal.appendMagicTodo(
          stream.session(session),
          undefined,
          magicTodoJournal.MagicTodoFact(caseName, [payload]),
          journal,
        )
        assert.equal(appended.ok, true, appended.ok ? '' : String(appended.error))
      }

      const afterRevise = agentJournal.snapshot(journal).AgentProjections.MagicTodo.ByLife.get(
        'life-magic-todo-revise-feedback',
      )
      assert.equal(afterRevise.CurrentObligationsRef[0].fields[0], t1.result.value.Prepared.ProposedTodoRef.fields[0])

      const t2 = await checkpoint(journal, session, 'call-revise-t2', [
        { name: 'implementation', work: 'Implement the requested behavior.' },
        { name: 'verification', work: 'Run the required runtime verification and preserve evidence.' },
      ])
      assert.equal(t2.result.ok, true, t2.result.ok ? '' : t2.result.error.cases()[t2.result.error.tag])

      const t2Accepted = await magicTodoMembrane.accept(
        journal,
        t2.result.value,
        magicTodoJournal.PhysicalSuccessEvidence.LiveAfterSuccess,
        t2.digest,
        sha256Hex('revise-t2-output'),
      )
      assert.equal(t2Accepted.ok, true, t2Accepted.ok ? '' : t2Accepted.error.cases()[t2Accepted.error.tag])
      assert.match(t2Accepted.value.EnrichedResult, /An earlier account of the work left something unresolved/)
      assert.match(t2Accepted.value.EnrichedResult, /omitted the required runtime verification/)
      assert.match(t2Accepted.value.EnrichedResult, /Keep working/)
      assert.doesNotMatch(t2Accepted.value.EnrichedResult, /settled|preview|reviewing/i)

      const afterT2 = agentJournal.snapshot(journal).AgentProjections.MagicTodo.ByLife.get(
        'life-magic-todo-revise-feedback',
      )
      assert.equal(afterT2.CurrentObligationsRef[0].fields[0], t2.result.value.Prepared.ProposedTodoRef.fields[0])
    } finally {
      providerLanguage.clearAllForTests()
    }
  })
})

test('WHAT[OBLIGATION-LEDGER-026] snapshot infrastructure failure takes the process-fatal path, never a todowrite red path', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-magic-todo-after-failclose-'))
  const created = await agentJournal.create({ directory, runtime: 'rt_magic_todo_after_failclose' })
  assert.equal(created.ok, true, created.ok ? '' : String(created.error))

  let releaseSnapshot
  const snapshot = {
    GetMessages: () =>
      new Promise((resolve) => {
        releaseSnapshot = resolve
      }),
  }

  try {
    const hooks = MagicTodoHostHooks_create(created.journal, snapshot, reviewRuntimeStub)
    const output = {
      args: {
        planComplete: false,
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
        // Diagnostic.fatal suppresses SIGKILL under node:test, then the helper's
        // unreachable guard throws so the fatal branch remains assertable.
        const message = String(error && error.message ? error.message : error)
        assert.match(message, /unreachable after Diagnostic\.fatal/)
        assert.match(message, /snapshot unavailable/)
        assert.doesNotMatch(message, /deferred prepare failed/)
        return true
      },
    )
  } finally {
    created.dispose()
    rmSync(directory, { recursive: true, force: true })
  }
})
