/**
 * gate-projection-cases.mjs — the mock's projection must not be tautological.
 *
 * VERIFY-007 / ARCH-004. Every scenario edge match and every prefix-seal check
 * ultimately asks "are these two requests the same exchange". If that question
 * ever answers `true` too readily, the whole forest degrades into a device that
 * green-lights any implementation — `design-script-forest.md` §14 names this the
 * worst available outcome, and it fails UPWARD: canaries go green.
 *
 * These cases exist because that exact failure was reached and measured. Package
 * K1's first plan was "delete harness's normaliser, call production's projection
 * directly". Production `Projection.decodeRequest` dispatches on a part's `type`
 * field, and an OpenAI HTTP message has no `parts` array at all:
 *
 *   two entirely different user texts  →  identical renderWire
 *   semanticallyEqual(A, B)            →  true
 *
 * Nothing threw. Every canary would have passed while matching every edge against
 * every request with the same role sequence. `provider-wire.js` is the corrected
 * form — a decoder per wire format, one set of questions — and this file is what
 * keeps the tautology from returning.
 *
 * The shape of every case below is the same: change ONE thing that the model
 * genuinely saw, and require the projections to disagree.
 */

import { assertTrue } from './lib.mjs';
import {
  renderWire,
  sealHolds,
  semanticOf,
  semanticallyEqual,
  wireOf,
} from '../../e2e/support/provider-wire.js';

// ── fixtures ────────────────────────────────────────────────────────────────

const body = (messages, tools = ['fork']) => ({
  model: 'mock/mock-model',
  tools: tools.map((name) => ({ type: 'function', function: { name } })),
  messages,
});

const SYSTEM = { role: 'system', content: 'You are a manager.' };
const user = (text) => ({ role: 'user', content: text });

const assistantCall = ({ id = 'c1', name = 'fork', args = '{}' } = {}) => ({
  role: 'assistant',
  content: null,
  tool_calls: [{ id, type: 'function', function: { name, arguments: args } }],
});

const toolResult = (content, id = 'c1') => ({ role: 'tool', tool_call_id: id, content });

const image = (url) => ({ role: 'user', content: [{ type: 'image_url', image_url: { url } }] });

const chunk = (type, text) => ({ role: 'assistant', content: [{ type, text }] });

/** Semantic equality of two whole bodies — what scenario matching rests on. */
const sameExchange = (left, right) => semanticallyEqual(semanticOf(left), semanticOf(right));

/** Byte equality of the wire projection — what the seal barrier rests on. */
const sameBytes = (left, right) => renderWire(wireOf(left)) === renderWire(wireOf(right));

const differs = (name, left, right) => ({
  name,
  fn: () => {
    assertTrue(!sameExchange(left, right), `${name}: two different exchanges compared EQUAL (tautology)`);
  },
});

const agrees = (name, left, right) => ({
  name,
  fn: () => {
    assertTrue(sameExchange(left, right), `${name}: one exchange compared UNEQUAL to itself`);
  },
});

// ── the cases ───────────────────────────────────────────────────────────────

export const projectionCases = [
  // The measured failure, stated directly. If this ever passes as "equal", the
  // forest is matching on role sequence alone.
  differs(
    'VERIFY-007 different user text is a different exchange',
    body([SYSTEM, user('Do thing A.')]),
    body([SYSTEM, user('Do a completely different thing B.')]),
  ),

  agrees(
    'VERIFY-007 identical bodies are the same exchange',
    body([SYSTEM, user('Do thing A.')]),
    body([SYSTEM, user('Do thing A.')]),
  ),

  // A request is not identified by how many messages it has.
  differs(
    'VERIFY-007 same length with different content differs',
    body([SYSTEM, user('one'), assistantCall(), toolResult('ok')]),
    body([SYSTEM, user('two'), assistantCall(), toolResult('ok')]),
  ),

  // ── tool calls: name, arguments, and canonical form ──────────────────────

  differs(
    'VERIFY-007 different tool arguments differ',
    body([SYSTEM, assistantCall({ args: '{"agent":"fast-coder"}' })]),
    body([SYSTEM, assistantCall({ args: '{"agent":"deep-coder"}' })]),
  ),

  differs(
    'VERIFY-007 different tool name differs',
    body([SYSTEM, assistantCall({ name: 'fork' })]),
    body([SYSTEM, assistantCall({ name: 'join' })]),
  ),

  // Key order is insertion order on the wire and carries no meaning. Without
  // canonicalisation a scenario edge would match or miss depending on how the
  // production JSON serialiser happened to order a record's fields.
  agrees(
    'VERIFY-007 tool argument key order does not change the exchange',
    body([SYSTEM, assistantCall({ args: '{"a":1,"b":2}' })]),
    body([SYSTEM, assistantCall({ args: '{"b":2,"a":1}' })]),
  ),

  // ── the two projections disagree on IDs, and that is the point ───────────

  {
    name: 'VERIFY-007 semantic drops call ids, wire keeps them',
    fn: () => {
      const first = body([SYSTEM, assistantCall({ id: 'call_aaa' })]);
      const second = body([SYSTEM, assistantCall({ id: 'call_bbb' })]);

      // Semantic: a fixture must still match on its second run, when the Host has
      // minted fresh ids.
      assertTrue(sameExchange(first, second), 'semantic projection must ignore call ids');

      // Wire: the seal barrier is about bytes the provider saw, ids included.
      assertTrue(!sameBytes(first, second), 'wire projection must keep call ids');
    },
  },

  // ── tool results ────────────────────────────────────────────────────────

  differs(
    'VERIFY-007 different tool result differs',
    body([SYSTEM, assistantCall(), toolResult('ok')]),
    body([SYSTEM, assistantCall(), toolResult('failed: permission denied')]),
  ),

  // ── multimodal: the digest stands in for the bytes ───────────────────────

  differs(
    'COMPANION-012 different image differs',
    body([SYSTEM, image('data:image/png;base64,AAAA')]),
    body([SYSTEM, image('data:image/png;base64,BBBB')]),
  ),

  agrees(
    'COMPANION-012 the same image is the same exchange',
    body([SYSTEM, image('https://example.test/a.png')]),
    body([SYSTEM, image('https://example.test/a.png')]),
  ),

  // Text and reasoning are both one string. Positional union construction would
  // relabel one as the other and every rendered projection would still be valid.
  differs(
    'VERIFY-007 reasoning is not text',
    body([SYSTEM, chunk('text', 'identical words')]),
    body([SYSTEM, chunk('reasoning', 'identical words')]),
  ),

  // ── an unknown chunk must vanish, not become empty text ─────────────────

  {
    name: 'VERIFY-007 an unrecognised content chunk is dropped, not emptied',
    fn: () => {
      const withUnknown = body([SYSTEM, { role: 'user', content: [{ type: 'video', src: 'x' }] }]);
      const withOther = body([SYSTEM, { role: 'user', content: [{ type: 'audio', src: 'y' }] }]);
      const withText = body([SYSTEM, user('hello')]);

      // Both unknown chunks decode to nothing, so they agree with each other —
      // that is honest. What must NOT happen is either of them agreeing with real
      // content, which is what an empty `WireText` placeholder would cause.
      assertTrue(sameExchange(withUnknown, withOther), 'unknown chunks decode to nothing');
      assertTrue(!sameExchange(withUnknown, withText), 'an unknown chunk must not equal real text');
    },
  },

  // ── tools participate in identity ────────────────────────────────────────

  differs(
    'ARCH-004 a different tool set is a different exchange',
    body([SYSTEM, user('go')], ['fork']),
    body([SYSTEM, user('go')], ['fork', 'join']),
  ),

  differs(
    'ARCH-004 tool order is part of the wire identity',
    body([SYSTEM, user('go')], ['fork', 'join']),
    body([SYSTEM, user('go')], ['join', 'fork']),
  ),

  // ── the seal barrier ────────────────────────────────────────────────────

  {
    name: 'ARCH-004 the seal admits append-only growth and nothing else',
    fn: () => {
      const first = body([SYSTEM, user('one')]);
      const grown = body([SYSTEM, user('one'), { role: 'assistant', content: 'a1' }, user('two')]);
      const rewritten = body([SYSTEM, user('CHANGED')]);
      const retooled = body([SYSTEM, user('one')], ['fork', 'join']);

      const sealed = wireOf(first);

      assertTrue(sealHolds(sealed, grown), 'appending must keep the seal');
      assertTrue(sealHolds(sealed, first), 'an unchanged request must keep the seal');
      assertTrue(!sealHolds(sealed, rewritten), 'rewriting the prefix must break the seal');
      assertTrue(!sealHolds(sealed, retooled), 'changing tools must break the seal');

      // Shrinking is not append-only either. A shorter request means the plugin
      // dropped messages the provider already saw, which is what COMPANION-009's
      // epoch boundary exists to make explicit rather than silent.
      assertTrue(!sealHolds(wireOf(grown), first), 'a shorter request must break the seal');

      // First request in a session: nothing sealed yet.
      assertTrue(sealHolds(null, rewritten), 'no previous seal admits any request');
    },
  },

  // ── the guard against re-introducing a second normaliser ────────────────

  {
    name: 'VERIFY-007 harness asks production, it does not re-implement',
    fn: async () => {
      const production = await import('../../../../../dist/Participant/Provider/Projection/Surface.js');
      const adapter = await import('../../e2e/support/provider-wire.js');

      // The adapter decodes OpenAI bytes; the registered owner consumes its
      // already-decoded JS-native wire messages. Compare both representations
      // against the same production projection instead of reaching into its
      // Fable model module.
      const sample = { ...body([SYSTEM, user('Do thing A.')], []), model: null };
      const ownerMessages = [
        { role: 'system', parts: [{ kind: 'text', text: 'You are a manager.' }] },
        { role: 'user', parts: [{ kind: 'text', text: 'Do thing A.' }] },
      ];
      const ownerSemantic = production.semanticProjection(ownerMessages);

      assertTrue(
        adapter.renderWire(adapter.wireOf(sample)) === production.renderWire(ownerMessages),
        'provider-wire renderWire must agree with the registered projection owner',
      );
      assertTrue(
        adapter.renderSemantic(adapter.semanticOf(sample)) === production.renderSemantic(ownerSemantic),
        'provider-wire semantic rendering must agree with the registered projection owner',
      );
      assertTrue(
        adapter.semanticallyEqual(adapter.semanticOf(sample), adapter.semanticOf(sample)) ===
          production.semanticallyEqual(ownerSemantic, ownerSemantic),
        'provider-wire equality must agree with the registered projection owner',
      );
    },
  },
];
