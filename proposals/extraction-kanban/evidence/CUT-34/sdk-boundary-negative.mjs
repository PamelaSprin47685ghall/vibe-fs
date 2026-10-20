// Negative controls for architecture gate ⑦ host-sdk-boundary (CUT-34).
// Run from repo root: node proposals/extraction-kanban/evidence/CUT-34/sdk-boundary-negative.mjs
import { readFileSync } from 'node:fs'
import { walk } from '../../../../scripts/lib/walk.mjs'
import { scanArchitecture } from '../../../../scripts/checks/architecture.mjs'

const files = walk('src/Wanxiangshu', ['.fs', '.fsproj'])
const real = (p) => readFileSync(p, 'utf8')
const gate = (read) => scanArchitecture(files, read).violations.filter((v) => v.gate === 'host-sdk-boundary')

const results = []
const run = (name, read, expectRed) => {
  const v = gate(read)
  const red = v.length > 0
  results.push({ name, violations: v.length, ok: red === expectRed })
  console.log(`${red === expectRed ? 'PASS' : 'FAIL'} ${name}: ${v.length} violation(s)${v[0] ? ' e.g. ' + v[0].message : ''}`)
}

run('master tree', real, false)
const kernel = 'src/Wanxiangshu/Mission/Work/Contract.fs'
const plant = (extra) => (p) => (p === kernel ? real(p) + extra : real(p))
run('N1 kernel imports @opencode-ai/sdk', plant('\n// [<Import("createOpencodeClient", "@opencode-ai/sdk")>]\n'), true)
run('N2 kernel reads input?client', plant('\nlet leak (input: obj) = input?client\n'), true)
run('N3 kernel calls client.session', plant('\nlet leak2 client = client.session\n'), true)
run('N4 kernel calls client?session', plant('\nlet leak3 client = client?session?list\n'), true)
// positive control: the real adapter files do match, so excluding the adapter area must turn the master tree red
const adapterHits = files.filter((p) => p.includes('/OpenCode/Host/') && /\?client\b|\bclient\s*(?:\?|\.(?:session|event|app|config|tui|find|file|project|path)\b)/.test(real(p)))
console.log(`${adapterHits.length > 0 ? 'PASS' : 'FAIL'} P1 adapter files really touch the client: ${adapterHits.join(', ')}`)
process.exit(results.every((r) => r.ok) && adapterHits.length > 0 ? 0 : 1)
