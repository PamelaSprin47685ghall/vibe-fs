/**
 * CUT-25: schema (permissionObj), execution admission (JsToolSpec) and
 * generator (isGeneratedToolName) share JsCapability.admitsProgrammingSurface.
 *
 * Pre-unify dual lists kept as oracles for synthetic permission sets that
 * used to diverge (base-red).
 */
import assert from 'node:assert/strict'
import { empty, add } from '../../../../dist/fable_modules/fable-library-js.5.13.0/Set.js'
import { ToolPermission } from '../../../../dist/Foundation/OfficeCapability.js'
import { Role as RoleX } from '../../../../dist/Foundation/Roles.js'
import { OfficeCapability_permissions as permissions } from '../../../../dist/Foundation/OfficeCapability.js'
import {
  JsCapabilityModule_ofToolCapabilities as ofToolCapabilities,
  JsCapabilityModule_admitsProgrammingSurface as admitsSurface,
} from '../../../../dist/Repository/Programming/Js/Capability.js'
import { JsToolGenerator_isGeneratedToolName as isGenerated } from '../../../../dist/Repository/Programming/Js/Surface.js'
import { permissionObj, jsToolName } from '../../../../dist/OpenCode/Tools/StaticTools.js'
import { JsToolSpec_admissionFor as admissionFor } from '../../../../dist/Repository/Programming/Js/OpenCode/ToolHost.js'

const comparer = { Compare: (a, b) => (a.tag === b.tag ? 0 : a.tag < b.tag ? -1 : 1) }
function setOf(...ps) {
  let s = empty(comparer)
  for (const p of ps) s = add(p, s)
  return s
}
function labelOf(p) {
  return p.cases()[p.tag]
}
function oldHasFs(permSet) {
  const fs = new Set(['Read', 'Write', 'Edit', 'Glob', 'Grep', 'Move', 'Remove', 'Fork', 'Join'])
  for (const p of permSet) if (fs.has(labelOf(p))) return true
  return false
}
function oldAdmission(permSet) {
  const surface = new Set([
    'Read', 'Write', 'Edit', 'Glob', 'Grep', 'Move', 'Remove', 'Exec', 'Fork', 'Join',
    'Fission', 'Horizon', 'Advisor', 'Ask', 'JsPlan', 'Resume', 'Review', 'ReviewAssessment',
    'Fetch', 'DevOpsConsultation', 'Deliver', 'Finality',
  ])
  for (const p of permSet) if (surface.has(labelOf(p))) return true
  return false
}

const roles = [
  RoleX.Manager, RoleX.Orchestrator, RoleX.Engineer, RoleX.DevOps, RoleX.Plan,
  RoleX.Advisor, RoleX.Distiller, RoleX.Coder, RoleX.Inspector, RoleX.Browser, RoleX.Inquiry,
]
const rows = []
let differing = 0
for (const role of roles) {
  const name = role.cases()[role.tag]
  const perms = permissions(role)
  const tool = jsToolName(role)
  const schema = permissionObj(role)[tool] === 'allow'
  const adm = admissionFor(name)
  const admission = !!(adm && typeof adm.fields?.[0] === 'function' && adm.fields[0]({}, role))
  const generated = isGenerated(name, perms, tool)
  const surface = admitsSurface(perms)
  if (schema !== generated || admission !== generated || surface !== generated) differing++
  assert.equal(schema, generated, `${name} schema vs generator`)
  assert.equal(admission, generated, `${name} admission vs generator`)
  assert.equal(surface, generated, `${name} admitsProgrammingSurface vs generator`)
  rows.push({ name, schema, admission, generated, surface })
}

// Synthetic base-red: Edit alone — old hasFs true, generator/new false.
{
  const editOnly = setOf(ToolPermission.Edit)
  assert.equal(oldHasFs(editOnly), true, 'base-red: old hasFs(Edit)')
  assert.equal(ofToolCapabilities(editOnly).size, 0)
  assert.equal(admitsSurface(editOnly), false)
}
// Synthetic base-red: Exec alone — old hasFs false, old admission true, new true.
{
  const execOnly = setOf(ToolPermission.Exec)
  assert.equal(oldHasFs(execOnly), false, 'base-red: old hasFs(Exec)')
  assert.equal(oldAdmission(execOnly), true, 'base-red: old admission(Exec)')
  assert.equal(admitsSurface(execOnly), true)
}

console.log(JSON.stringify({ compared: rows.length, differing, rows }, null, 2))
