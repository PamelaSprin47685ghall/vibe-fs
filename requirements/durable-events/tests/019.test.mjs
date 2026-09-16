import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as casebook from '../../../dist/Repository/Knowledge/Casebook/Surface.js'
import * as transaction from '../../../dist/Repository/Programming/Js/TransactionSurface.js'
import * as strength from '../../../dist/Strength/Surface.js'

const id = (n) => n.toString(16).padStart(40, '0')
const hash = (text) => `proof-hash(${text})`

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

test('WHAT[DURABLE-EVENTS-019] every registered business oracle changes its production Current', async () => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-integrator-registration-'))
  const commonDir = join(root, '.git')
  mkdirSync(commonDir, { recursive: true })

  try {
    const store = eventStore.create(commonDir, 'business-registration')

    try {
      mustOk(await eventStore.append(store, [structuralEvent]), 'append Structural fact')

      const payload = mustOk(
        await strength.storeWritePayload(store, new TextEncoder().encode('canonical strength material')),
        'write Strength payload',
      )
      const strengthFact = strength.eventPrepared(
        'canonical-owner',
        'canonical-decision',
        'canonical-target-run',
        'canonical-replica',
        'K1',
        'canonical-anchor',
        'canonical-frame',
        27,
        [payload.value],
      )
      mustOk(await strength.storeAppend(store, hash, strengthFact), 'append Strength fact')

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
          mutations: [{ path: 'a.txt', originalText: 'before', newText: 'after' }],
        }),
        'append JsTransaction fact',
      )

      const fetched = mustOk(await casebook.fetchCase(store, 10, 'canonical-case'), 'read Casebook Current')
      const strengthCurrent = strength.storeCurrent(store)

      assert.deepEqual(
        {
          structuralHead: eventStore.head(store, structuralEvent.stream),
          structuralEvent: eventStore.read(store, structuralEvent.id)?.payload?.proof ?? null,
          strengthDecision: strength.projectionDecisionForTarget('canonical-target-run', strengthCurrent),
          caseAnswer: fetched.value?.a ?? null,
          pendingTransactions: transaction.pending(store).map(({ transactionId }) => transactionId).sort(),
        },
        {
          structuralHead: structuralEvent.id,
          structuralEvent: 'structural',
          strengthDecision: 'canonical-decision',
          caseAnswer: 'A',
          pendingTransactions: ['canonical-transaction'],
        },
      )
    } finally {
      eventStore.dispose(store)
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
            case: 'CompanionBloggerClosed',
            payload: {
              SessionId: 'canonical-session',
            },
          },
        ),
        'append Journal fact',
      )
      assert.equal(journal.JournalSurface_hasSession(booted.journal, 'canonical-session'), true)
    } finally {
      journal.JournalSurface_dispose(booted.journal)
    }
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})
