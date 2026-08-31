import assert from 'node:assert/strict'
import fs from 'node:fs'
import test from 'node:test'

const runbookUrl = new URL('../../managed-chat-execution/OPERATOR-RUNBOOK.md', import.meta.url)
const incidentUrl = new URL('../../managed-chat-execution/fixtures/incidents/agent-028.json', import.meta.url)
const schemaUrl = new URL('../../managed-chat-execution/tests/fixtures/incident-evidence-v1.schema.json', import.meta.url)
const replayToolUrl = new URL('../../managed-chat-execution/tests/support/incident-evidence.mjs', import.meta.url)

const runbook = fs.readFileSync(runbookUrl, 'utf8')
const incident = fs.readFileSync(incidentUrl, 'utf8')

test('WHAT[VERIFICATION-SYSTEM-006] reliability runbook names every safe query, stop condition and evidence boundary', () => {
  for (const anchor of [
    '## Identity conflict',
    '## Accepted nonterminal',
    '## Attempt amplification',
    '## Queue saturation',
    '## Capacity divergence',
    '## Hook criticality',
    '## Safe rollback and canary stop',
    '## Evidence collection',
    '## Decision tree',
    '## Restart or plugin reload',
    '## Mandatory escalation',
  ]) assert.match(runbook, new RegExp(`^${anchor.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}$`, 'm'))

  for (const api of [
    'Surface.fold(serializedFacts)',
    'StatusSurface.queryFacts(serializedFacts, sessionId, physicalUserMessageId)',
    'ModelRoutingSurface.sharedCapacitySnapshot()',
    'reconcileCapacityEvidence(snapshot)',
    'ReliabilityDiagnosticsSurface.queryReliability',
    'HookPolicySurface.js',
    'recoverScenarios([scenario])',
  ]) assert.ok(runbook.includes(api), `missing exact runbook API: ${api}`)

  assert.ok(runbook.includes('node --test requirements/host-boundary/tests/opencode-chat-admission-canary.test.mjs'))
  assert.ok(runbook.includes('node requirements/managed-chat-execution/tests/support/incident-evidence.mjs replay <evidence.json>'))
  assert.equal(fs.existsSync(schemaUrl), true)
  assert.equal(fs.existsSync(replayToolUrl), true)
  assert.equal(fs.existsSync(incidentUrl), true)

  assert.match(runbook, /Never edit journal facts/)
  assert.match(runbook, /never collect.*secret\/token\/cookie\/credential.*stack.*filesystem path/i)
  assert.match(runbook, /does \*\*not\*\* prove an exact accepted-message replay capability/)
  assert.doesNotMatch(incident, /Bearer\s+|api[_-]?key|password|stack trace|\/(?:home|Users)\//i)
  assert.deepEqual(JSON.parse(incident).redaction, {
    payloads: 'removed', credentials: 'removed', stacks: 'removed', paths: 'removed',
  })
})
