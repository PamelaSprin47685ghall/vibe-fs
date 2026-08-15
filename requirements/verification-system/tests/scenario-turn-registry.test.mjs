import assert from 'node:assert/strict'
import test from 'node:test'
import { createScenarioTurn } from './e2e/support/scenario-turn.js'

const fakeEvents = () => ({
  lastSeq: 0,
  all: [],
  async awaitEvent(predicate) {
    const found = this.all.find(predicate)
    if (!found) throw new Error('no matching event')
    return found
  },
})

test('VERIFY_turn_registry_keeps_physical_cursor_identity_per_session', async () => {
  const events = fakeEvents()
  const turns = createScenarioTurn({ events })

  const root = turns.start('root')
  const child = turns.start('child')

  assert.equal(turns.current('root'), root, 'starting child work must not overwrite root turn cursor')
  assert.equal(turns.current('child'), child)

  events.all.push(
    { seq: 1, type: 'message.updated', sessionID: 'root', finishReason: 'stop' },
    { seq: 2, type: 'session.idle', sessionID: 'root' },
  )
  events.lastSeq = 2

  await root.awaitTerminal({ requireAssistantTerminal: false })
  assert.equal(root.activitySeq, 1)
})

test('VERIFY_turn_registry_restart_clear_forgets_all_pre_restart_cursors', () => {
  const turns = createScenarioTurn({ events: fakeEvents() })
  turns.start('root')
  turns.start('child')
  turns.clear()

  assert.equal(turns.current('root'), null)
  assert.equal(turns.current('child'), null)
})
