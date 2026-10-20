// CUT-08 segment 4 differential: the host-side utility role table (`cognitiveUtilityRoleAllowed`,
// literal skill/assume allows in permissionObj and requestToolMap) and the Fission admission's
// `r = Role.Engineer &&` guard (base dist) vs the wanxiangli/capability-backed candidate dist.
// Deterministic projections only; no effects. Usage: node differential-utility.mjs <base-root> <cand-root>
import assert from 'node:assert/strict'
const [baseRoot, candRoot] = process.argv.slice(2)
const load = async (root) => ({
  cap: await import(`${root}/dist/Foundation/OfficeCapability.js`),
  roles: await import(`${root}/dist/Foundation/Roles.js`),
  st: await import(`${root}/dist/OpenCode/Tools/StaticTools.js`),
  assume: await import(`${root}/dist/OpenCode/Tools/AssumeTool.js`),
  fission: await import(`${root}/dist/Execution/Fission/OpenCode/Tool.js`),
})
const base = await load(baseRoot), cand = await load(candRoot)
const roleLabels = ['manager','orchestrator','engineer','devops','plan','advisor','coder','inspector','browser','inquiry','distiller']
const role = (m, l) => m.roles.Roles_tryParseRole(l)
const plain = (o) => JSON.parse(JSON.stringify(o))
const list = (x) => Array.from(x)
const admit = (adm, r) => adm.fields[0](undefined, r)
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
for (const r of roleLabels) {
  same(`permissionObj ${r}`, m => plain(m.st.permissionObj(role(m, r))))
  same(`requestToolMap ${r} own`, m => list(m.st.requestToolMap(role(m, r), m.cap.OfficeCapability_permissions(role(m, r)))))
  same(`assume.roleAllowed ${r}`, m => m.assume.roleAllowed(role(m, r)))
  same(`assume.admission ${r}`, m => admit(m.assume.admission, role(m, r)))
  same(`fission.admission ${r}`, m => admit(m.fission.admission, role(m, r)))
}
console.log(`compared ${compared}; differing ${diffs.length}`)
for (const d of diffs) console.log(JSON.stringify(d))
