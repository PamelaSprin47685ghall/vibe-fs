import assert from 'node:assert/strict'
import test from 'node:test'

import * as Projection from '../../../dist/Infrastructure/OpenCode/Codec/Projection.js'
import * as Provider from '../../../dist/Domain/ProviderProjection.js'
import { ofArray as toList, toArray as listItems } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'

const H = (text) => `H(${text})`
const resultOf = (value) => value.tag === 0
  ? { ok: true, value: value.fields[0] }
  : { ok: false, error: value.fields[0] }
const text = (value) => new Provider.WirePart(0, [value])
const media = (mime, digest) => new Provider.WirePart(4, [mime, digest])
const msg = (role, parts) => ({ Role: role, Parts: toList(parts) })

test('STRENGTH_009_rendered_message_adapter_roundtrips_wire_semantics_with_host_only_ids', () => {
  const rendered = {
    Messages: toList([msg('user', [text('hello')]), msg('assistant', [text('world')])]),
    HostMessageIds: toList([undefined, undefined]),
    HostIsPhysical: toList([false, false]),
  }

  const applied = resultOf(Projection.tryApplyRenderedMessages('replica-session', H, rendered))
  assert.equal(applied.ok, true)
  const raw = listItems(applied.value)
  assert.equal(raw.length, 2)
  assert.equal(raw[0].info.sessionID, 'replica-session')
  assert.doesNotMatch(raw[0].info.id, /strength|replica|prefetch/i)

  const decoded = Projection.decodeMessageView(applied.value)
  assert.equal(Provider.renderWire(decoded), Provider.renderWire({
    ProviderId: undefined,
    ModelId: undefined,
    Variant: undefined,
    Tools: toList([]),
    System: toList([]),
    Messages: rendered.Messages,
  }))
})

test('STRENGTH_009_media_mirror_fails_closed_instead_of_reconstructing_from_digest', () => {
  const rendered = {
    Messages: toList([msg('user', [media('image/png', 'digest-only')])]),
    HostMessageIds: toList([undefined]),
    HostIsPhysical: toList([false]),
  }
  const applied = resultOf(Projection.tryApplyRenderedMessages('replica-session', H, rendered))
  assert.equal(applied.ok, false)
  assert.match(applied.error, /media cannot be reconstructed/i)
})
