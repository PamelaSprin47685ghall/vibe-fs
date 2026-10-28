import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { mkdirSync, mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const eventStore = await import("../../../dist/Persistence/EventStore/Surface.js");
const journal = await import("../../../dist/Persistence/Journal/Surface.js");
const casebook = await import("../../../dist/Repository/Knowledge/Casebook/Surface.js");
const transaction = await import("../../../dist/Repository/Programming/Js/TransactionSurface.js");

const workspace = await import("../../../dist/OpenCode/Host/WorkspaceEventStoreSurface.js");

const id = (n) => n.toString(16).padStart(40, '0')

const structuralEvent = {
  id: id(1),
  stream: 'canonical-integrator/proof',
  type: 'JobRequested',
  parents: [],
  payload: { proof: 'structural' },
  payloadRefs: [],
}
const mustOk = (result, label) => {
  assert.equal(result.ok, true, `${label}: ${JSON.stringify(result.error)}`)
  return result
}

test('WHAT[durable-events-019] every registered business oracle changes its production Current', async () => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-integrator-registration-'))
  const commonDir = join(root, '.Herdchestra')
  mkdirSync(commonDir, { recursive: true })

  try {
    const store = eventStore.create(commonDir, 'business-registration')

    try {
      mustOk(await eventStore.append(store, [structuralEvent]), 'append Structural fact')

      mustOk(
        await casebook.archive(store, {
          sessionId: 'canonical-case',
          q: 'Q',
          a: 'A',
          observations: [],
          lastAccessOrder: 0,
        }),
        'append Casebook fact',
      )

      mustOk(
        await transaction.appendPrepared(store, {
          transactionId: 'canonical-transaction',
          workspaceRoot: '/canonical-workspace',
          mutation: { path: 'a.txt', originalText: 'before', newText: 'after' },
        }),
        'append JsTransaction fact',
      )

      const fetched = mustOk(await casebook.fetchCase(store, 10, 'canonical-case'), 'read Casebook Current')

      assert.deepEqual(
        {
          structuralHead: eventStore.head(store, structuralEvent.stream),
          structuralEvent: eventStore.read(store, structuralEvent.id)?.payload?.proof ?? null,
          caseAnswer: fetched.value?.a ?? null,
          pendingTransactions: transaction.pending(store).map(({ transactionId }) => transactionId).sort(),
        },
        {
          structuralHead: structuralEvent.id,
          structuralEvent: 'structural',
          caseAnswer: 'A',
          pendingTransactions: ['canonical-transaction'],
        },
      )
    } finally {
      eventStore.dispose(store)
    }

    const reopened = eventStore.create(commonDir, 'business-registration-reopen')
    try {
      assert.equal(eventStore.head(reopened, structuralEvent.stream), structuralEvent.id)
      assert.equal(eventStore.read(reopened, structuralEvent.id)?.payload?.proof, 'structural')
      assert.equal(mustOk(await casebook.fetchCase(reopened, 10, 'canonical-case'), 'replayed Casebook Current').value?.a, 'A')
      assert.deepEqual(transaction.pending(reopened).map(({ transactionId }) => transactionId), ['canonical-transaction'])
    } finally {
      eventStore.dispose(reopened)
    }

    const booted = mustOk(
      await journal.JournalSurface_bootWithWriterId(
        commonDir,
        'journal-registration',
        'runtime-journal-registration',
        4242,
        '9999-01-01T00:00:00Z',
      ),
      'boot Journal',
    )

    try {
      const payload = mustOk(
        await journal.JournalSurface_writePayload(booted.journal, 'canonical opening text'),
        'write Journal payload',
      )
      mustOk(
        await journal.JournalSurface_appendManagerLifecycle(
          booted.journal,
          { kind: 'Session', session: 'canonical-session' },
          {
            case: 'LifeOpened',
            payload: {
              SessionId: 'canonical-session',
              LifeId: 'canonical-life',
              OpeningUserMessageId: 'canonical-message',
              OpeningTextRef: payload.blobRef,
              OpeningTextDigest: payload.blobDigest,
              OpeningCursorSequence: 1,
            },
          },
        ),
        'append Journal fact',
      )
      mustOk(
        await journal.JournalSurface_appendAgent(
          booted.journal,
          { kind: 'Session', session: 'canonical-session' },
          null,
          {
            family: 'Companion',
            case: 'TerminalOutputCaptured',
            payload: {
              SessionId: 'canonical-session',
              TextRef: 'blob-term',
              TextDigest: 'sha-term',
              ProviderRun: 'run-term',
            },
          },
        ),
        'append Journal fact',
      )
      assert.equal(journal.JournalSurface_hasSession(booted.journal, 'canonical-session'), true)
    } finally {
      journal.JournalSurface_dispose(booted.journal)
    }
    const reopenedJournal = mustOk(await journal.JournalSurface_bootWithWriterId(
      commonDir, 'journal-reopen', 'runtime-journal-reopen', 4343, '9999-01-02T00:00:00Z',
    ), 'reopen Journal')
    try {
      assert.equal(journal.JournalSurface_hasSession(reopenedJournal.journal, 'canonical-session'), true)
    } finally {
      journal.JournalSurface_dispose(reopenedJournal.journal)
    }
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})

test('WHAT[durable-events-019] removing one real hostProgram registration blocks its domain result', async () => {
  // Structural 与 Journal 是 spine 注册：移除时 CanonicalIntegrator 的
  // base-rule 前置检查直接拒绝构造。如实断言「构造被整体拒绝」这一
  // fail-closed 形态，不把它伪称为「观察到缺失 Current」。
  for (const ruleName of ['Structural', 'Journal']) {
    const root = mkdtempSync(join(tmpdir(), `wxs-integrator-without-${ruleName.toLowerCase()}-`))
    const commonDir = join(root, '.Herdchestra')
    mkdirSync(commonDir, { recursive: true })
    try {
      assert.throws(
        () => workspace.createIsolatedWithoutRegistration(commonDir, `without-${ruleName.toLowerCase()}`, ruleName),
        (error) => {
          const text = String((error && error.message) || error)
          assert.ok(
            text.includes(`missing required rule '${ruleName}'`),
            `expected a fail-closed construction refusal naming ${ruleName}, got: ${text}`,
          )
          return true
        },
        `removing ${ruleName} must fail closed at construction`,
      )
    } finally {
      rmSync(root, { recursive: true, force: true })
    }
  }

  // 三个域注册的隔离变异：变体程序来自真实 hostProgram 移除恰好一项、
  // 其余注册（含 Sphinx）保持生产顺序；每个变体落在自己的临时目录，
  // 不碰共享工作区。live fact 仍按已知词汇落盘，对应业务 Current 被阻止，
  // 保留注册的 Structural 结果不受牵连。
  const isolatedStore = (ruleName, writerId, root) => {
    const commonDir = join(root, '.Herdchestra')
    mkdirSync(commonDir, { recursive: true })
    return workspace.createIsolatedWithoutRegistration(commonDir, writerId, ruleName)
  }


  {
    const root = mkdtempSync(join(tmpdir(), 'wxs-integrator-without-casebook-'))
    try {
      const store = isolatedStore('Casebook', 'without-casebook', root)
      try {
        mustOk(await eventStore.append(store, [structuralEvent]), 'append Structural fact with Casebook removed')
        mustOk(
          await casebook.archive(store, {
            sessionId: 'isolated-case',
            q: 'Q',
            a: 'A',
            observations: [],
            lastAccessOrder: 0,
          }),
          'append Casebook fact with rule removed',
        )
        const fetched = mustOk(
          await casebook.fetchCase(store, 10, 'isolated-case'),
          'read Casebook Current with rule removed',
        )
        assert.equal(fetched.value, null, 'the Casebook Current no longer produces the case')
        assert.equal(
          eventStore.head(store, structuralEvent.stream),
          structuralEvent.id,
          'the kept Structural registration still folds its live fact',
        )
      } finally {
        eventStore.dispose(store)
      }
    } finally {
      rmSync(root, { recursive: true, force: true })
    }
  }

  {
    const root = mkdtempSync(join(tmpdir(), 'wxs-integrator-without-jstransaction-'))
    try {
      const store = isolatedStore('JsTransaction', 'without-jstransaction', root)
      try {
        mustOk(await eventStore.append(store, [structuralEvent]), 'append Structural fact with JsTransaction removed')
        mustOk(
          await transaction.appendPrepared(store, {
            transactionId: 'isolated-transaction',
            workspaceRoot: '/isolated-workspace',
            mutation: { path: 'a.txt', originalText: 'before', newText: 'after' },
          }),
          'append JsTransaction fact with rule removed',
        )
        assert.deepEqual(
          transaction.pending(store).map(({ transactionId }) => transactionId),
          [],
          'the JsTransaction Current no longer lists the pending transaction',
        )
        assert.equal(
          eventStore.head(store, structuralEvent.stream),
          structuralEvent.id,
          'the kept Structural registration still folds its live fact',
        )
      } finally {
        eventStore.dispose(store)
      }
    } finally {
      rmSync(root, { recursive: true, force: true })
    }
  }
})
}


