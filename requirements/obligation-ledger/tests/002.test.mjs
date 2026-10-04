import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'
import { acceptAuthorityRoot, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

const here = path.dirname(fileURLToPath(import.meta.url))
const installedTodoCanaryRunner = path.join(here, 'support/run-native-todo-replacement-canary.mjs')

integrationTest('WHAT[obligation-ledger-002] empty and duplicate native todo lists pass through unchanged', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'obligation-native-replacement'
    await acceptAuthorityRoot(runtime, sessionID, 'engineer')

    const examples = [
      [],
      [
        { content: '重复内容', status: 'pending', priority: 'low' },
        { content: '重复内容', status: 'completed', priority: 'high' },
      ],
    ]

    for (let index = 0; index < examples.length; index += 1) {
      const todos = examples[index]
      const args = { todos }
      await hooks['tool.execute.before'](
        { tool: 'todowrite', sessionID, callID: 'todo-native-' + index },
        { args },
      )
      assert.equal(args.todos, todos, 'the executor receives the same array object')
      assert.deepEqual(args.todos, todos)
    }
  })
})

// B5: the runner boots the installed OpenCode Host (`opencode serve`), loads
// the production wanxiangshu plugin, drives real todowrite calls through a
// strict mock provider, and reads the TodoTable back through the public SDK
// todo endpoint. A non-zero exit or a version drift fails here.
const runInstalledTodoCanary = () => {
  const launched = spawnSync(process.execPath, [installedTodoCanaryRunner], {
    cwd: path.resolve(here, '../../..'),
    encoding: 'utf8',
  })
  assert.equal(launched.status, 0, launched.stderr || launched.stdout)
  return JSON.parse(launched.stdout)
}

integrationTest('WHAT[obligation-ledger-002] installed OpenCode native executor replaces only the current session TodoTable; an empty todowrite is a Host no-op', () => {
  const evidence = runInstalledTodoCanary()

  // B5 version fence — the compatibility baseline (host-provider-failure-ownership-007).
  assert.equal(evidence.versions.opencode, '1.18.29', `OpenCode version drifted: ${evidence.versions.opencode}`)
  assert.equal(evidence.versions.plugin, '1.18.29', `plugin version drifted: ${evidence.versions.plugin}`)

  const submittedA = evidence.submitted.sessionA
  const submittedB = evidence.submitted.sessionB

  // The public SDK todo endpoint answers Host-owned rows. Calibrated against
  // the real 1.18.29 endpoint: each row carries exactly content/status/priority
  // (the model's submitted values — no Host-issued id field exists on this
  // endpoint version), and those three are the exact submitted array the
  // oracle compares.
  const hostRows = (todos, label) => {
    assert.ok(Array.isArray(todos), `${label}: the public todo endpoint must answer an array`)
    return todos.map(({ content, status, priority }) => ({ content, status, priority }))
  }

  // Positive: A's full list lands in A's TodoTable exactly as submitted —
  // duplicate rows, Chinese content, multiline text and explicit priorities
  // all survive the plugin chain unchanged.
  assert.deepEqual(
    hostRows(evidence.observed.afterASet.sessionA, 'A after set'),
    submittedA,
    'A\'s TodoTable must equal the exact submitted array',
  )

  // Positive: B's own list lands in B's TodoTable.
  assert.deepEqual(
    hostRows(evidence.observed.afterBSet.sessionB, 'B after set'),
    submittedB,
    'B\'s TodoTable must equal the exact submitted array',
  )

  // Counterexample: B's replacement must not touch A's table.
  assert.deepEqual(
    hostRows(evidence.observed.afterBSet.sessionA, 'A after B set'),
    submittedA,
    'B\'s todowrite must not alter A\'s TodoTable',
  )

  // Host compat fact (calibrated expectation): OpenCode 1.18.29 treats an
  // empty-array todowrite as a no-op — A's table keeps the exact set content.
  // Probe probe-native-todo-clear-no-plugin.mjs settled this with two
  // independent HOST-NO-OP verdicts on 2026-10-04 (with and without the
  // plugin chain: identical behavior). This corrects the previous wrong
  // expectation ("clear"), not the assertion strength: it is still an exact
  // deep-equal against the full submitted array.
  assert.deepEqual(
    hostRows(evidence.observed.afterAClear.sessionA, 'A after clear'),
    submittedA,
    'A\'s empty todowrite is a Host no-op: A\'s TodoTable must keep the exact set content',
  )

  // Settled: the no-op must be stable — no late clear after quiescence.
  assert.deepEqual(
    hostRows(evidence.observed.afterAClearSettled.sessionA, 'A after clear settled'),
    submittedA,
    'A\'s empty todowrite must not clear A\'s TodoTable even after settling',
  )

  // Counterexample: A's empty-array no-op must not touch B's table.
  assert.deepEqual(
    hostRows(evidence.observed.afterAClear.sessionB, 'B after A clear'),
    submittedB,
    'A\'s empty todowrite must not alter B\'s TodoTable',
  )

  console.log(JSON.stringify({
    nativeTodoReplacementCanary: {
      versions: evidence.versions,
      providerRequests: evidence.providerRequests,
    },
  }))
})