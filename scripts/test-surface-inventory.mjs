#!/usr/bin/env node
// Test-surface inventory (P1, TASK.md §1): report-only debt census over
// semantic tests. Never fails — it exists to make the debt a finite set.
//
//   node scripts/test-surface-inventory.mjs
//   node scripts/test-surface-inventory.mjs --package=provider-language
//   node scripts/test-surface-inventory.mjs --json

import { existsSync } from 'node:fs'
import { join, relative } from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  BUILD_VERIFICATION_FILES,
  REQUIREMENTS_ROOT,
  scanAll,
  semanticTestFiles,
} from './lib/test-surface-scan.mjs'

const args = process.argv.slice(2)
const argValue = (flag) => {
  const inline = args.find((a) => a.startsWith(`${flag}=`))
  if (inline) return inline.slice(flag.length + 1)
  const index = args.indexOf(flag)
  return index >= 0 ? args[index + 1] : undefined
}

const pkg = argValue('--package')
const asJson = args.includes('--json')

const all = scanAll()
const entries = Object.entries(all)
const packageNames = [
  ...new Set(
    semanticTestFiles().map((file) => relative(REQUIREMENTS_ROOT, file).replace(/\\/g, '/').split('/')[0]),
  ),
].sort()

const filtered = pkg ? entries.filter(([file]) => file.startsWith(`requirements/${pkg}/`)) : entries

if (asJson) {
  console.log(JSON.stringify({ packages: packageNames, files: filtered.length, debt: Object.fromEntries(filtered) }, null, 2))
  process.exit(0)
}

const byRule = new Map()
for (const [, hits] of filtered) {
  for (const hit of hits) byRule.set(hit.rule, (byRule.get(hit.rule) ?? 0) + 1)
}

const totalFiles = filtered.length
const totalHits = [...byRule.values()].reduce((a, b) => a + b, 0)
const allFiles = entries.length
const allHits = entries.flatMap(([, h]) => h).length

console.log(`test-surface-inventory: ${allFiles} semantic test files carry debt, ${allHits} violating lines`)
console.log(`scope: ${pkg ?? 'all packages'} — ${totalFiles} files, ${totalHits} lines`)
console.log('')
console.log('by rule:')
for (const [rule, count] of [...byRule.entries()].sort((a, b) => b[1] - a[1])) {
  console.log(`  ${rule.padEnd(18)} ${String(count).padStart(5)}`)
}
console.log('')
console.log('by file (top 25):')
const byFile = filtered
  .map(([file, hits]) => [file, hits.length])
  .sort((a, b) => b[1] - a[1])
  .slice(0, 25)
for (const [file, count] of byFile) console.log(`  ${String(count).padStart(4)}  ${file}`)
if (!existsSync(join(REQUIREMENTS_ROOT, 'verification-system'))) {
  console.error('warning: requirements/verification-system missing — inventory root invalid')
}
console.log(`\nexempt build-verification files: ${BUILD_VERIFICATION_FILES.size}`)
