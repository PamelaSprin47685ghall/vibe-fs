// DURABLE-CONVERGENCE-009: the remote is a fully dumb Git remote — only
// objects/refs/fetch/push/lease/CAS/authentication, no Event/Projection/Wanxiang
// domain. Sync intelligence lives entirely in the client (hook process).
// Contract test: source-level proof that the remote fixture and the client
// gateway contain no server-side merge / receive-side reducer / domain API.
// (Behavioral end-to-end proof lives in tests/integration/persist/dumb-server.test.mjs.)

import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

const read = (relative) => readFile(new URL(`../../../${relative}`, import.meta.url), 'utf8')

test('WHAT[DURABLE-CONVERGENCE-009] dumb remote fixture has no Wanxiang domain or server-side logic', async () => {
  const remote = await read('requirements/verification-system/tests/support/dumb-remote.mjs')
  assert.doesNotMatch(remote, /dist\/Domain|CanonicalIntegrator|Projection|WriterStreamSync|HookSync/,
    'remote fixture must stay a dumb Git remote: no Event/Projection/Wanxiang domain')
  assert.match(remote, /git/, 'remote fixture speaks plain Git')
  assert.doesNotMatch(remote, /pre-receive|post-receive|serverSide|receive\.hook/i,
    'no server-side merge, pre-receive reducer or post-receive projection')

  const gateway = await read('src/Wanxiangshu/Git/Gateway.fs')
  assert.match(gateway, /WriterStreamSync\.syncWriterStreams/,
    'all sync intelligence lives in the client gateway')
  assert.doesNotMatch(gateway, /pre-receive|post-receive/i,
    'client gateway must not install server-side receive hooks')
})
