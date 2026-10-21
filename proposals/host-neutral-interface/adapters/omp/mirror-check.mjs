// Fails when the TS mirror (ports.ts) drifts from the F# ports (src/Wanxiangshu/Host/Ports/<family>.fsi).
// Checks: every F# type exists in TS; record fields, interface members and union cases match by name.
import { readFileSync } from 'node:fs'
import { resolve, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const hostDir = resolve(here, '../../../../src/Wanxiangshu/Host/Ports')
export const FAMILIES = ['Vocabulary', 'Diagnostics', 'Storage', 'ModelRouting', 'Sessions', 'Prompts', 'Transcript',
  'Tools', 'ToolGates', 'Signals', 'TurnTransforms', 'Compaction', 'Lifecycle']

function parseFsi(text) {
  const types = new Map()
  let cur = null
  for (const raw of text.split('\n')) {
    const line = raw.replace(/\/\/.*$/, '')
    const t = line.match(/^type\s+(\w+)(?:<[^>]+>)?\s*=\s*(.*)$/)
    if (t) {
      cur = { name: t[1], names: new Set(), kind: 'alias' }
      types.set(t[1], cur)
      const rest = t[2]
      if (rest.startsWith('{')) cur.kind = 'record'
      const f = rest.match(/^\{\s*(\w+)\s*:/); if (f) cur.names.add(f[1])
      const singleCase = rest.match(/^(\w+) of /); if (singleCase && singleCase[1] === t[1]) cur.kind = 'wrapper'
      continue
    }
    if (!cur || /^\S/.test(line)) { if (/^\S/.test(line) && !line.startsWith('[<')) cur = null; continue }
    let m
    if ((m = line.match(/^\s+\|\s*(\w+)/))) { cur.kind = 'union'; cur.names.add(m[1]) }
    else if ((m = line.match(/^\s+abstract\s+(\w+)\s*:/))) { cur.kind = 'interface'; cur.names.add(m[1]) }
    else if ((m = line.match(/^\s+\{?\s*(\w+)\s*:/))) { cur.kind = 'record'; cur.names.add(m[1]) }
  }
  return types
}

function tsBlock(ts, name) {
  const re = new RegExp(`export (?:interface|type) ${name}\\b[^\\n]*(?:\\n(?!export )[^\\n]*)*`)
  return ts.match(re)?.[0]
}

const ts = readFileSync(resolve(here, 'ports.ts'), 'utf8')
const issues = []
let count = 0
for (const family of FAMILIES) {
  const types = parseFsi(readFileSync(resolve(hostDir, `${family}.fsi`), 'utf8'))
  for (const [name, def] of types) {
    count++
    const block = tsBlock(ts, name)
    if (!block) { issues.push(`${family}.fsi: type ${name} missing in ports.ts`); continue }
    for (const member of def.names) {
      const present = def.kind === 'union'
        ? new RegExp(`["']${member}["']`).test(block)
        : new RegExp(`\\b${member}\\??\\s*[(:]|get ${member}\\b|readonly ${member}\\??:`).test(block)
      if (!present) issues.push(`${family}.fsi: ${name}.${member} (${def.kind}) missing in ports.ts`)
    }
  }
}
if (issues.length) { console.log(issues.join('\n')); console.log(`mirror: ${issues.length} drift(s)`); process.exit(1) }
console.log(`mirror: ${count} F# port types match ports.ts`)
