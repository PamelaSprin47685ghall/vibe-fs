// CUT-11 recovery differential: the base private PromptRecovery.findPhysical (exposed by
// appending an export to a temp copy of the base dist module, nothing else changed) vs the
// candidate's DispatchLaw.recover (wanxiangli/dispatch) fed exactly as candidate Recovery.fs
// feeds it: (Role, PromptKey carrier, Id), oldest first.
// Usage: node recovery-diff.mjs <base-root> <cand-root> [cases]
import assert from 'node:assert/strict'
import { readFileSync, writeFileSync, rmSync } from 'node:fs'
const [baseRoot, candRoot, caseArg] = process.argv.slice(2)
const CASES = Number(caseArg ?? 200000)
const rec = `${baseRoot}/dist/Interaction/Dispatch/Recovery.js`, tmp = `${baseRoot}/dist/Interaction/Dispatch/.Recovery.cut11-diff.js`
writeFileSync(tmp, readFileSync(rec, 'utf8') + '\nexport { findPhysical as __cut11FindPhysical };\n')
let baseMod
try { baseMod = await import(tmp) } finally { rmSync(tmp) }
const bid = await import(`${baseRoot}/dist/Foundation/Identity.js`)
const bsnap = await import(`${baseRoot}/dist/OpenCode/Host/SessionSnapshot.js`)
const fl = await import(`${baseRoot}/dist/fable_modules/fable-library-js.5.13.0/List.js`)
const cmodel = await import(`${candRoot}/dist/Interaction/Authority/Model.js`)
const cfl = await import(`${candRoot}/dist/fable_modules/fable-library-js.5.13.0/List.js`)
const { FSharpList: _ } = fl
let seed = 0x9e3779b9
const rnd = (n) => { seed ^= seed << 13; seed >>>= 0; seed ^= seed >>> 17; seed ^= seed << 5; seed >>>= 0; return seed % n }
const roles = ['user', 'assistant', 'system', 'User', 'tool']
const keys = [undefined, 'K', 'K2', 'K ', 'k']
let compared = 0
const tally = { proven: 0, pending: 0 }, diffs = []
for (let c = 0; c < CASES; c++) {
  const len = [0, 1, 2, 5, 49, 50, 51, 52, 80][rnd(9)]
  const msgs = Array.from({ length: len }, (_, i) => ({ id: `m${i}`, role: roles[rnd(roles.length)], key: rnd(4) === 0 ? keys[rnd(keys.length)] : undefined }))
  const hasReceipt = rnd(2) === 0
  const bmsgs = fl.ofArray(msgs.map((m) => new bsnap.SessionMessage(m.id, m.role, undefined, undefined, undefined, undefined, undefined, undefined, false, false, m.key, fl.empty(), fl.empty(), fl.empty())))
  const b = baseMod.__cut11FindPhysical(bid.PromptKeyModule_create('K'), bmsgs)
  const baseVerdict = b === undefined ? { pending: hasReceipt } : { proven: bid.PhysicalUserMessageIdModule_value(b) }
  const r = cmodel.DispatchLaw_recover('K', hasReceipt, cfl.ofArray(msgs.map((m) => [m.role, m.key, m.id])))
  const candVerdict = r.tag === 0 ? { proven: r.fields[0] } : { pending: r.fields[0] }
  compared++
  tally[baseVerdict.proven !== undefined ? 'proven' : 'pending']++
  try { assert.deepEqual(candVerdict, baseVerdict) } catch { if (diffs.length < 20) diffs.push({ len, msgs: msgs.filter((m) => m.key), baseVerdict, candVerdict }); else diffs.length++ }
}
console.log(JSON.stringify({ compared, differences: diffs.length, verdicts: tally }, null, 1))
if (diffs.length) { console.log(JSON.stringify(diffs.slice(0, 3), null, 1)); process.exit(1) }
