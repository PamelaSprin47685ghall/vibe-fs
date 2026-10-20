/**
 * CUT-25: which runtime api members the host installs (ToolsBindings.createApi),
 * old hand-written capability gate vs library member directory.
 * Usage: node differential-runtime-api.mjs <base-dist> <candidate-dist>
 * For every office role, the installed api member names (in order) must be identical,
 * and every library member except the base-class sugar `dag` must have an api member.
 */
import assert from 'node:assert/strict'
import path from 'node:path'
import { pathToFileURL } from 'node:url'

async function load(dist) {
  const m = (p) => import(pathToFileURL(path.resolve(dist, p)).href)
  const { Role } = await m('Foundation/Roles.js')
  const { OfficeCapability_permissions } = await m('Foundation/OfficeCapability.js')
  const c = await m('Repository/Programming/Js/Capability.js')
  const t = await m('Repository/Programming/Js/ToolsBindings.js')
  return { Role, OfficeCapability_permissions, c, t }
}

function project(L) {
  const out = {}
  for (const name of new L.Role(0, []).cases()) {
    const perms = L.OfficeCapability_permissions(L.Role[name])
    const caps = L.c.JsCapabilityModule_ofToolCapabilities(perms)
    const api = L.t.JsToolsBindings_createApi(caps, '/nonexistent-cut25', [], [], [], null, undefined, undefined, undefined)
    out[name] = { api: Object.keys(api), directory: L.c.JsCapabilityModule_memberNamesFor ? [...L.c.JsCapabilityModule_memberNamesFor(caps, '')] : null }
  }
  return out
}

const [baseDist, candDist] = process.argv.slice(2)
const base = project(await load(baseDist))
const cand = project(await load(candDist))
const rows = Object.keys(base).map((k) => ({
  role: k,
  same: JSON.stringify(base[k].api) === JSON.stringify(cand[k].api),
  api: cand[k].api,
  directoryWithoutSugar: (cand[k].directory ?? []).filter((n) => n !== 'dag'),
}))
for (const r of rows) assert.deepEqual([...r.api].sort(), [...r.directoryWithoutSugar].sort(), `${r.role}: api members = library directory minus dag`)
const differing = rows.filter((r) => !r.same)
console.log(JSON.stringify({ compared: rows.length, differing: differing.length, rows }, null, 2))
for (const r of differing) assert.deepEqual(cand[r.role].api, base[r.role].api, r.role)
