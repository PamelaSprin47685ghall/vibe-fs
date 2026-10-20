/**
 * CUT-25: generated programming surface, old host projection vs library law.
 * Usage: node differential-surface.mjs <base-dist> <candidate-dist>
 * For every office role × provider language, compares the whole generated
 * JsSurface (tool name, JS capabilities, member directory, description, base
 * class, examples) plus membersFor / isGeneratedToolName / memberBinding.
 */
import assert from 'node:assert/strict'
import path from 'node:path'
import { pathToFileURL } from 'node:url'

async function load(dist) {
  const m = (p) => import(pathToFileURL(path.resolve(dist, p)).href)
  const { Role } = await m('Foundation/Roles.js')
  const { OfficeCapability_permissions } = await m('Foundation/OfficeCapability.js')
  const { ProviderLanguage } = await m('Participant/Provider/Language.js')
  const s = await m('Repository/Programming/Js/Surface.js')
  const c = await m('Repository/Programming/Js/Capability.js')
  const { JsDescriptionAssets_load } = await m('Repository/Programming/Js/OpenCode/ToolHost.js')
  return { Role, OfficeCapability_permissions, ProviderLanguage, s, c, JsDescriptionAssets_load }
}

const arr = (xs) => (xs == null ? [] : [...xs])
const tagName = (u) => u.cases()[u.tag]

function project(L) {
  const out = {}
  const members = ['write', 'rm', 'mv', 'cat', 'ls', 'rg', 'stat', 'mkdir', 'whereAll', 'where', 'call', 'fork',
    'resume', 'commission', 'devops', 'dag', 'join', 'fission', 'horizon', 'advisor', 'ask', 'deliver', 'park',
    'reviewOnly.cat', 'reviewOnly.ls']
  for (const lang of [L.ProviderLanguage.English, L.ProviderLanguage.SimplifiedChinese]) {
    const prose = L.JsDescriptionAssets_load(lang)
    for (const name of new L.Role(0, []).cases()) {
      const role = L.Role[name]
      const perms = L.OfficeCapability_permissions(role)
      const surface = L.s.JsToolGenerator_generate(name, perms, prose)
      const caps = L.c.JsCapabilityModule_ofToolCapabilities(perms)
      const key = `${tagName(lang)}/${name}`
      out[key] = {
        admits: L.c.JsCapabilityModule_admitsProgrammingSurface(perms),
        ofToolCapabilities: arr(caps).map(tagName).sort(),
        membersFor: arr(L.s.JsToolGenerator_membersFor(caps)).map((f) => [tagName(f.Capability), f.MemberName]),
        isGenerated: L.s.JsToolGenerator_isGeneratedToolName(name, perms, 'todowrite-for-' + name.toLowerCase()),
        bindings: members.map((mn) => L.s.JsToolGenerator_memberBinding(name, perms, mn) ?? null),
        surface: surface == null ? null : {
          ToolName: surface.ToolName,
          RoleName: surface.RoleName,
          Capabilities: arr(surface.Capabilities).map(tagName).sort(),
          Members: arr(surface.Members).map((f) => [tagName(f.Capability), f.MemberName]),
          Description: surface.Description,
          BaseClassSource: surface.BaseClassSource,
          Examples: arr(surface.Examples),
        },
      }
    }
  }
  return out
}

const [baseDist, candDist] = process.argv.slice(2)
const base = project(await load(baseDist))
const cand = project(await load(candDist))
const rows = Object.keys(base).map((k) => {
  const same = JSON.stringify(base[k]) === JSON.stringify(cand[k])
  return { key: k, same, generated: cand[k].surface != null, members: cand[k].surface?.Members.map((m) => m[1]) ?? [] }
})
const differing = rows.filter((r) => !r.same)
console.log(JSON.stringify({ compared: rows.length, differing: differing.length, generated: rows.filter((r) => r.generated).length, rows }, null, 2))
for (const r of differing) assert.deepEqual(cand[r.key], base[r.key], r.key)
