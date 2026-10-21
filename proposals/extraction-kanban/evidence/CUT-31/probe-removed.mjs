// CUT-31 ① probe of the removed in-repo Fit/DesignCheck (base dist, master d57f886c5).
// Usage: node probe-removed.mjs <base-dist>
import { join } from 'node:path'
const d = process.argv[2]
const fl = join(d, 'fable_modules/fable-library-js.5.13.0')
const { ofArray, toArray } = await import(join(fl, 'List.js'))
const M = await import(join(d, 'Sphinx/V2/Plugins/Ordinal/Model.js'))
const F = await import(join(d, 'Sphinx/V2/Plugins/Ordinal/Fit.js'))
const C = await import(join(d, 'Sphinx/V2/Plugins/Ordinal/DesignCheck.js'))
let k = 0
const D = (w, l, pos = 1) => new M.Ballot(`b${k++}`, 's', `c${k}`, new M.BallotKind(0, [w, l]), pos, true)
const conn = [D('a','b'),D('b','a',-1),D('a','b'),D('b','c'),D('c','b',-1),D('b','c'),D('a','c'),D('c','a',-1)]
const show = (r) => { const v = r.fields[0]; return { status: v.Status.name ?? v.Status.tag, iterationsReported: v.Iterations, gradientNorm: v.GradientNorm, beta: v.Beta ?? null, theta: Object.fromEntries(Array.from(v.Theta)) } }
for (const max of [1, 100, 10000]) console.log(`fit(maxIterations=${max}, tol=1e-8)`, JSON.stringify(show(F.Fit_fit(M.OrdinalModel_defaultModel, ofArray(conn), ofArray(['a','b','c']), max, 1e-8))))
console.log('tie-aware model, same ballots', JSON.stringify(show(F.Fit_fit(M.OrdinalModel_tieAwareModel, ofArray(conn), ofArray(['a','b','c']), 100, 1e-8))))
const c = C.DesignCheck_connectivity(ofArray(conn), ofArray(['a','b','c','z']))
console.log('connectivity with never-shown z', JSON.stringify({ components: toArray(c.Components).map(toArray), connected: c.Connected, isolated: toArray(c.Isolated) }))
