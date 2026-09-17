import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { existsSync, readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { join } from 'node:path'
import test from 'node:test'
import { createBareWorkspace, readRemoteStoreOid, remoteHasObject } from '../../../verification-system/tests/support/dumb-remote.mjs'
import * as eventStore from '../../../../dist/Persistence/EventStore/Surface.js'

const runner = fileURLToPath(new URL('../../../../resources/git/wanxiang-hook.mjs', import.meta.url))

const event = (id, writer) => ({
  id,
  stream: 'dumb/remote',
  type: 'JobRequested',
  parents: [],
  payload: { writer },
  payloadRefs: [],
})

const open = (repo, writerId) => eventStore.create(join(repo, '.git'), writerId)

const append = async (handle, value) => {
  const result = await eventStore.append(handle, [value])
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
}

const hook = (repo, kind = 'pre-push', arg = 'origin', input = '') => spawnSync(
  process.execPath,
  [runner, kind, arg],
  { cwd: repo, input, encoding: 'utf8', env: { ...process.env, WANXIANG_GIT_SYNC_ACTIVE: '' } },
)

const assertHookOk = (result) => {
  assert.equal(result.status, 0, `hook failed: ${result.stderr || result.stdout}`)
}

test('WHAT[DURABLE-CONVERGENCE-008] reference_transaction_is_also_full_bidirectional_convergence', async () => {
  const source = readFileSync(new URL('../../../../src/Wanxiangshu/Git/Hook/Sync.fs', import.meta.url), 'utf8')
  assert.match(source, /runReferenceTransaction/)
  assert.match(source, /converge remote observed/)
  assert.doesNotMatch(source, /downloadOnly|importOnly|ConvergeObserved/)
})
