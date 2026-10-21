// CUT-14 ② replay differential: the ledger's answer to an already-counted failure (ProviderFailureLedger.replayLatestFailure,
// a private function of the compiled Fallback/Ledger.js) before and after the cut. Both compiled modules are run as-is: a
// probe copy of Ledger.js / ProviderFailureSurface.js is written next to the original with one appended export line, so
// the private function and the surface's plain→F# converters become importable; nothing else is changed.
// usage: node diff-replay.mjs <seed> <runs> <old Fallback dir> <new Fallback dir> [mutant]
// mutants (applied to the new side's answer, to show the harness is sensitive): anyRecent (any remembered key, not only the
// latest, is replayable), ignoreBudget (latest always RetryAuthorized), ignoreExhausted (exhausted projection still authorizes)
import { readFileSync, writeFileSync, rmSync } from 'node:fs'
import { join } from 'node:path'
import { pathToFileURL } from 'node:url'
const [seedArg, runsArg, oldDir, newDir, mutant] = process.argv.slice(2)
async function load(dir) {
  const lp = join(dir, 'Ledger.__probe.js'), sp = join(dir, 'ProviderFailureSurface.__probe.js')
  writeFileSync(lp, readFileSync(join(dir, 'Ledger.js'), 'utf8') + '\nexport { ProviderFailureLedger_replayLatestFailure as __replay }\n')
  writeFileSync(sp, readFileSync(join(dir, 'ProviderFailureSurface.js'), 'utf8') + '\nexport { projectionOf as __projectionOf, identityOf as __identityOf }\n')
  try {
    const L = await import(pathToFileURL(lp).href), S = await import(pathToFileURL(sp).href)
    return { S, replay: (limit, ident, h) => L.__replay(limit, S.__identityOf(ident), S.__projectionOf(h)).name ?? String(L.__replay(limit, S.__identityOf(ident), S.__projectionOf(h)).tag) }
  } finally { rmSync(lp); rmSync(sp) }
}
const A = await load(oldDir), B = await load(newDir)
let s = Number(seedArg) >>> 0 || 1
const rnd = () => ((s = (Math.imul(s ^ (s >>> 15), 0x2c1b3c6d) + 0x9e3779b9) >>> 0) / 2 ** 32)
const pick = xs => xs[Math.floor(rnd() * xs.length)]
const runs = ['lr-a', 'lr-b', ''], roots = ['root-1', 'root-2'], sessions = ['s1', 's2', '', 'a\u001fb']
const J = JSON.stringify
let mismatches = 0, first = null, steps = 0, replays = 0
const seen = {}
for (let run = 0; run < Number(runsArg) && !first; run++) {
  const lr = pick(runs), root = pick(roots)
  let ha = A.S.providerFailureProjection.forAuthority(lr, root), hb = B.S.providerFailureProjection.forAuthority(lr, root)
  const pool = 4 + Math.floor(rnd() * 40), len = 1 + Math.floor(rnd() * 60), applied = []
  for (let i = 0; i < len && !first; i++) {
    steps++
    const r = rnd()
    if (r < 0.6) {
      const ident = { session: pick(sessions), run: rnd() < 0.93 ? lr : pick(runs), root: rnd() < 0.93 ? root : pick(roots), attempt: `p${Math.floor(rnd() * pool)}` }
      const cur = A.S.providerFailureProjection.read(ha).failures
      const count = rnd() < 0.9 ? cur + 1 : pick([cur, cur + 2])
      const ra = A.S.providerFailureProjection.applyFailure(ident, count, ha), rb = B.S.providerFailureProjection.applyFailure(ident, count, hb)
      if (ra.ok) { ha = ra.value; applied.unshift(ident) }
      if (rb.ok) hb = rb.value
    } else if (r < 0.63) { ha = A.S.providerFailureProjection.applyExhausted(ha); hb = B.S.providerFailureProjection.applyExhausted(hb) }
    else if (r < 0.7) { ha = A.S.providerFailureProjection.recordSuccess(ha); hb = B.S.providerFailureProjection.recordSuccess(hb) }
    // replay probe: the latest applied attempt, an older one, or a never-seen one, under several limits
    const probe = applied.length && rnd() < 0.5 ? applied[0] : applied.length && rnd() < 0.6 ? pick(applied)
      : { session: pick(sessions), run: lr, root, attempt: `q${Math.floor(rnd() * 5)}` }
    for (const limit of [pick([0, 1, 2, 3, 5]), 12]) {
      replays++
      const o = A.replay(limit, probe, ha)
      let n = B.replay(limit, probe, hb)
      if (mutant) {
        const p = B.S.__projectionOf(hb), key = B.S.budget.dedupeKey(probe), keys = [...p.RecentFailureKeys]
        const may = B.S.providerFailureProjection.mayRetry(limit, hb)
        if (mutant === 'anyRecent' && keys.includes(key)) n = may ? 'RetryAuthorized' : 'RetryExhausted'
        if (mutant === 'ignoreBudget' && n === 'RetryExhausted' && !p.Exhausted) n = 'RetryAuthorized'
        if (mutant === 'ignoreExhausted' && n === 'RetryExhausted' && p.Exhausted) n = 'RetryAuthorized'
      }
      seen[o] = (seen[o] ?? 0) + 1
      if (o !== n) { mismatches++; first ??= { run, i, limit, probe, old: o, new: n } }
    }
  }
}
console.log(J({ seed: Number(seedArg), runs: Number(runsArg), mutant: mutant ?? null, steps, replays, mismatches, first, seen }))
