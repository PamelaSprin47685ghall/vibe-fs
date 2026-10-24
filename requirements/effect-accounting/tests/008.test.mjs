import test from 'node:test'
import assert from 'node:assert/strict'
import * as journalSurface from '../../../dist/Persistence/Journal/Surface.js'
import { acceptAuthorityRoot, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'
import { countFactCase } from '../../verification-system/tests/e2e/support/journal-observer.js'
import { readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'

const countCheckpointFacts = (directory) => {
  const eventsDir = join(directory, '.Herdchestra', 'wanxiangshu', 'events')
  let count = 0
  for (const file of readdirSync(eventsDir).filter((file) => file.endsWith('.ndjson'))) {
    const content = readFileSync(join(eventsDir, file), 'utf8')
    const events = content.split('\n').filter(Boolean).map((line) => JSON.parse(line))
    count += countFactCase(events, 'ViewCheckpointCommitted')
  }
  return count
}

integrationTest('WHAT[effect-accounting-008] programming surface with view commits its compression checkpoint only after execution succeeds', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    const sessionID = 'effect-008-todowrite'
    const callID = 'effect-008-todo-1'
    await acceptAuthorityRoot(runtime, sessionID, 'engineer')

    const beforeOutput = {
      args: {
        program: 'class Js extends JsProgram { async run() { return {}; } }',
      },
    }

    await hooks['tool.execute.before']({ tool: 'todowrite-for-engineer', sessionID, callID }, beforeOutput)
    assert.deepEqual(
      beforeOutput.args,
      { program: 'class Js extends JsProgram { async run() { return {}; } }' },
      'plugin must not rewrite raw provider arguments on todowrite-for-*',
    )

    const beforeSnapshot = journalSurface.JournalSurface_snapshot(runtime.journal)
    assert.equal(beforeSnapshot.viewCheckpoints, undefined, 'before admission must not commit a checkpoint')

    await hooks['tool.execute.after'](
      { tool: 'todowrite-for-engineer', sessionID, callID, args: beforeOutput.args },
      { title: 'todowrite-for-engineer', output: 'ok', metadata: {} },
    )

    assert.equal(
      journalSurface.JournalSurface_snapshot(runtime.journal).viewCheckpoints,
      undefined,
      'after must not commit a checkpoint',
    )

    await hooks.event({
      event: {
        type: 'message.part.updated',
        properties: {
          sessionID,
          part: {
            type: 'tool',
            tool: 'todowrite-for-engineer',
            callID,
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

    const afterSnapshot = journalSurface.JournalSurface_snapshot(runtime.journal)
    assert.equal(afterSnapshot.viewCheckpoints, undefined, 'completed programming surface call commits no checkpoint')
    assert.equal(countCheckpointFacts(directory), 0, 'no ViewCheckpointCommitted fact is emitted')
  })
})

integrationTest('WHAT[effect-accounting-008] programming surface failure does not commit a compression checkpoint', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    const sessionID = 'effect-008-todowrite-failed'
    const callID = 'effect-008-todo-failed-1'
    await acceptAuthorityRoot(runtime, sessionID, 'engineer')

    const beforeOutput = {
      args: {
        program: 'class Js extends JsProgram { async run() { return {}; } }',
      },
    }

    await hooks['tool.execute.before']({ tool: 'todowrite-for-engineer', sessionID, callID }, beforeOutput)

    await hooks['tool.execute.after'](
      { tool: 'todowrite-for-engineer', sessionID, callID, args: beforeOutput.args },
      { title: 'todowrite-for-engineer', output: 'failure', metadata: {} },
    )

    await hooks.event({
      event: {
        type: 'message.part.updated',
        properties: {
          sessionID,
          part: {
            type: 'tool',
            tool: 'todowrite-for-engineer',
            callID,
            state: {
              status: 'error',
              input: {
                program: 'class Js extends JsProgram { async run() { return {}; } }',
              },
            },
          },
        },
      },
    })

    assert.equal(journalSurface.JournalSurface_snapshot(runtime.journal).viewCheckpoints, undefined)
    assert.equal(countCheckpointFacts(directory), 0)
  })
})


