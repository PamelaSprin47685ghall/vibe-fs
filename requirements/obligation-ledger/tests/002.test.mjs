import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import * as journalSurface from '../../../dist/Persistence/Journal/Surface.js'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'
import { acceptAuthorityRoot, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

const here = path.dirname(fileURLToPath(import.meta.url))
const installedTodoCanaryRunner = path.join(here, 'support/run-native-todo-replacement-canary.mjs')

integrationTest('WHAT[obligation-ledger-002] programming-surface calls commit no checkpoint window or second todo projection across todowrite-for-* roles', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'obligation-view-window'
    await acceptAuthorityRoot(runtime, sessionID, 'engineer')

    const calls = [
      { callID: 'view-cp-1', tool: 'todowrite-for-engineer' },
      { callID: 'view-cp-2', tool: 'todowrite-for-devops' },
      { callID: 'view-cp-3', tool: 'todowrite-for-manager' },
      { callID: 'view-cp-4', tool: 'todowrite-for-plan' },
    ]

    for (const { callID, tool } of calls) {
      await hooks.event({
        event: {
          type: 'message.part.updated',
          properties: {
            sessionID,
            part: {
              type: 'tool',
              tool,
              callID,
              state: {
                status: 'completed',
                input: { program: 'class Js extends JsProgram { async run() { return {}; } }' },
              },
            },
          },
        },
      })
    }

    const snapshot = journalSurface.JournalSurface_snapshot(runtime.journal)
    assert.equal(
      snapshot.viewCheckpoints,
      undefined,
      'no checkpoint window is recorded for any todowrite-for-* role',
    )
    assert.equal(
      snapshot.sessionProjections[sessionID]?.todos,
      undefined,
      'no second todo projection is maintained for the session',
    )
  })
})

const runInstalledTodoCanary = () => {
  const launched = spawnSync(process.execPath, [installedTodoCanaryRunner], {
    cwd: path.resolve(here, '../../..'),
    encoding: 'utf8',
  })
  assert.equal(launched.status, 0, launched.stderr || launched.stdout)
  return JSON.parse(launched.stdout)
}

integrationTest('WHAT[obligation-ledger-002] installed OpenCode Host runs todowrite-for-engineer without native todowrite or checkpoint facts', () => {
  const evidence = runInstalledTodoCanary()

  assert.equal(evidence.versions.opencode, '1.18.29', `OpenCode version drifted: ${evidence.versions.opencode}`)
  assert.equal(evidence.versions.plugin, '1.18.29', `plugin version drifted: ${evidence.versions.plugin}`)
  assert.equal(evidence.observed.todowriteAdvertised, false, 'native todowrite must not be advertised on the Engineer provider wire')
  assert.equal(evidence.observed.programmingToolAdvertised, true, 'todowrite-for-engineer must be advertised on the Engineer provider wire')
  assert.deepEqual(
    evidence.observed.checkpointsBySession,
    {},
    'programming-surface calls emit no ViewCheckpointCommitted facts on any session',
  )
})
