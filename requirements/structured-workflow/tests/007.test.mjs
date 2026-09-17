import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'

const ROOT = new URL('../../../', import.meta.url).pathname

const readSrc = (rel) => readFileSync(join(ROOT, rel), 'utf8')

const VOCABULARY_SURFACES = {
  'Mission/Manager/Workflow': ['observe', 'observeIdle'],
  'Participant/Provider/Attempt/Fallback/Ledger': ['recordAuthorizedFailure'],
  'Participant/Provider/Attempt/Fallback/Workflow': ['continueAfterConfirmedFailure'],
  'Change/Program': ['run'],
}

const REJECTED_PREFIX = /^(execute|process|handle|do|retry|run|perform|with)[A-Z]/

test('WHAT[STRUCTURED-WORKFLOW-007] SW_011_named_vocabulary_surface_exists_in_Application', () => {
  for (const [modulePath, names] of Object.entries(VOCABULARY_SURFACES)) {
    const source = readSrc(`src/Wanxiangshu/${modulePath}.fs`)
    for (const name of names) {
      assert.match(
        source,
        new RegExp(`\\blet(?: rec)?(?: private)? ${name}\\b`),
        `${modulePath} must define '${name}' as a let binding`,
      )
    }
  }
})

test('WHAT[STRUCTURED-WORKFLOW-007] SW_011_vocabulary_names_declare_business_promises_not_implementation_actions', () => {
  const bad = []
  for (const [modulePath, names] of Object.entries(VOCABULARY_SURFACES)) {
    for (const name of names) {
      if (REJECTED_PREFIX.test(name)) bad.push(`${modulePath}.${name}`)
    }
  }
  assert.deepEqual(bad, [], 'vocabulary names must not be implementation-action labels')
})

test('WHAT[STRUCTURED-WORKFLOW-007] every vocabulary binds owner_law_relation_and_executable_proof', () => {
  const OBLIGATIONS = [
    ['ManagerWorkflow.observe', 'Mission/Manager/Workflow.fs', 'Mission.Manager'],
    ['ManagerWorkflow.observeIdle', 'Mission/Manager/Workflow.fs', 'Mission.Manager'],
    ['FallbackLedger.recordAuthorizedFailure', 'Participant/Provider/Attempt/Fallback/Ledger.fs', 'Participant.Provider'],
    ['ProviderRecoveryWorkflow.continueAfterConfirmedFailure', 'Participant/Provider/Attempt/Fallback/Workflow.fs', 'Participant.Provider'],
    ['OrchestratorProgram.run', 'Change/Program.fs', 'Change'],
  ]

  const howPath = join(ROOT, 'requirements/structured-workflow/HOW.md')
  const how = readFileSync(howPath, 'utf8')
  const lines = how.split('\n')
  const tableStart = lines.findIndex((line) => line.startsWith('### 3.3 '))
  const tableEnd = lines.findIndex((line) => line.startsWith('### 3.3.1'))
  const rows = lines
    .slice(tableStart + 1, tableEnd)
    .map((text, offset) => ({ text, line: tableStart + offset + 2 }))
    .filter(({ text }) => text.startsWith('| `'))
  assert.equal(rows.length, OBLIGATIONS.length, 'the obligation table must contain exactly the registered vocabulary')

  for (const [vocab, file, owner] of OBLIGATIONS) {
    const row = rows.find(({ text }) => text.includes(`\`${vocab}\``))
    assert.ok(row, `HOW §3.3 must register ${vocab}`)
    const columns = row.text.split('|').map((column) => column.trim()).filter(Boolean)
    assert.equal(columns.length, 5, `${vocab} must bind vocabulary, owner/path, WHAT, relation, proof`)
    assert.ok(columns[1].includes(owner) && columns[1].includes(file), `${vocab} must name exact owner and source`)

    const whatId = columns[2].replaceAll('`', '')
    assert.ok(['STRUCTURED-WORKFLOW-007', 'STRUCTURED-WORKFLOW-008'].includes(whatId), `${vocab} must bind its primary workflow law`)
    assert.ok(columns[3].length > 12, `${vocab} must declare a non-empty trace relation`)

    const short = vocab.split('.').pop()
    const production = readSrc(`src/Wanxiangshu/${file}`)
    assert.match(production, new RegExp(`\\blet(?: rec)?(?: private)? ${short}\\b`), `${vocab} must exist in ${file}`)
  }
})
