import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
import * as casebook from '../../../dist/Repository/Knowledge/Casebook/Surface.js'
import * as transaction from '../../../dist/Repository/Programming/Js/TransactionSurface.js'
import * as strength from '../../../dist/Strength/Surface.js'

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
