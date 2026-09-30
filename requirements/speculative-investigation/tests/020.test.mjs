import assert from 'node:assert/strict'
import test from 'node:test'
import fc from 'fast-check'
import * as boundary from '../../../dist/Strength/Replica/TwinBijection.js'

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
