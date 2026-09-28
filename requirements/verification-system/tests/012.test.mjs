import assert from 'node:assert/strict'
import { readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import { checks } from '../../../scripts/check.mjs'

const ROOT = fileURLToPath(new URL('../../..', import.meta.url))

// Known syntactic forms only; semantic gate ownership still requires review.
function mechanicalLineRules(source) {
  const patterns = [
    /\b(?:maxLines|maxFileLines|max_lines|lines\.length\s*>\s*\d+)\b[^\n]*(?:push\(|error|fail)/i,
    /\b(?:line-count-advisory|file-too-long|advisory-line-limit)\b/i,
    /issue[^\n]*(?:file too long|line count exceeds|exceeded max lines)/i,
  ]
  return patterns.filter((pattern) => pattern.test(source)).map(String)
}
const lineCountCommand = (command) => /\b(?:cloc|wc\s+-l|line-count-check)\b/i.test(command)

test('WHAT[verification-system-012] the same detector rejects controlled line gates and permits ordinary measurements', () => {
  for (const source of [
    'if (lines.length > 50) issues.push({ message: "too long" })',
    'if (lines.length > 500) fail()',
    'warn("line-count-advisory")',
    'issues.push("line count exceeds policy")',
  ]) assert.notDeepEqual(mechanicalLineRules(source), [], source)
  for (const source of [
    'return { lineCount: lines.length }',
    'issues.push("invalid import boundary")',
    'if (events.length > 50) fail()',
  ]) assert.deepEqual(mechanicalLineRules(source), [], source)
  assert.equal(lineCountCommand('wc -l src/file.fs'), true)
  assert.equal(lineCountCommand('node scripts/check.mjs'), false)
})

test('WHAT[verification-system-012] registered gates and npm entries contain no known mechanical line rule', () => {
  assert.ok(checks.length > 0, 'the scan must examine actual gates')
  const directory = join(ROOT, 'scripts/checks')
  const files = new Set([
    ...checks,
    ...readdirSync(directory).filter((name) => name.endsWith('.mjs')).map((name) => join(directory, name)),
  ])
  for (const file of files) assert.deepEqual(mechanicalLineRules(readFileSync(file, 'utf8')), [], file)
  const { scripts } = JSON.parse(readFileSync(join(ROOT, 'package.json'), 'utf8'))
  for (const [name, command] of Object.entries(scripts)) assert.equal(lineCountCommand(command), false, name)
})
