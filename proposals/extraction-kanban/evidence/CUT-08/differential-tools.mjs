// CUT-08 segment 2 differential: old host tool-name map / reverse lookup / permission object (base dist)
// vs the wanxiangli/capability-backed candidate dist. Deterministic projections only; no effects.
import assert from 'node:assert/strict'
const [baseRoot, candRoot] = process.argv.slice(2)
const load = async (root) => ({
  cap: await import(`${root}/dist/Foundation/OfficeCapability.js`),
  roles: await import(`${root}/dist/Foundation/Roles.js`),
  st: await import(`${root}/dist/OpenCode/Tools/StaticTools.js`),
})
const base = await load(baseRoot), cand = await load(candRoot)
const roleLabels = ['manager','orchestrator','engineer','devops','plan','advisor','coder','inspector','browser','inquiry','distiller']
const permissionLabels = ['Fork','Resume','Join','Horizon','Fission','Read','Write','Edit','Glob','Grep','Move','Remove','Exec','Pty',
  'ReviewAssessment','Fetch','Finality','BashHoneypot','JsPlan','Ask','Deliver','Review','DevOpsConsultation','Advisor']
const names = ['fork','resume','commission','join','horizon','fission','skill','assume','bash-honeypot','review','fetch','deliver',
  'todowrite-for-engineer','todowrite-for-manager','todowrite-for-orchestrator','todowrite-for-devops','todowrite-for-bookkeeper',
  'todowrite-for-plan','todowrite','bash','read','write','edit','glob','grep','exec','pty','advisor','ask','devops','','Fork','REVIEW']
const role = (m, l) => m.roles.Roles_tryParseRole(l)
const perm = (m, l) => m.cap.OfficeCapability_permissionOfLabel(l)
const label = (m, p) => p == null ? null : m.cap.OfficeCapability_permissionLabel(p)
const list = (x) => Array.from(x)
const plain = (o) => JSON.parse(JSON.stringify(o))
let compared = 0
const diffs = []
const same = (k, f) => {
  let b, c, be, ce
  try { b = f(base) } catch (e) { be = String(e?.message ?? e) }
  try { c = f(cand) } catch (e) { ce = String(e?.message ?? e) }
  compared++
  try { assert.deepEqual({ value: c, threw: ce !== undefined }, { value: b, threw: be !== undefined }) }
  catch { diffs.push({ k, base: be ? { threw: be } : b, cand: ce ? { threw: ce } : c }) }
}
for (const p of permissionLabels) same(`toolNames ${p}`, m => list(m.st.toolNames(perm(m, p))))
same('knownToolNames', m => list(m.st.knownToolNames))
for (const r of roleLabels) {
  same(`permissionObj ${r}`, m => plain(m.st.permissionObj(role(m, r))))
  same(`jsToolName ${r}`, m => m.st.jsToolName(role(m, r)))
  for (const n of names) {
    same(`permissionOfToolName ${r} ${n}`, m => label(m, m.st.permissionOfToolName(role(m, r), n)))
    same(`admitsToolForRole ${r} ${n}`, m => m.st.admitsToolForRole(role(m, r), n))
  }
  // requestToolMap over the role's own set and over every single-permission set.
  same(`requestToolMap ${r} own`, m => list(m.st.requestToolMap(role(m, r), m.cap.OfficeCapability_permissions(role(m, r)))).map(([k, v]) => [k, v]))
}
console.log(`compared ${compared}; differing ${diffs.length}`)
for (const d of diffs.slice(0, 20)) console.log(JSON.stringify(d))
