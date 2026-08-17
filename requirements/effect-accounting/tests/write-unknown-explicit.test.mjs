// Disposed EventStore capabilities report an explicit unknown write outcome.
import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtemp } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as store from '../../../dist/Persistence/EventStore/Surface.js'

test('WHAT[EFFECT-ACCOUNTING-006] write_after_dispose_returns_explicit_unknown_not_pretended_commit', async () => {
  const directory = await mkdtemp(join(tmpdir(), 'wxs-effect-unknown-'))
  const handle = store.create(directory, 'writer-unknown')
  const event = {
    id: 'event-unknown-1',
    stream: 'session/ses_006',
    type: 'CompanionBloggerClosed',
    parents: [],
    payload: { SessionId: 'ses_006' },
    payloadRefs: [],
  }

  const healthy = await store.append(handle, [event])
  assert.equal(healthy.ok, true, JSON.stringify(healthy.error ?? ''))
  store.dispose(handle)

  await assert.rejects(
    () => store.append(handle, [event]),
    /disposed|writer|unknown|invalid/i,
  )
})
