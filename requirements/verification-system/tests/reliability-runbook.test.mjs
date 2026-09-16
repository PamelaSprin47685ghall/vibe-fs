import assert from 'node:assert/strict'
import fs from 'node:fs'
import test from 'node:test'

const runbookUrl = new URL('../../managed-chat-execution/OPERATOR-RUNBOOK.md', import.meta.url)
const incidentUrl = new URL('../../managed-chat-execution/fixtures/incidents/agent-028.json', import.meta.url)
const schemaUrl = new URL('../../managed-chat-execution/tests/fixtures/incident-evidence-v1.schema.json', import.meta.url)
const replayToolUrl = new URL('../../managed-chat-execution/tests/support/incident-evidence.mjs', import.meta.url)

const runbook = fs.readFileSync(runbookUrl, 'utf8')
const incident = fs.readFileSync(incidentUrl, 'utf8')

test('WHAT[VERIFICATION-SYSTEM-006] incident fixture carries declared redaction and no secrets', () => {
  assert.equal(fs.existsSync(schemaUrl), true)
  assert.equal(fs.existsSync(replayToolUrl), true)
  assert.equal(fs.existsSync(incidentUrl), true)

  assert.doesNotMatch(incident, /Bearer\s+|api[_-]?key|password|stack trace|\/(?:home|Users)\//i)
  assert.deepEqual(JSON.parse(incident).redaction, {
    payloads: 'removed', credentials: 'removed', stacks: 'removed', paths: 'removed',
  })
})
