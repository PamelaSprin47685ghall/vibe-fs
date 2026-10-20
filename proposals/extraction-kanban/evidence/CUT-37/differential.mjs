// Old-vs-new AblationManifest differential (CUT-37).
// Usage (repo root, after a build): copy the base build's dist/Ablation/Manifest.js to
// dist/Ablation/Manifest.base.js, then
// `node proposals/extraction-kanban/evidence/CUT-37/differential.mjs [seed] [rounds]`.
// Random manifests (valid and broken: missing/duplicate main nodes, bad slice parents,
// cycles, dangling edges, unknown edge kinds) and random three-state mode maps are fed to
// both validateNodes and validateDag; the Result tag, error case and reason must be equal.
import * as Old from '../../../../dist/Ablation/Manifest.base.js'
import * as New from '../../../../dist/Ablation/Manifest.js'
import { AblationNodeIdModule_create, AblationModeModule_parse } from '../../../../dist/Ablation/Model.js'
import { ofArray } from '../../../../dist/fable_modules/fable-library-js.5.13.0/Map.js'
import { compare } from '../../../../dist/fable_modules/fable-library-js.5.13.0/Util.js'

let seed = Number(process.argv[2] ?? 20261020) >>> 0
const rounds = Number(process.argv[3] ?? 2000)
const rand = () => ((seed = (seed * 1664525 + 1013904223) >>> 0) / 2 ** 32)
const pick = (xs) => xs[Math.floor(rand() * xs.length)]

const manifest = () => {
  const packages = Array.from({ length: 1 + Math.floor(rand() * 5) }, (_, i) => `p${i}`)
  const nodes = []
  for (const pkg of packages) {
    const r = rand()
    if (r > 0.08) nodes.push({ id: pkg, package: pkg, station: 0, kind: 'package', borrowed_surface: [] })
    if (r < 0.04) nodes.push({ id: `${pkg}-dup`, package: pkg, station: 0, kind: 'package', borrowed_surface: [] })
    for (let s = 0; s < Math.floor(rand() * 3); s++) {
      const q = rand()
      const parent = q < 0.85 ? pkg : q < 0.9 ? undefined : q < 0.95 ? 'ghost' : pick(packages)
      const n = { id: `${pkg}.s${s}`, package: pkg, station: 1, kind: 'slice', borrowed_surface: [] }
      if (parent !== undefined) n.parent = parent
      nodes.push(n)
    }
  }
  const ids = nodes.map((n) => n.id).concat(rand() < 0.2 || nodes.length === 0 ? ['ghost'] : [])
  const edges = []
  for (let e = 0; e < Math.floor(rand() * 8); e++) {
    edges.push({ from: pick(ids), to: pick(ids), kind: pick(['station-order', 'borrow', 'station-order', 'borrow', 'note']) })
  }
  return { version: 'ablation-v1', nodes, edges }
}

const modes = (doc) => ofArray(doc.nodes.filter(() => rand() < 0.9).map((n) => [
  AblationNodeIdModule_create(n.id), AblationModeModule_parse(pick(['ablated', 'borrowed', 'active'])),
]), { Compare: compare })

const view = (result) => result.tag === 0 ? 'Ok' : `Error:${result.fields[0].name ?? result.fields[0].tag}:${result.fields[0].fields.join('|')}`

let compared = 0, mismatches = 0, errors = 0
for (let r = 0; r < rounds; r++) {
  const doc = manifest()
  const decoded = Old.decodeFromJs(doc)
  if (decoded.tag !== 0) throw new Error('fixture did not decode: ' + view(decoded))
  const document = decoded.fields[0]
  const m = modes(doc)
  for (const [name, a, b] of [
    ['validateNodes', Old.validateNodes(document), New.validateNodes(document)],
    ['validateDag', Old.validateDag(document, m), New.validateDag(document, m)],
  ]) {
    compared++
    if (view(a) !== 'Ok') errors++
    if (view(a) !== view(b)) { mismatches++; if (mismatches < 5) console.error('MISMATCH', name, JSON.stringify(doc), view(a), view(b)) }
  }
}
console.log(JSON.stringify({ rounds, compared, errorsSeen: errors, mismatches }))
process.exit(mismatches ? 1 : 0)
