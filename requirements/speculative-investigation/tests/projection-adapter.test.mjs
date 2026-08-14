import assert from 'node:assert/strict'
import test from 'node:test'

import * as WireDecode from '../../../dist/OpenCode/Codec/ProviderWireDecode.js'
import * as WireCapture from '../../../dist/OpenCode/Codec/ProviderWireCapture.js'
import * as MessageEdit from '../../../dist/OpenCode/Codec/ProjectionMessageEdit.js'
import * as Provider from '../../../dist/Participant/Provider/Projection/Model.js'
import * as Id from '../../../dist/Foundation/Identity.js'
import { toList, listItems } from '../../verification-system/tests/support/domain.mjs'

const H = (text) => `H(${text})`
const resultOf = (value) => value.tag === 0
  ? { ok: true, value: value.fields[0] }
  : { ok: false, error: value.fields[0] }
const text = (value) => new Provider.WirePart(0, [value])
const call = (id, name, args) => new Provider.WirePart(2, [Id.ToolCallIdModule_create(id), name, args])
const result = (id, value) => new Provider.WirePart(3, [Id.ToolCallIdModule_create(id), value])
const media = (mime, digest) => new Provider.WirePart(4, [mime, digest])
const msg = (role, parts) => ({ Role: role, Parts: toList(parts) })

test('STRENGTH_009_rendered_message_adapter_roundtrips_wire_semantics_with_host_only_ids', () => {
  const rendered = {
    Messages: toList([msg('user', [text('hello')]), msg('assistant', [text('world')])]),
    HostMessageIds: toList([undefined, undefined]),
    HostIsPhysical: toList([false, false]),
  }

  const applied = resultOf(MessageEdit.tryApplyRenderedMessages('replica-session', H, rendered))
  assert.equal(applied.ok, true)
  const raw = listItems(applied.value)
  assert.equal(raw.length, 2)
  assert.equal(raw[0].info.sessionID, 'replica-session')
  assert.doesNotMatch(raw[0].info.id, /strength|replica|prefetch/i)

  const decoded = WireCapture.decodeMessageView(applied.value)
  assert.equal(Provider.renderWire(decoded), Provider.renderWire({
    ProviderId: undefined,
    ModelId: undefined,
    Variant: undefined,
    Tools: toList([]),
    System: toList([]),
    Messages: rendered.Messages,
  }))
})

test('STRENGTH_009_host_adapter_encodes_strength_tool_pairs_as_native_completed_OpenCode_parts', () => {
  const rendered = {
    Messages: toList([
      msg('user', [text('owner mirror')]),
      msg('assistant', [
        call('c1', 'read', '{"filePath":"README.md"}'),
        call('c2', 'grep', '{"pattern":"Strength"}'),
      ]),
      msg('tool', [result('c1', 'alpha'), result('c2', 'beta')]),
    ]),
    HostMessageIds: toList([undefined, 'synthetic-call-message', 'synthetic-result-message']),
    HostIsPhysical: toList([false, false, false]),
  }

  const applied = resultOf(MessageEdit.tryApplyStrengthRenderedMessages('replica-session', H, rendered))
  assert.equal(applied.ok, true)
  const raw = listItems(applied.value)
  assert.equal(raw.length, 2, 'one logical tool batch becomes one native completed Host message')
  assert.equal(raw[1].info.role, 'assistant')
  assert.deepEqual(raw[1].parts.map((part) => part.type), ['tool', 'tool'])
  assert.deepEqual(raw[1].parts.map((part) => part.callID), ['c1', 'c2'])
  assert.deepEqual(raw[1].parts.map((part) => part.tool), ['read', 'grep'])
  assert.deepEqual(raw[1].parts.map((part) => part.state.status), ['completed', 'completed'])
  assert.deepEqual(raw[1].parts.map((part) => part.state.input), [
    { filePath: 'README.md' },
    { pattern: 'Strength' },
  ])
  assert.deepEqual(raw[1].parts.map((part) => part.state.output), ['alpha', 'beta'])
})

test('STRENGTH_009_media_mirror_fails_closed_instead_of_reconstructing_from_digest', () => {
  const rendered = {
    Messages: toList([msg('user', [media('image/png', 'digest-only')])]),
    HostMessageIds: toList([undefined]),
    HostIsPhysical: toList([false]),
  }
  const applied = resultOf(MessageEdit.tryApplyRenderedMessages('replica-session', H, rendered))
  assert.equal(applied.ok, false)
  assert.match(applied.error, /media cannot be reconstructed/i)
})
