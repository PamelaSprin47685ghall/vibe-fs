#!/usr/bin/env node
// ⑩/⑪ host-neutral interface gates (HNI-04, proposals/host-neutral-interface).
//
// ⑩ adapter-thinness (ratchet, baseline scripts/checks/adapter-thinness-baseline.json):
//   a) product-reference: distinct product-domain namespaces (Wanxiangshu.<Domain>… that are not an
//      `….OpenCode` adapter namespace) opened or qualified by each adapter file. An adapter that needs them
//      is taking product decisions instead of calling a neutral port / ProductHooks.
//   b) without-port: adapter files that reference no host-neutral port type (a type declared by a port
//      signature under src/Wanxiangshu/Host/Ports/**).
//   The ratchet may only go DOWN. Totals may not grow, a baselined file may not exceed its baseline, and
//   the number of adapter files without a neutral port may not grow. An adapter file that is NOT in the
//   baseline may carry product-domain references as long as the totals stay within the baseline, so a
//   lane that moves or splits a baselined adapter file never has to edit the baseline. Improvements never
//   fail; the arch lane tightens the baseline (`--write-baseline`, and only on a clean run).
// ⑪ port-purity (strict): every file under src/Wanxiangshu/Host/Ports/** (.fs and .fsi) references no adapter
//   namespace and no product domain — only Foundation, Wanxiangshu.Host and System. The scope is discovered
//   from the sources, so a port family added by another lane (any name) is covered without editing this file.
// ⑫ port-family integrity: every family under Host/Ports/ declares exactly one reserved id in an
//   `HNI P-xx` header marker, no two families claim the same id, and no file directly under Host/ takes
//   the base name of a neutral family. The mirror generator keys on the marker and reads the family by
//   file name, so both are checked here instead of by a failing build.
//
// Honest scope notes (see proposals/host-neutral-interface/evidence/adapter-thinness-gate.md):
//   * The legacy composition seams (P-14 delegation-caller / P-15 delegation-command / P-17 fission port
//     shapes) live in src/Wanxiangshu/Host/*.fs, NOT under Host/Ports/. They are RETAINED legacy, not neutral
//     ports, so they are deliberately outside the strict purity scope; the gate does not pretend they are ports.
//   * src/Wanxiangshu/Host/** outside Host/Ports/ is product/neutral-host code, not adapter code; it is not
//     counted as "adapter" either, so a neutral-host adapter folder convention is not yet covered here.
//
// Usage: node scripts/checks/adapter-thinness.mjs [--report] [--write-baseline]

import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import { join, relative } from 'node:path'
import { fileURLToPath } from 'node:url'
import { walk } from '../lib/walk.mjs'

const PRODUCTION_ROOT = 'src/Wanxiangshu'
export const THINNESS_BASELINE = 'scripts/checks/adapter-thinness-baseline.json'
export const PORT_ROOT = `${PRODUCTION_ROOT}/Host/Ports`
export const PRODUCT_DOMAINS = ['Ablation', 'Change', 'Composition', 'Context', 'Execution', 'Git', 'Interaction',
  'Mission', 'Participant', 'Persistence', 'Process', 'Repository', 'Sphinx', 'Verification']

const norm = (path) => path.replace(/\\/g, '/')

// Any file under the port root is a port file — implementation (.fs) and signature (.fsi) alike. The family
// name is irrelevant: a lane that lands Host/Ports/<AnyName>.fs is in the purity scope and contributes its
// declared types to the neutral-port vocabulary.
export const isPortFile = (file) => {
  const path = norm(file)
  return path.startsWith(`${PORT_ROOT}/`) && (path.endsWith('.fs') || path.endsWith('.fsi'))
}

// Adapter areas: the OpenCode adapter folder, any domain-local `…/OpenCode/` adapter folder, and the Omp
// adapter folder (any location). src/Wanxiangshu/Host/** is NOT an adapter area: it holds neutral host code
// and the retained legacy seams, and the port files themselves live under Host/Ports/.
export const isAdapterFile = (file) => {
  const path = norm(file)
  return path.endsWith('.fs') &&
    (path.startsWith(`${PRODUCTION_ROOT}/OpenCode/`) ||
      path.includes('/OpenCode/') ||
      path.startsWith(`${PRODUCTION_ROOT}/Omp/`) ||
      path.includes('/Omp/'))
}

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

// Signature files that declare the neutral port vocabulary, discovered from the sources.
// The disk fallback is relative to `root` (the tree being scanned), not to this file, so the gate
// also works on a tree other than the repository it lives in.
const portSignatureFiles = (productionFiles, root = process.cwd()) => {
  const list = (productionFiles ?? []).map(norm).filter((f) => isPortFile(f) && f.endsWith('.fsi'))
  if (list.length) return [...new Set(list)].sort()
  const base = join(root, PORT_ROOT)
  if (!existsSync(base)) return []
  return walk(base, ['.fsi']).map((p) => norm(relative(root, p))).sort()
}

export function portTypeNames(read, productionFiles, root) {
  const names = new Set()
  for (const file of portSignatureFiles(productionFiles, root)) {
    // An unreadable port file is a hard error: silently skipping it would shrink the neutral-port
    // vocabulary and hide the file from the scope.
    const text = read(file)
    for (const m of text.matchAll(/^type\s+(\w+)/gm)) names.add(m[1])
  }
  return names
}

// A port type name also declared outside the port files (e.g. an older `ToolResult`) only counts when
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

export function measure(productionFiles, read, root = process.cwd()) {
  const files = (productionFiles ?? []).map(norm)
  const fsFiles = files.filter((f) => f.endsWith('.fs'))
  const names = portTypeNames(read, files, root)
  const distinctive = distinctivePortNames(names, fsFiles, read)
  const adapters = fsFiles.filter(isAdapterFile).sort()
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

// The strict purity scope: every file under the port root, implementation and signature alike.
const purityScope = (productionFiles, root = process.cwd()) => {
  const list = (productionFiles ?? []).map(norm).filter(isPortFile)
  if (list.some((f) => f.endsWith('.fsi'))) return [...new Set(list)].sort()
  const base = join(root, PORT_ROOT)
  if (!existsSync(base)) return [...new Set(list)].sort()
  const onDisk = walk(base, ['.fs', '.fsi']).map((p) => norm(relative(root, p)))
  return [...new Set([...list, ...onDisk])].sort()
}

// ⑫ port-family integrity (HNI-11, drift D-06/D-07): the port families under Host/Ports/ must be
// self-describing, because the TS mirror generator and the file layout both key on them.
//   * every family signature declares exactly one reserved id in an `HNI P-xx` header marker — the
//     generator matches /\bHNI (P-\d{2})\b/ and hard-errors on a family without one;
//   * no two families claim the same reserved id;
//   * a family's file base name is unique across Host/Ports/ and Host/ — a lane file that shares a base
//     name with a neutral family (drift D-07: the tools lane's Host/Tools.fs vs P-04's
//     Host/Ports/Tools.fs) would silently overwrite it the moment the lane moves its file under
//     Host/Ports/ (drift D-05 is the namespace half of the same problem).
// The check reads the sources, so a family another lane lands is covered without editing this file.
export const PORT_FAMILY_ID = /\bHNI (P-\d{2})\b/g
export const PORT_FAMILY_ROOT = `${PRODUCTION_ROOT}/Host`

export function scanPortFamilies(productionFiles, read, options) {
  const violations = []
  const fail = (gate, message) => violations.push({ gate, message })
  const root = options?.root ?? process.cwd()
  const files = (productionFiles ?? []).map(norm).filter((f) => f.endsWith('.fs') || f.endsWith('.fsi'))

  // The family root: every .fs/.fsi under Host/, whether it sits in Host/Ports/ or directly under Host/.
  // A lane file that is still directly under Host/ is NOT a port family yet (it is a domain seam, and
  // HNI-14 moves it), but its base name still must not collide with a neutral family.
  const familyFiles = files.filter((f) => {
    const path = norm(f)
    return path.startsWith(`${PORT_FAMILY_ROOT}/`) && (path.endsWith('.fs') || path.endsWith('.fsi'))
  })
  if (familyFiles.length === 0) {
    const base = join(root, PORT_FAMILY_ROOT)
    if (!existsSync(base)) return { violations }
    familyFiles.push(...walk(base, ['.fs', '.fsi']).map((p) => norm(relative(root, p))))
  }

  // Base names already taken by a neutral family (under Host/Ports/).
  const neutralBases = new Map()
  for (const file of familyFiles) {
    if (!isPortFile(file)) continue
    const base = file.slice(file.lastIndexOf('/') + 1).replace(/\.fsi?$/, '')
    if (!neutralBases.has(base)) neutralBases.set(base, file)
  }

  // A file directly under Host/ must not take a neutral family's base name.
  for (const file of familyFiles) {
    if (isPortFile(file)) continue
    const base = file.slice(file.lastIndexOf('/') + 1).replace(/\.fsi?$/, '')
    const taken = neutralBases.get(base)
    if (taken) {
      fail('port-family', `${file}: base name '${base}' is already used by the neutral family ${taken}; rename before moving it under ${PORT_ROOT}/`)
    }
  }

  // Every neutral family signature declares exactly one reserved id; no id is claimed twice.
  const claims = new Map()
  for (const file of familyFiles) {
    if (!isPortFile(file) || !file.endsWith('.fsi')) continue
    const text = read(file)
    const ids = [...new Set([...text.matchAll(PORT_FAMILY_ID)].map((m) => m[1]))]
    if (ids.length === 0) {
      fail('port-family', `${file}: declares no reserved port id; add an \`HNI P-xx\` marker to its header`)
      continue
    }
    if (ids.length > 1) {
      fail('port-family', `${file}: declares ${ids.length} reserved port ids (${ids.join(', ')}); a family declares exactly one`)
      continue
    }
    const id = ids[0]
    if (claims.has(id)) {
      fail('port-family', `${file}: reserved port id ${id} is already claimed by ${claims.get(id)}`)
      continue
    }
    claims.set(id, file)
  }

  return { violations, families: [...claims.entries()].sort() }
}

export function scanAdapterThinness(productionFiles, read, options) {
  const violations = []
  const fail = (gate, message) => violations.push({ gate, message })
  const root = options?.root ?? process.cwd()
  const files = (productionFiles ?? []).map(norm).filter((f) => f.endsWith('.fs') || f.endsWith('.fsi'))
  const fsFiles = files.filter((f) => f.endsWith('.fs'))

  // ⑪ port purity — scope discovered from the sources, so new port families are covered automatically.
  for (const file of purityScope(files, root)) {
    // An unreadable port file is a hard error: skipping it would hide the file from the scope.
    const body = code(read(file))
    const bad = [...body.matchAll(/\bWanxiangshu\.([A-Z]\w*)/g)].map((m) => m[1]).filter((ns) => ns !== 'Foundation' && ns !== 'Host')
    if (/\bOpenCode\b|\bOpencode\w*|\bomp\b/i.test(body.replace(/\/\/\/.*$/gm, ''))) bad.push('host vocabulary')
    if (bad.length) fail('port-purity', `${file}: neutral port references ${[...new Set(bad)].join(', ')}`)
  }

  // ⑫ port-family integrity — the mirror generator keys on the `HNI P-xx` marker and on the family
  // file name, so both are checked here rather than discovered by a failing build.
  violations.push(...scanPortFamilies(files, read, { root }).violations)

  // ⑩ adapter thinness ratchet
  const baseline = options?.baseline ?? readThinnessBaseline()
  const now = measure(files, read, root)
  if (!baseline) {
    fail('adapter-thinness', `${THINNESS_BASELINE} missing`)
    return { violations, measurement: now, baselineTotals: { productReference: 0, withoutPort: 0 } }
  }
  const base = baseline.productReference ?? {}
  const baseTotal = Object.values(base).reduce((a, b) => a + b, 0)
  if (now.productReferenceTotal > baseTotal) {
    const grown = Object.entries(now.productReference).filter(([f, n]) => n > (base[f] ?? 0)).map(([f, n]) => `${f} ${base[f] ?? 0}->${n}`)
    fail('adapter-thinness', `product-domain references in adapter files rose ${baseTotal} -> ${now.productReferenceTotal}: ${grown.join('; ')}`)
  }
  // Per-file: a baselined file may not grow. A file that is not in the baseline is allowed to carry
  // product-domain references while the totals stay within the baseline, so a lane that moves or splits
  // a baselined adapter file never has to edit the baseline (PROTOCOL: lanes never edit the baseline).
  for (const [file, n] of Object.entries(now.productReference)) {
    if (file in base && n > base[file]) {
      fail('adapter-thinness', `${file}: product-domain references rose ${base[file]} -> ${n}`)
    }
  }
  const baseWithout = baseline.withoutPort ?? []
  if (now.withoutPort.length > baseWithout.length) {
    const added = now.withoutPort.filter((f) => !baseWithout.includes(f))
    fail('adapter-thinness', `adapter files without a host-neutral port rose ${baseWithout.length} -> ${now.withoutPort.length}: ${added.join(', ')}`)
  }
  return { violations, measurement: now, baselineTotals: { productReference: baseTotal, withoutPort: baseWithout.length } }
}

// --- baseline writing ---------------------------------------------------------------------------
// Pure decision function: a baseline may only be written on a clean run. Any violation (product-reference
// total rose, a baselined file grew, adapter files without a port rose, or a port-purity violation) refuses
// the write, so the ratchet can never be raised by a regression.
export function baselineWritePlan(violations, measurement, prior) {
  const list = violations ?? []
  if (list.length > 0) {
    const reasons = list.map((v) => `${v.gate}: ${v.message}`)
    return {
      allowed: false,
      reason: `refusing to write ${THINNESS_BASELINE}: ${list.length} violation(s) would raise the ratchet; the baseline file is left unchanged`,
      reasons,
      content: null,
    }
  }
  const m = measurement
  if (!m) {
    return {
      allowed: false,
      reason: `refusing to write ${THINNESS_BASELINE}: no measurement available`,
      reasons: [],
      content: null,
    }
  }
  const content = JSON.stringify({
    _note: prior?._note ?? 'HNI-04 gate 10 (adapter-thinness). Totals only go down; tighten with --write-baseline.',
    productReference: m.productReference,
    withoutPort: m.withoutPort,
  }, null, 2) + '\n'
  return { allowed: true, reason: null, reasons: [], content }
}

// Applies the decision: `write` is only called when the plan allows it.
export function applyBaselineWrite({ violations, measurement, prior, write }) {
  const plan = baselineWritePlan(violations, measurement, prior)
  if (!plan.allowed) return { written: false, reason: plan.reason, reasons: plan.reasons, content: null }
  write(plan.content)
  return { written: true, reason: null, reasons: [], content: plan.content }
}

// --- CLI -----------------------------------------------------------------------------------------
function runCli() {
  const root = process.cwd()
  const files = walk(join(root, PRODUCTION_ROOT), ['.fs', '.fsi']).map((p) => norm(relative(root, p)))
  const read = (p) => readFileSync(p, 'utf8')
  const { violations, measurement, baselineTotals } = scanAdapterThinness(files, read, { root })
  const m = measurement ?? measure(files.filter((f) => f.endsWith('.fs')), read, root)
  if (process.argv.includes('--write-baseline')) {
    const prior = readThinnessBaseline()
    const result = applyBaselineWrite({
      violations,
      measurement: m,
      prior,
      write: (content) => writeFileSync(THINNESS_BASELINE, content),
    })
    if (result.written) {
      console.log(`baseline written: product-reference ${m.productReferenceTotal}, without-port ${m.withoutPort.length}`)
    } else {
      console.error(result.reason)
      for (const reason of result.reasons ?? []) console.error(`  ${reason}`)
      console.error(`${THINNESS_BASELINE} was NOT modified`)
    }
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
