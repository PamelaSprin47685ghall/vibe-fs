import assert from 'node:assert/strict'
import test from 'node:test'
import fc from 'fast-check'
import * as boundary from '../../../dist/Strength/Surface.js'
import * as projection from '../../../dist/Participant/Provider/Projection/Surface.js'

// The class exposes a plain-object boundary; the pure F# functions are what it
// wraps, and the properties below are stated against that boundary.
const { TwinBijectionSurface_restore: restore,
        TwinBijectionSurface_preservesOwnerOrder: preservesOwnerOrder,
        TwinBijectionSurface_introducesNothing: introducesNothing,
        TwinBijectionSurface_dropsNoSpeech: dropsNoSpeech,
        TwinBijectionSurface_extensionIsPrefix: extensionIsPrefix } = boundary

const isSpeech = (message) => message.parts.every((part) => part.kind === 'text' || part.kind === 'reasoning')
const hasToolCall = (message) => message.parts.some((part) => part.kind === 'tool-call')

// The main <-> predictor bijection is isolated in one class. These properties are
// its proof made testable: the owner transcript is the skeleton, the replica's own
// speech is restored into the recorded gaps, and append-only growth preserves the
// provider's cached prefix.

// A tool-carrying owner message: exactly what the projection puts into main.
const ownerExchange = (ordinal) => [
  { role: 'assistant', parts: [{ kind: 'tool-call', callId: `c${ordinal}`, name: 'grep', args: '{}' }] },
  { role: 'tool', parts: [{ kind: 'tool-result', callId: `c${ordinal}`, result: `r${ordinal}` }] },
]

// A child tool exchange: the same shape, as the replica itself produced it.
const childExchange = (ordinal) => [
  { role: 'assistant', parts: [{ kind: 'tool-call', callId: `p${ordinal}`, name: 'js-predictor', args: '{}' }] },
  { role: 'tool', parts: [{ kind: 'tool-result', callId: `p${ordinal}`, result: `q${ordinal}` }] },
]

const speech = (text) => ({ role: 'assistant', parts: [{ kind: 'text', text }] })

test('WHAT[speculative-investigation-020] synchronized text returns to predictor once while ordinary main content stays unchanged', () => {
  const privateReasoning = { kind: 'reasoning', text: 'predictor private thinking' }
  const childSpeech = { role: 'assistant', parts: [privateReasoning, { kind: 'text', text: 'finding' }] }
  const mainText = speech('main response')
  const mainReasoning = { role: 'assistant', parts: [{ kind: 'reasoning', text: 'finding' }] }
  const owner = [
    ...ownerExchange(0),
    { role: 'assistant', parts: [{ kind: 'reasoning', text: 'finding' }] },
    mainText, mainReasoning,
  ]
  const child = [...childExchange(0), childSpeech]
  assert.deepEqual(restore(child, owner, [2]), [
    ...ownerExchange(0), childSpeech, mainText, mainReasoning,
  ])
  const laterOwner = owner.concat(ownerExchange(1))
  const laterChild = child.concat(childExchange(1))
  const restored = restore(laterChild, laterOwner, [2])
  assert.deepEqual(restored, [
    ...ownerExchange(0), childSpeech, mainText, mainReasoning, ...ownerExchange(1),
  ])
  assert.deepEqual(restored.slice(0, 5), restore(child, owner, [2]))
})

test('WHAT[speculative-investigation-020] repeated synchronized text keeps occurrences and mixed tool text keeps its original kind', () => {
  const child = [speech('same'), ...childExchange(0), speech('same')]
  const owner = [
    { role: 'assistant', parts: [{ kind: 'reasoning', text: 'same' }] },
    ...ownerExchange(0),
    { role: 'assistant', parts: [{ kind: 'reasoning', text: 'same' }] },
  ]
  assert.deepEqual(restore(child, owner, [0, 3]), [speech('same'), ...ownerExchange(0), speech('same')])
  const mixed = ownerExchange(0)
  mixed[0].parts.unshift({ kind: 'reasoning', text: 'tool preface' })
  const restored = restore([
    { role: 'assistant', parts: [{ kind: 'text', text: 'tool preface' }, ...childExchange(0)[0].parts] },
    childExchange(0)[1],
  ], mixed, [0])
  assert.deepEqual(restored[0].parts, [{ kind: 'text', text: 'tool preface' }, ...ownerExchange(0)[0].parts])
})

test('WHAT[speculative-investigation-020] projected text roundtrips with private thinking and append-only prefix stability', () => {
  fc.assert(fc.property(fc.array(fc.string(), { minLength: 2, maxLength: 6 }), texts => {
    const child = texts.flatMap((text, index) => {
      const exchange = childExchange(index)
      exchange[0].parts.unshift({ kind: 'reasoning', text: `private-${index}` }, { kind: 'text', text })
      return exchange
    })
    const owner = texts.flatMap((_text, index) => {
      const batches = boundary.collectCompleteBatches(child.slice(index * 2, index * 2 + 2))
      const bundle = boundary.frameTryBuild(value => `H(${value})`, batches)
      assert.equal(bundle.ok, true, bundle.error)
      const candidate = boundary.candidate(value => `H(${value})`, {
        ownerSessionId: 'owner', ownerRole: 'engineer', decisionId: `decision-${index}`,
        targetProviderRun: `target-${index}`, currentProviderRun: `target-${index}`, bundle: bundle.value,
      })
      assert.equal(candidate.ok, true, candidate.error)
      return projection.renderMessages(projection.projectionSnapshot(projection.semanticProjection([])), [], [candidate.value])
    })
    const coordinates = owner.map((_message, index) => index).filter(index => index % 2 === 0)
    const first = restore(child.slice(0, -2), owner.slice(0, -2), coordinates.slice(0, -1))
    const full = restore(child, owner, coordinates)
    assert.deepEqual(full.slice(0, first.length), first)
    assert.deepEqual(full.flatMap(message => message.parts).filter(part => part.kind === 'text').map(part => part.text), texts)
    assert.deepEqual(full.flatMap(message => message.parts).filter(part => part.kind === 'reasoning').map(part => part.text),
      texts.map((_text, index) => `private-${index}`))
    assert.deepEqual(owner.flatMap(message => message.parts).filter(part => part.kind === 'reasoning').map(part => part.text), texts,
      'main contains demoted text, not native thinking')
  }), { seed: 20261003 })
})

test('WHAT[speculative-investigation-020] owner_sequence_survives_restoration_unchanged', () => {
  fc.assert(
    fc.property(
      fc.array(fc.string({ minLength: 1 }), { maxLength: 6 }),
      fc.array(fc.integer({ min: 0, max: 6 }), { maxLength: 8 }),
      (texts, gaps) => {
        const owner = gaps.flatMap(ownerExchange)
        const child = gaps.flatMap((gap, index) => [
          ...(texts[index] ? [speech(`${gap}:${texts[index]}`)] : []),
          ...childExchange(gap),
        ])
        assert.equal(preservesOwnerOrder(child, owner), true)
      },
    ),
  )
})

test('WHAT[speculative-investigation-020] restoration_drops_no_speech_and_fabricates_nothing', () => {
  fc.assert(
    fc.property(
      fc.array(fc.string({ minLength: 1 }), { maxLength: 8 }),
      fc.array(fc.integer({ min: 0, max: 5 }), { maxLength: 8 }),
      (texts, gaps) => {
        const owner = gaps.flatMap(ownerExchange)
        const child = [
          ...texts.map((text) => speech(text)),
          ...gaps.flatMap(childExchange),
        ]
        assert.equal(dropsNoSpeech(child, owner), true)
        assert.equal(introducesNothing(child, owner), true)
      },
    ),
  )
})

test('WHAT[speculative-investigation-020] speech_is_restored_before_the_owner_exchange_of_its_gap', () => {
  fc.assert(
    fc.property(fc.integer({ min: 0, max: 4 }), fc.string({ minLength: 1 }), (gap, text) => {
      const owner = [0, 1, 2, 3, 4].flatMap(ownerExchange)
      // speech recorded after `gap` child exchanges belongs before owner exchange `gap`.
      const child = [
        ...[...Array(gap).keys()].flatMap(childExchange),
        speech(text),
      ]
      const restored = restore(child, owner)
      const speechAt = restored.findIndex((message) => isSpeech(message))
      const ownerCalls = restored
        .map((message, index) => [index, message])
        .filter(([, message]) => hasToolCall(message))
      const anchor = gap < ownerCalls.length ? ownerCalls[gap][0] : restored.length
      assert.equal(speechAt < anchor, true)
    }),
  )
})

test('WHAT[speculative-investigation-020] append_only_growth_reproduces_the_earlier_request_as_a_prefix', () => {
  fc.assert(
    fc.property(
      fc.array(fc.string({ minLength: 1 }), { maxLength: 4 }),
      fc.array(fc.string({ minLength: 1 }), { maxLength: 4 }),
      fc.integer({ min: 0, max: 3 }),
      (before, after, extraOwner) => {
        const exchanges = [0, 1, 2, 3]
        const ownerBefore = exchanges.flatMap(ownerExchange)
        const childBefore = [
          ...before.map(speech),
          ...exchanges.flatMap(childExchange),
        ]
        // The next round only ever appends: more owner exchanges and more speech.
        const ownerAfter = ownerBefore.concat(
          [...Array(extraOwner).keys()].map((n) => ownerExchange(10 + n)).flat(),
        )
        const childAfter = childBefore
          .concat(after.map(speech))
          .concat([...Array(extraOwner).keys()].flatMap((n) => childExchange(20 + n)))
        assert.equal(
          extensionIsPrefix(childBefore, ownerBefore, childAfter, ownerAfter),
          true,
        )
      },
    ),
  )
})

test('WHAT[speculative-investigation-020] unbalanced_histories_still_yield_a_usable_request', () => {
  fc.assert(
    fc.property(fc.array(fc.string({ minLength: 1 }), { maxLength: 5 }), (texts) => {
      const owner = [0, 1].flatMap(ownerExchange)
      // More child exchanges than owner exchanges: nothing may be dropped or thrown.
      const child = [0, 1, 2, 3].flatMap(childExchange).concat(texts.map(speech))
      const restored = restore(child, owner)
      assert.equal(preservesOwnerOrder(child, owner), true)
      assert.equal(dropsNoSpeech(child, owner), true)
    }),
  )
})
