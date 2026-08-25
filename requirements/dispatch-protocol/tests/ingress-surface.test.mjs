// DISPATCH-PROTOCOL-004 — IngressSurface is the sole physical-acceptance contract.
// Old path: HostSignalBootstrap directly opened PromptIngress / PromptIngressCodec / HostSessionNudge.
// New path: all ingress decoding and hook wiring goes through Dispatch.IngressSurface.
// This test is RED before IngressSurface exists (module missing) and GREEN after.
//
// - physical evidence is the only way to establish PhysicalAccepted (receipt ≠ identity)
// - IngressSurface is the single published Dispatch.IngressSurface contract
// - HostSignalBootstrap must consume only that surface for ingress concerns

import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import { resolve, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')

// ── Surface existence and codec fidelity ───────────────────────────────────

test('WHAT[DISPATCH-PROTOCOL-004] INGRESS_004_surface_decodes_prompt_key_from_physical_message', async () => {
  const mod = await import('../../../dist/Interaction/Dispatch/OpenCode/IngressSurface.js')
  assert.ok(mod.decode, 'Dispatch.IngressSurface module must be published (decode)')

  // Host physical user message with PromptKey in metadata (PROMPT-011 field)
  const promptKey = 'pk-surface-004'
  const input = { sessionID: 'ses_ingress_004', messageID: 'msg_phys_004' }
  const output = {
    id: 'msg_phys_004',
    message: { id: 'msg_phys_004', role: 'user' },
    parts: [
      { type: 'text', text: 'hello via ingress surface' },
      { metadata: { wanxiangshu_prompt_key: promptKey } },
    ],
  }

  const decoded = mod.decode(input, output)
  assert.equal(decoded.PhysicalUserMessageId, 'msg_phys_004')
  assert.equal(decoded.PromptKey, promptKey)
  assert.equal(decoded.SessionId, 'ses_ingress_004')

  // Without PromptKey, surface must return None (not invent one)
  const outputNoKey = {
    id: 'msg_phys_005',
    message: { id: 'msg_phys_005', role: 'user' },
    parts: [{ type: 'text', text: 'external user without key' }],
  }
  const inputNoKey = { sessionID: 'ses_ingress_005', messageID: 'msg_phys_005' }
  const decodedNoKey = mod.decode(inputNoKey, outputNoKey)
  assert.equal(decodedNoKey.PromptKey, null)
})

test('WHAT[DISPATCH-PROTOCOL-004] INGRESS_004_surface_exposes_metadata_codec_as_single_field', async () => {
  const mod = await import('../../../dist/Interaction/Dispatch/OpenCode/IngressSurface.js')
  const packet = mod.createMetadata('pk-meta-004', 'Continuation:ProviderRetry', 'run-004')
  assert.equal(packet.wanxiangshu_prompt_key, 'pk-meta-004')
  assert.equal(packet.wanxiangshu_origin, 'Continuation:ProviderRetry')
  assert.equal(packet.wanxiangshu_logical_run, 'run-004')

  // Authority root has no logical run yet — must be null, not ""
  const packetNoRun = mod.createMetadata('pk-root-004', 'AuthorityRoot:AgentOwnerRoot', null)
  assert.equal(packetNoRun.wanxiangshu_prompt_key, 'pk-root-004')
  assert.equal(packetNoRun.wanxiangshu_logical_run, null)
})

test('WHAT[DISPATCH-PROTOCOL-004] INGRESS_004_surface_hook_is_physical_only_entry', async () => {
  const mod = await import('../../../dist/Interaction/Dispatch/OpenCode/IngressSurface.js')
  // We probe its JS shape without needing a full journal — just that it returns a function.
  const fakeJournal = null
  const hook = mod.createHook(
    fakeJournal,
    () => {},
    () => {},
    () => {},
    null,
    null,
  )
  assert.equal(typeof hook, 'function')
})

// ── Sole-contract ratchet: HostSignalBootstrap must not bypass the surface ──

test('WHAT[DISPATCH-PROTOCOL-004] INGRESS_004_host_signal_bootstrap_ingress_via_surface_only', () => {
  const p = resolve(ROOT, 'src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs')
  const text = readFileSync(p, 'utf8')

  // Must consume the published surface
  assert.match(text, /Wanxiangshu\.Interaction\.Dispatch/, 'must reference dispatch-protocol ingress')
  assert.match(text, /IngressSurface/, 'HostSignalBootstrap must consume Dispatch.IngressSurface')

  // Old direct opens must be gone from the ingress path.
  // HostSignalBootstrap may still open Dispatch for PromptDispatcher (owned by dispatcher node),
  // but it must not directly open the three ingress codec/nudge modules.
  const directOpens = [
    'open Wanxiangshu.Interaction.Dispatch.OpenCode',
    'PromptIngressCodec',
    'HostSessionNudge',
  ]
  for (const token of directOpens) {
    // The only allowed occurrence of PromptIngress is via IngressSurface re-export.
    if (token === 'PromptIngressCodec' || token === 'HostSessionNudge') {
      assert.doesNotMatch(text, new RegExp(token), `HostSignalBootstrap must not directly reference ${token}; use IngressSurface`)
    }
    if (token === 'open Wanxiangshu.Interaction.Dispatch.OpenCode') {
      assert.doesNotMatch(text, /open\s+Wanxiangshu\.Interaction\.Dispatch\.OpenCode/, 'HostSignalBootstrap must not open OpenCode directly for ingress; go via IngressSurface')
    }
  }

  // Direct PromptIngress type usage outside IngressSurface is a bypass.
  // The hook construction must be via IngressSurface.createHook.
  assert.match(text, /IngressSurface\.createHook/, 'ingress hook must be created via IngressSurface.createHook')
  assert.doesNotMatch(text, /PromptIngress\.createHook/, 'must not call PromptIngress.createHook directly')
  assert.match(text, /IngressSurface\.decode/, 'routing decode must be via IngressSurface.decode')
  assert.doesNotMatch(text, /PromptIngressCodec\.decode/, 'must not call PromptIngressCodec.decode directly')
})
