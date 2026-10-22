// Pure-decision differential: old host identity owner (base dist) vs library-backed candidate dist.
// Only deterministic catalog/identity functions are double-computed; no external effects.
import assert from 'node:assert/strict'
const [baseRoot, candRoot] = process.argv.slice(2)
const load = async (root) => ({
  persona: await import(`${root}/dist/Participant/Persona/Surface.js`),
  roles: await import(`${root}/dist/Foundation/RolesSurface.js`),
  office: await import(`${root}/dist/Participant/Persona/OfficeCapabilitySurface.js`),
})
const base = await load(baseRoot), cand = await load(candRoot)
const names = ['manager','orchestrator','engineer','devops','plan','advisor','bookkeeper','coder','inspector','browser','inquiry','distiller',
  'build','student','teacher','meditator','executor','blogger','predictor','chronicler','Engineer','DEVOPS','fast_engineer','no-such','nosuch','',' ',
  'a b','a.b','x'.repeat(70),null,undefined,'Bookkeeper','Advisor']
const roles = ['manager','orchestrator','engineer','devops','plan','advisor','bookkeeper','coder','inspector','browser','inquiry','distiller','Engineer','blogger','',null]
const personas = ['Lead','Director','Engineer','Operator','Planner','Auditor','Curator','Coder','Investigator','DirectorV1','',' ',null]
const versions = [1, 2, 0]
const origins = ['ResolvedAtRoot','InheritedFromOwner','weird','']
const owners = ['', 'manager', 'engineer', 'bookkeeper', 'coder', 'advisor']
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
for (const k of ['allRoleLabels','allPublicRoleLabels','allInternalRoleLabels','requiredNames','legacyNames','currentCatalogVersion'])
  same(`persona.${k}`, m => m.persona[k])
for (const k of ['allRoleLabels','allPublicRoleLabels','allInternalRoleLabels']) same(`roles.${k}`, m => m.roles[k])
for (const n of names) {
  same(`isManagedName ${n}`, m => m.persona.isManagedName(n))
  same(`isLegacyName ${n}`, m => m.persona.isLegacyName(n))
  same(`roleName ${n}`, m => m.persona.roleName(n))
  same(`persona ${n}`, m => m.persona.persona(n, 'deep'))
  same(`nameOf ${n}`, m => m.persona.nameOf('deep', n))
  same(`resolve ${n}`, m => m.persona.resolveParticipantIdentityAtRoot(n))
  for (const o of owners) same(`inherit ${n}<-${o}`, m => m.persona.inheritParticipantIdentityFromOwner(n, o))
}
for (const r of roles) same(`office.permissions ${r}`, m => m.office.permissions?.(r))
for (const o of owners) for (const n of names) for (const r of roles) for (const p of personas) for (const v of versions) for (const og of origins)
  same(`rehydrate ${o}|${n}|${r}|${p}|${v}|${og}`, m => m.persona.rehydrateParticipantIdentity(o, n, r, 'deep', 'peer', p, v, og))
console.log(`compared ${compared}; differing ${diffs.length}`)
for (const d of diffs.slice(0, 60)) console.log(JSON.stringify(d))
process.exitCode = diffs.length ? 1 : 0
