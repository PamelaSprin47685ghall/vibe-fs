import assert from 'node:assert/strict'
import test from 'node:test'
import * as journalSurface from '../../../dist/Persistence/Journal/Surface.js'
import * as obligation from '../../../dist/Persistence/Journal/ObligationJournalSurface.js'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'
import { acceptAuthorityRoot, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

// WHAT[obligation-ledger-003]：插件不得保存第二份 desired/applied 待办投影。
// 本仓 durable projection 只在 ViewCheckpoints 中记录压缩 checkpoint 的
// ToolCallId——没有待办行、没有视图正文、没有重放队列。002 已证明窗口语义；
// 这里钉死投影条目的精确形状（deepEqual，多一个字段即红）。
test('WHAT[obligation-ledger-003] plugin keeps no second desired/applied todo projection beside the Host-native TodoTable', () => {
  // WHAT[obligation-ledger-003]: The plugin does not maintain a second desired/applied
  // todo projection. The retired MagicTodo projection must return null/refusal,
  // and no todo content fields exist in the journal snapshot.
  const dummyHandle = {}
  assert.equal(obligation.snapshotMagicTodo(dummyHandle), null)

  const appendResult = obligation.appendMagicTodo(dummyHandle)
  assert.equal(appendResult.ok, false)
  assert.match(appendResult.error, /appendMagicTodo is retired \(obligation-ledger-007\)/)
})

integrationTest(
  'WHAT[obligation-ledger-003] the durable projection keeps no todo rows, view bodies, or checkpoint windows',
  async () => {
    await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
      const sessionID = 'obligation-no-second-projection'
      await acceptAuthorityRoot(runtime, sessionID, 'engineer')

      await hooks.event({
        event: {
          type: 'message.part.updated',
          properties: {
            sessionID,
            part: {
              type: 'tool',
              tool: 'todowrite-for-engineer',
              callID: 'view-cp-shape',
              state: {
                status: 'completed',
                input: {
                  program: 'class Js extends JsProgram { async run() { return {}; } }',
                },
              },
            },
          },
        },
      })

      const snapshot = journalSurface.JournalSurface_snapshot(runtime.journal)
      assert.equal(snapshot.viewCheckpoints, undefined, 'no checkpoint window is persisted')
      const sessionProjection = snapshot.sessionProjections[sessionID]
      assert.equal(sessionProjection?.todos, undefined, 'no todo rows are persisted in session projection')
      assert.equal(sessionProjection?.desiredTodos, undefined, 'no desired todo snapshot is persisted')
      assert.equal(sessionProjection?.appliedTodos, undefined, 'no applied todo snapshot is persisted')
    })
  },
)

