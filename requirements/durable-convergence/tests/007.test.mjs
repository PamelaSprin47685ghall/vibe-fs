import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { mkdirSync, mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const eventStore = await import("../../../dist/Persistence/EventStore/Surface.js");
const casebook = await import("../../../dist/Repository/Knowledge/Casebook/Surface.js");
const transaction = await import("../../../dist/Repository/Programming/Js/TransactionSurface.js");
const strength = await import("../../../dist/Strength/Surface.js");

const structuralEvent = {
  id: '7'.repeat(40),
  stream: 'integrator-parity/proof',
  type: 'JobRequested',
  parents: [],
  payload: { proof: 'replayed' },
  payloadRefs: [],
}
const mustOk = (result, label) => {
  assert.equal(result.ok, true, `${label}: ${JSON.stringify(result.error)}`)
  return result
}
const observe = async (store) => {
  const caseResult = mustOk(await casebook.fetchCase(store, 10, 'parity-case'), 'read Casebook Current')

  return {
    structuralHead: eventStore.head(store, structuralEvent.stream),
    structuralPayload: eventStore.read(store, structuralEvent.id)?.payload?.proof ?? null,
    strengthDecision: strength.projectionDecisionForTarget('parity-target', strength.storeCurrent(store)),
    caseAnswer: caseResult.value?.a ?? null,
    pendingTransactions: transaction.pending(store).map(({ transactionId }) => transactionId).sort(),
  }
}

test('WHAT[DURABLE-CONVERGENCE-007] retained rich history rebuilds the exact live production Current', async () => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-integrator-parity-'))
  const commonDir = join(root, '.git')
  mkdirSync(commonDir, { recursive: true })

  try {
    const liveStore = eventStore.create(commonDir, 'parity-live')
    let live

    try {
      mustOk(await eventStore.append(liveStore, [structuralEvent]), 'append Structural fact')
      const payload = mustOk(
        await strength.storeWritePayload(liveStore, new TextEncoder().encode('parity strength material')),
        'write Strength payload',
      )
      mustOk(
        await strength.storeAppend(
          liveStore,
          (text) => `parity-hash(${text})`,
          strength.eventPrepared(
            'parity-owner',
            'parity-decision',
            'parity-target',
            'parity-replica',
            'K1',
            'parity-anchor',
            'parity-frame',
            24,
            [payload.value],
          ),
        ),
        'append Strength fact',
      )
      mustOk(
        await casebook.archive(liveStore, {
          sessionId: 'parity-case',
          q: 'Q',
          a: 'replayed answer',
          observations: [],
          lastAccessOrder: 0,
        }),
        'append Casebook fact',
      )
      mustOk(
        await transaction.appendPrepared(liveStore, {
          transactionId: 'parity-transaction',
          workspaceRoot: '/parity-workspace',
          mutations: [{ path: 'a.txt', originalText: 'before', newText: 'after' }],
        }),
        'append JsTransaction fact',
      )
      live = await observe(liveStore)
    } finally {
      eventStore.dispose(liveStore)
    }

    assert.deepEqual(live, {
      structuralHead: structuralEvent.id,
      structuralPayload: 'replayed',
      strengthDecision: 'parity-decision',
      caseAnswer: 'replayed answer',
      pendingTransactions: ['parity-transaction'],
    })

    const replayStore = eventStore.create(commonDir, 'parity-replay')
    try {
      assert.deepEqual(await observe(replayStore), live)
    } finally {
      eventStore.dispose(replayStore)
    }
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { execFileSync } = await import("node:child_process");
const { existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } = await import("node:fs");
const { readFile } = await import("node:fs/promises");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const eventStore = await import("../../../dist/Persistence/EventStore/Surface.js");
const retention = await import("../../../dist/Persistence/EventStore/RetentionSurface.js");

const read = (relative) => readFile(new URL(`../../../${relative}`, import.meta.url), 'utf8')
const make = (id, stream, parents = []) => ({ id, stream, type: 'JobRequested', parents, payload: {}, payloadRefs: [] })

test('WHAT[DURABLE-CONVERGENCE-007] sync does not integrate business history', async () => {
  const source = await read('src/Wanxiangshu/Persistence/EventStore/WriterStreamSync.fs')
  assert.doesNotMatch(source, /StrengthProjection|CasebookProjection|AgentProjection|MagicTodo|JsTransactionPrepared/)
  assert.doesNotMatch(source, /Fold\.apply|StrengthProjection\.fold|CasebookProjection\.fold/)
})
}
