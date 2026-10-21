// CUT-31 ① shadow: the old in-repo Pairwise/Ranking (base dist, master d57f886c5) against
// wanxiangli/ordinal on the same seeded inputs. Usage: node shadow-ordinal.mjs <base-dist> <ordinal-entry> <rounds> <seed>
import { join } from 'node:path'
const [baseDist, entry, roundsArg = '100000', seedArg = '31'] = process.argv.slice(2)
const P = await import(join(baseDist, 'Sphinx/V2/Plugins/Ordinal/Pairwise.js'))
const R = await import(join(baseDist, 'Sphinx/V2/Plugins/Ordinal/Ranking.js'))
const fl = join(baseDist, 'fable_modules/fable-library-js.5.13.0')
const { ofArray, toArray } = await import(join(fl, 'List.js'))
const FMap = await import(join(fl, 'Map.js'))
const { comparePrimitives } = await import(join(fl, 'Util.js'))
const { OrdinalLaw } = await import(entry)
const law = new OrdinalLaw()
let s = Number(seedArg) >>> 0
const rnd = () => { s = (s + 0x6D2B79F5) >>> 0; let t = s; t = Math.imul(t ^ (t >>> 15), t | 1); t ^= t + Math.imul(t ^ (t >>> 7), t | 61); return ((t ^ (t >>> 14)) >>> 0) / 4294967296 }
const int = (n) => Math.floor(rnd() * n)
const real = () => (rnd() < 0.1 ? (rnd() < 0.5 ? -1 : 1) * 1000 * rnd() : (rnd() - 0.5) * 20)
const ids = ['a', 'b', 'c', 'd', 'e', 'f']
const fmap = (o) => FMap.ofList(ofArray(Object.entries(o)), { Compare: comparePrimitives })
const same = (x, y) => Object.is(x, y) || (Number.isNaN(x) && Number.isNaN(y))
const fam = {}, bad = {}
const check = (name, a, b) => { fam[name] = (fam[name] ?? 0) + 1; const eq = JSON.stringify(a) === JSON.stringify(b) && (typeof a !== 'number' || same(a, b)); if (!eq) { bad[name] = (bad[name] ?? 0) + 1; if (bad[name] <= 3) console.log('DIFF', name, a, b) } }
const subset = () => ids.filter(() => rnd() < 0.6)
const shuffle = (xs) => { const ys = [...xs]; for (let i = ys.length - 1; i > 0; i--) { const j = int(i + 1); [ys[i], ys[j]] = [ys[j], ys[i]] } return ys }
for (let r = 0; r < Number(roundsArg); r++) {
  const x = real(), y = real(), b = real() / 10, pos = [-1, 0, 1][int(3)], k = real() / 5, tie = rnd() < 0.5
  check('logSigmoid', P.logSigmoid(x), law.logSigmoid(x))
  check('logSumExp', P.logSumExp(x, y), law.logSumExp(x, y))
  check('directional', P.directionalLogLikelihood(x, y, b, pos), law.directionalLogLikelihood(x, y, b, pos))
  check('tieAware', P.tieAwareLogLikelihood(x, y, b, pos, k, tie), law.tieAwareLogLikelihood(x, y, b, pos, k, tie))
  check('gradient', [...P.directionalGradient(x, y, b, pos)], law.directionalGradient(x, y, b, pos))
  const theta = Object.fromEntries(subset().map(id => [id, real() / 50]))
  const set = shuffle(subset()), order = shuffle(subset())
  const best = ids[int(6)], worst = ids[int(6)]
  check('maxDiff', R.maxDiffLogProbability(fmap(theta), ofArray(set), best, worst) ?? null, law.maxDiffLogProbability(theta, set, best, worst))
  check('plackettLuce', R.plackettLuceLogProbability(fmap(theta), ofArray(order)) ?? null, law.plackettLuceLogProbability(theta, order))
  check('compositePairs', toArray(R.compositePairs(ofArray(order))).map(p => [...p]), law.compositePairs(order))
  const rankings = Array.from({ length: int(5) }, () => shuffle(subset()))
  const cands = subset()
  check('borda', Object.fromEntries(Array.from(FMap.toSeq ? FMap.toSeq(R.bordaCounts(ofArray(rankings.map(o => ofArray(o))), ofArray(cands))) : [], ([k2, v]) => [k2, v])), { ...law.bordaCounts(rankings, cands) })
}
for (const k of Object.keys(fam)) console.log(`${k} cases=${fam[k]} differences=${bad[k] ?? 0}`)
