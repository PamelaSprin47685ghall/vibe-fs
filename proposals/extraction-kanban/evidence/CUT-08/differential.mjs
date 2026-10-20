// Pure-decision differential for CUT-08: old host office matrix / manager gate (base dist)
// vs the wanxiangli/capability-backed candidate dist. Deterministic functions only; no effects.
import assert from 'node:assert/strict'
const [baseRoot, candRoot] = process.argv.slice(2)
const load = async (root) => ({
  cap: await import(`${root}/dist/Foundation/OfficeCapability.js`),
  roles: await import(`${root}/dist/Foundation/Roles.js`),
  surface: await import(`${root}/dist/Participant/Persona/OfficeCapabilitySurface.js`),
})
const base = await load(baseRoot), cand = await load(candRoot)
const roleLabels = ['manager','orchestrator','engineer','devops','plan','advisor','coder','inspector','browser','inquiry','distiller']
const permissionLabels = ['Fork','Resume','Join','Horizon','Fission','Read','Write','Edit','Glob','Grep','Move','Remove','Exec','Pty',
  'ReviewAssessment','Fetch','Finality','BashHoneypot','JsPlan','Ask','Deliver','Review','DevOpsConsultation','Advisor']
const odd = ['Engineer','DEVOPS','bookkeeper','no-such','',' ',null,undefined,'read','Telepathy',42]
const labelsOf = (m, set) => Array.from(set, p => m.cap.OfficeCapability_permissionLabel(p)).sort()
const role = (m, l) => { const r = m.roles.Roles_tryParseRole(l); if (r == null) throw new Error(`no role ${l}`); return r }
const perm = (m, l) => m.cap.OfficeCapability_permissionOfLabel(l)
let compared = 0
const diffs = []
const same = (label, f) => {
  let b, c, be, ce
  try { b = f(base) } catch (e) { be = String(e?.message ?? e) }
  try { c = f(cand) } catch (e) { ce = String(e?.message ?? e) }
  compared++
  try { assert.deepEqual({ value: c, threw: ce !== undefined }, { value: b, threw: be !== undefined }) }
  catch { diffs.push({ label, base: be ? { threw: be } : b, cand: ce ? { threw: ce } : c }) }
}
same('managerReviewReadOnly', m => labelsOf(m, m.cap.OfficeCapability_managerReviewReadOnlyPermissions))
same('managerFinish', m => labelsOf(m, m.cap.OfficeCapability_managerFinishPermissions))
same('forkableOffices', m => m.surface.managerForkableOffices())
for (const r of roleLabels) {
  same(`permissions ${r}`, m => labelsOf(m, m.cap.OfficeCapability_permissions(role(m, r))))
  for (const p of permissionLabels) same(`isAllowed ${r} ${p}`, m => m.cap.OfficeCapability_isAllowed(role(m, r), perm(m, p)))
}
for (const r of [...roleLabels, ...odd]) {
  same(`surface.permissions ${r}`, m => m.surface.permissions(r))
  for (const p of [...permissionLabels, ...odd]) same(`surface.isAllowed ${r} ${p}`, m => m.surface.isAllowed(r, p))
}
const bools = [false, true]
for (const a of bools) for (const s of bools) for (const c of bools) for (const f of bools) for (const d of [undefined, 'digest-1', '']) {
  const facts = m => new m.cap.ManagerCapabilityFacts(a, s, c, f, d)
  const key = `facts a=${a} s=${s} c=${c} f=${f} d=${JSON.stringify(d)}`
  same(key, m => labelsOf(m, m.cap.OfficeCapability_permissionsForManagerFacts(facts(m))))
  for (const p of permissionLabels) same(`${key} ${p}`, m => m.cap.OfficeCapability_isAllowedForManagerFacts(facts(m), perm(m, p)))
  for (const p of [...permissionLabels, ...odd]) same(`surface ${key} ${p}`, m => m.surface.managerFactsAllowed(m.surface.managerFacts(a, s, f, d ?? null), p))
}
console.log(`compared ${compared}; differing ${diffs.length}`)
for (const d of diffs.slice(0, 20)) console.log(JSON.stringify(d))
