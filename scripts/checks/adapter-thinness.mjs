#!/usr/bin/env node
// ⑩/⑪ host-neutral interface gates (HNI-04, proposals/host-neutral-interface).
// ⑩ adapter-thinness (ratchet, baseline scripts/checks/adapter-thinness-baseline.json):
//   a) product-reference: distinct product-domain namespaces (Wanxiangshu.<Domain>… that are not an
//      `….OpenCode` adapter namespace) opened or qualified by each adapter file. An adapter that needs them
//      is taking product decisions instead of calling a neutral port / ProductHooks.
//   b) without-port: adapter files that reference no host-neutral port type (Wanxiangshu.Host.<family>).
//   Totals may only go down; a listed file may not exceed its baseline. Unlisted files may carry counts only
//   while the total stays within the baseline (moves/splits), so parallel lanes never need to edit the baseline.
//   Improvements do not fail; the arch lane tightens the baseline (`--write-baseline`).
// ⑪ port-purity (strict): the port family files under Host/ reference no adapter namespace and no
//   product domain — only Foundation, Wanxiangshu.Host and System.
// Usage: node scripts/checks/adapter-thinness.mjs [--report] [--write-baseline]

import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import { join, relative } from 'node:path'
import { fileURLToPath } from 'node:url'
import { walk } from '../lib/walk.mjs'

const PRODUCTION_ROOT = 'src/Wanxiangshu'
export const THINNESS_BASELINE = 'scripts/checks/adapter-thinness-baseline.json'
export const PORT_FAMILIES = ['Vocabulary', 'Diagnostics', 'Storage', 'ModelRouting', 'Sessions', 'Prompts', 'Transcript',
  'Tools', 'ToolGates', 'Signals', 'TurnTransforms', 'Compaction', 'Lifecycle']
export const PRODUCT_DOMAINS = ['Ablation', 'Change', 'Composition', 'Context', 'Execution', 'Git', 'Interaction',
  'Mission', 'Participant', 'Persistence', 'Process', 'Repository', 'Sphinx', 'Verification']

const norm = (path) => path.replace(/\\/g, '/')
export const isPortFile = (file) => PORT_FAMILIES.some((f) => file === `${PRODUCTION_ROOT}/Host/Ports/${f}.fs`)
export const isAdapterFile = (file) =>
  file.endsWith('.fs') && (file.startsWith(`${PRODUCTION_ROOT}/OpenCode/`) || file.includes('/OpenCode/') || file.startsWith(`${PRODUCTION_ROOT}/Omp/`))
const code = (text) => text.split('\n').filter((line) => !/^\s*\/\//.test(line)).join('\n')

const PRODUCT_REF = new RegExp(`\\bWanxiangshu\\.(?:${PRODUCT_DOMAINS.join('|')})(?:\\.[A-Z]\\w*)*`, 'g')
export function productReferences(text) {
  const found = new Set()
  for (const match of code(text).matchAll(PRODUCT_REF)) {
    const ns = match[0]
    if (/\.OpenCode(?:\.|$)/.test(ns) || /\.Omp(?:\.|$)/.test(ns)) continue // adapter-to-adapter
    found.add(ns)
  }
  return [...found].sort()
}

export function portTypeNames(read) {
  const names = new Set()
  for (const family of PORT_FAMILIES) {
    const file = `${PRODUCTION_ROOT}/Host/Ports/${family}.fsi`
    if (!existsSync(file)) continue
    for (const m of read(file).matchAll(/^type\s+(\w+)/gm)) names.add(m[1])
  }
  return names
}

// A port type name also declared outside the port families (e.g. an older `ToolResult`) only counts when
// qualified as `Host.<Name>`; distinctive names count once the file opens or qualifies Wanxiangshu.Host.
export function distinctivePortNames(names, productionFs, read) {
  const elsewhere = new Set()
  for (const file of productionFs) {
    if (isPortFile(file)) continue
    const text = read(file)
    for (const m of text.matchAll(/^\s*(?:and|type)\s+(?:private\s+)?(\w+)/gm)) elsewhere.add(m[1])
    for (const m of text.matchAll(/^\s*\|\s*([A-Z]\w*)\b/gm)) elsewhere.add(m[1]) // union cases
  }
  return new Set([...names].filter((n) => !elsewhere.has(n)))
}

export function referencesPort(text, names, distinctive = names) {
  const body = code(text)
  if (!/\bWanxiangshu\.Host\b/.test(body)) return false
  if ([...body.matchAll(/\b(?:Host|Ports)\.([A-Z]\w*)/g)].some((m) => names.has(m[1]))) return true
  return [...body.matchAll(/\b[A-Z]\w*\b/g)].some((m) => distinctive.has(m[0]))
}

export const readThinnessBaseline = () =>
  existsSync(THINNESS_BASELINE) ? JSON.parse(readFileSync(THINNESS_BASELINE, 'utf8')) : null

export function measure(productionFs, read) {
  const names = portTypeNames(read)
  const distinctive = distinctivePortNames(names, productionFs, read)
  const adapters = productionFs.filter(isAdapterFile).sort()
  const productReference = {}
  const withoutPort = []
  let lines = 0
  for (const file of adapters) {
    const text = read(file)
    lines += text.split('\n').length
    const refs = productReferences(text)
    if (refs.length > 0) productReference[file] = refs.length
    if (!referencesPort(text, names, distinctive)) withoutPort.push(file)
  }
  const total = Object.values(productReference).reduce((a, b) => a + b, 0)
  return { adapterFiles: adapters.length, adapterLines: lines, productReference, productReferenceTotal: total, withoutPort }
}

export function scanAdapterThinness(productionFiles, read) {
  const violations = []
  const fail = (gate, message) => violations.push({ gate, message })
  const productionFs = productionFiles.map(norm).filter((f) => f.endsWith('.fs'))

  // ⑪ port purity
  for (const file of productionFs.filter(isPortFile)) {
    const body = code(read(file))
    const bad = [...body.matchAll(/\bWanxiangshu\.([A-Z]\w*)/g)].map((m) => m[1]).filter((ns) => ns !== 'Foundation' && ns !== 'Host')
    if (/\bOpenCode\b|\bOpencode\w*|\bomp\b/i.test(body.replace(/\/\/\/.*$/gm, ''))) bad.push('host vocabulary')
    if (bad.length) fail('port-purity', `${file}: neutral port references ${[...new Set(bad)].join(', ')}`)
  }

  // ⑩ adapter thinness ratchet
  const baseline = readThinnessBaseline()
  if (!baseline) { fail('adapter-thinness', `${THINNESS_BASELINE} missing`); return { violations } }
  const now = measure(productionFs, read)
  const base = baseline.productReference ?? {}
  const baseTotal = Object.values(base).reduce((a, b) => a + b, 0)
  if (now.productReferenceTotal > baseTotal) {
    const grown = Object.entries(now.productReference).filter(([f, n]) => n > (base[f] ?? 0)).map(([f, n]) => `${f} ${base[f] ?? 0}->${n}`)
    fail('adapter-thinness', `product-domain references in adapter files rose ${baseTotal} -> ${now.productReferenceTotal}: ${grown.join('; ')}`)
  }
  for (const [file, n] of Object.entries(now.productReference)) {
    if (file in base && n > base[file]) fail('adapter-thinness', `${file}: product-domain references rose ${base[file]} -> ${n}`)
  }
  const baseWithout = baseline.withoutPort ?? []
  if (now.withoutPort.length > baseWithout.length) {
    const added = now.withoutPort.filter((f) => !baseWithout.includes(f))
    fail('adapter-thinness', `adapter files without a host-neutral port rose ${baseWithout.length} -> ${now.withoutPort.length}: ${added.join(', ')}`)
  }
  return { violations, measurement: now, baselineTotals: { productReference: baseTotal, withoutPort: baseWithout.length } }
}

function runCli() {
  const root = process.cwd()
  const files = walk(join(root, PRODUCTION_ROOT), ['.fs', '.fsi']).map((p) => norm(relative(root, p)))
  const read = (p) => readFileSync(p, 'utf8')
  const { violations, measurement, baselineTotals } = scanAdapterThinness(files, read)
  const m = measurement ?? measure(files.filter((f) => f.endsWith('.fs')), read)
  if (process.argv.includes('--write-baseline')) {
    const prior = readThinnessBaseline()
    writeFileSync(THINNESS_BASELINE, JSON.stringify({
      _note: prior?._note ?? 'HNI-04 gate 10 (adapter-thinness). Totals only go down; tighten with --write-baseline.',
      productReference: m.productReference, withoutPort: m.withoutPort,
    }, null, 2) + '\n')
    console.log(`baseline written: product-reference ${m.productReferenceTotal}, without-port ${m.withoutPort.length}`)
  }
  console.log(`adapter-thinness: ${m.adapterFiles} adapter files, ${m.adapterLines} lines; product-reference ${m.productReferenceTotal}` +
    `${baselineTotals ? ` (baseline ${baselineTotals.productReference})` : ''}; without-port ${m.withoutPort.length}` +
    `${baselineTotals ? ` (baseline ${baselineTotals.withoutPort})` : ''}`)
  if (process.argv.includes('--report')) {
    for (const [f, n] of Object.entries(m.productReference).sort((a, b) => b[1] - a[1])) console.log(`  ${n}\t${f}`)
  }
  for (const v of violations) console.error(`${v.gate}: ${v.message}`)
  process.exit(violations.length ? 1 : 0)
}

const isEntry = process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]
if (isEntry) runCli()
