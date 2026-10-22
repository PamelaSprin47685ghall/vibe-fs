// Fails when the checked-in TS mirror (ports.ts) drifts from the F# port signatures.
//
// The mirror is GENERATED: gen-ports.mjs derives it from
// src/Wanxiangshu/Host/Ports/*.fsi plus the two foundation signature files.
// This check regenerates it and compares byte-for-byte, so ANY drift is caught:
// a renamed field, a changed field type, a lost `option`, a missing or extra
// union case, a changed member signature, or a TS-only extra that is not
// produced by the generator.
//
// It does not merely compare type or member NAMES — that check could not see
// `ToolCallRecord.State`, the `HostFailure` field shapes, or `SendOutcome`
// being reduced to a bare string.
import { readFileSync } from 'node:fs'
import { resolve, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import { generate, GENERATED_PATH } from './gen-ports.mjs'

const here = dirname(fileURLToPath(import.meta.url))
const checkedIn = resolve(here, 'ports.ts')

const expected = generate()
const actual = readFileSync(checkedIn, 'utf8')

if (actual === expected) {
  console.log('mirror: ports.ts is byte-identical to the generated mirror of the F# port signatures')
  process.exit(0)
}

const expectedLines = expected.split('\n')
const actualLines = actual.split('\n')
const issues = []
const max = Math.max(expectedLines.length, actualLines.length)
for (let i = 0; i < max; i++) {
  if (expectedLines[i] !== actualLines[i]) {
    issues.push(`line ${i + 1}:\n  expected: ${expectedLines[i] ?? '<missing>'}\n  actual:   ${actualLines[i] ?? '<missing>'}`)
    if (issues.length >= 20) { issues.push('… further differences suppressed'); break }
  }
}
console.log(issues.join('\n'))
console.log(`mirror: ${issues.length} difference(s) between ports.ts and the generated mirror`)
console.log('Fix: regenerate with `node proposals/host-neutral-interface/adapters/omp/gen-ports.mjs --write`')
process.exit(1)
