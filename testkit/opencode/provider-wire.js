/**
 * provider-wire.js — OpenAI HTTP wire body → production `ProviderWireProjection`.
 *
 * VERIFY-007 has exactly two projections and they live in
 * `src/Wanxiangshu.Next/Domain/ProviderProjection.fs`. This file is NOT a third one: it decodes a
 * wire format and then asks the production projection every question. Nothing here
 * compares, normalises or digests — those all come from `build/next`.
 *
 * ── why an adapter is still needed ──────────────────────────────────────────
 *
 * Two different byte formats carry the same conversation:
 *
 *   Host raw      parts: [{ type: "text", text: "..." }]      ← production transform
 *   OpenAI HTTP   content: "...", tool_calls: [{ id, ... }]   ← what this mock receives
 *
 * Production's `Projection.decodeRequest` dispatches on a part's `type` field, so
 * an OpenAI message — which has no `parts` array at all — decodes to zero parts and
 * keeps only its `Role`. Feeding OpenAI bodies to it therefore makes ANY two
 * requests with the same role sequence compare equal.
 *
 * That failure is silent and it points the wrong way: every scenario edge would
 * match every request, every prefix seal would hold, and the canaries would go
 * green. `design-script-forest.md` §14 calls this the worst outcome available — a
 * verification device that green-lights a wrong implementation. `provider-wire-
 * tautology.mjs` is the test that keeps it from coming back.
 *
 * So: one decoder per wire format (they really are two formats), one set of
 * questions (there really is one meaning).
 */

import { canonicalJson } from '../../build/next/OpenCode/CanonicalJson.js';
import { sha256Hex } from '../../build/next/Host/HostDigest.js';
import { ToolCallIdModule_create as toolCallId } from '../../build/next/Kernel/Identity.js';
import {
  ProviderWireProjection,
  WireMessage,
  WirePart,
  fixtureKey,
  isAppendOnlyPrefix,
  renderSemantic,
  renderWire,
  sealDigest,
  semanticallyEqual,
  toSemantic,
  toolResultDigests,
} from '../../build/next/Domain/ProviderProjection.js';
import { ofArray, toArray as listToArray } from '../../build/next/fable_modules/fable-library-js.5.13.0/List.js';

// ── union construction by case NAME ─────────────────────────────────────────
// Positional construction would silently relabel prose as reasoning: `WireText`
// and `WireReasoning` are both one string, so swapping them yields a projection
// that renders as valid JSON and compares unequal for the wrong reason.

const WIRE_PART_CASES = new WirePart(0, ['']).cases();

const wirePart = (caseName, fields) => {
  const index = WIRE_PART_CASES.indexOf(caseName);
  if (index < 0) {
    throw new Error(`WirePart has no case '${caseName}'. Available: ${WIRE_PART_CASES.join(', ')}`);
  }
  return new WirePart(index, fields);
};

// ── decoding one OpenAI message ─────────────────────────────────────────────

/** A data URL or remote URL is the media's identity; the digest stands in for it. */
const mediaPart = (part) => {
  const url = part?.image_url?.url ?? part?.url ?? null;
  if (typeof url !== 'string' || url === '') return null;
  const mediaType = part?.image_url?.detail ?? part?.mime ?? part?.mediaType ?? undefined;
  return wirePart('WireMedia', [mediaType, sha256Hex(url)]);
};

/**
 * `content` is either a string or an array of typed chunks.
 *
 * An unrecognised chunk is DROPPED rather than rendered as empty text: an empty
 * `WireText` would make every such chunk compare equal to every other, which is
 * the same tautology this file exists to prevent, one level down.
 */
const contentParts = (content) => {
  if (typeof content === 'string') {
    return content === '' ? [] : [wirePart('WireText', [content])];
  }
  if (!Array.isArray(content)) return [];

  return content
    .map((chunk) => {
      if (typeof chunk === 'string') return chunk === '' ? null : wirePart('WireText', [chunk]);
      switch (chunk?.type) {
        case 'text':
          return typeof chunk.text === 'string' && chunk.text !== '' ? wirePart('WireText', [chunk.text]) : null;
        case 'reasoning':
        case 'thinking':
          return typeof chunk.text === 'string' && chunk.text !== ''
            ? wirePart('WireReasoning', [chunk.text])
            : null;
        case 'image_url':
        case 'image':
        case 'file':
          return mediaPart(chunk);
        default:
          return null;
      }
    })
    .filter((part) => part !== null);
};

/** `arguments` is a JSON string on the wire; canonicalise so key order cannot leak. */
const canonicalArguments = (raw) => {
  if (typeof raw !== 'string') return canonicalJson(raw ?? {});
  try {
    return canonicalJson(JSON.parse(raw));
  } catch {
    // Not JSON. The bytes the model produced are the identity, so keep them.
    return raw;
  }
};

const toolCallParts = (message) =>
  (Array.isArray(message?.tool_calls) ? message.tool_calls : [])
    .map((call) => {
      const id = call?.id;
      const name = call?.function?.name ?? call?.name;
      // Both halves are the identity (HOST-011). A call missing either is dropped
      // rather than given a placeholder, which would let "no identity" look real.
      if (typeof id !== 'string' || typeof name !== 'string') return null;
      return wirePart('WireToolCall', [toolCallId(id), name, canonicalArguments(call?.function?.arguments)]);
    })
    .filter((part) => part !== null);

const decodeMessage = (message) => {
  const role = typeof message?.role === 'string' ? message.role.toLowerCase() : '';

  if (role === 'tool') {
    const id = message?.tool_call_id;
    if (typeof id !== 'string') return null;
    const result = typeof message?.content === 'string' ? message.content : canonicalJson(message?.content ?? null);
    return new WireMessage(role, ofArray([wirePart('WireToolResult', [toolCallId(id), result])]));
  }

  const parts = [...contentParts(message?.content), ...toolCallParts(message)];
  if (role === '' && parts.length === 0) return null;
  return new WireMessage(role, ofArray(parts));
};

// ── the one entry point ─────────────────────────────────────────────────────

/**
 * Decode a whole OpenAI chat-completions body.
 *
 * `system` stays a MESSAGE here, unlike production's separate `System` list. That
 * is not a divergence: production keeps it separate because the Host sends
 * `system: string[]` through its own hook, and on the OpenAI wire the system
 * prompt genuinely is `messages[0]`. Each decoder mirrors the bytes it reads,
 * which is what makes `renderWire` mean "these bytes" in both.
 */
export function wireOf(body) {
  const messages = Array.isArray(body?.messages) ? body.messages : [];
  const tools = (Array.isArray(body?.tools) ? body.tools : [])
    .map((tool) => tool?.function?.name ?? tool?.name)
    .filter((name) => typeof name === 'string');

  return new ProviderWireProjection(
    undefined,
    typeof body?.model === 'string' ? body.model : undefined,
    undefined,
    ofArray(tools),
    ofArray([]),
    ofArray(messages.map(decodeMessage).filter((message) => message !== null)),
  );
}

/** VERIFY-007's one-way downgrade, applied to a decoded body. */
export const semanticOf = (body) => toSemantic(wireOf(body));

/** VERIFY-007: the fixture-matching key. Semantic, so it survives a second run. */
export const fixtureKeyOf = (body) => fixtureKey(semanticOf(body))

const SEMANTIC_PART_CASES = ['SemanticText', 'SemanticReasoning', 'SemanticToolCall', 'SemanticToolResult', 'SemanticMedia']

const UNIT = '\u001f'
const RECORD = '\u001e'

/**
 * One semantic part as text a PREFIX comparison can work on.
 *
 * NOT `renderSemantic`: its closed JSON envelope puts `}]}]}` after the text, so no
 * shorter utterance is ever a string prefix of a longer one and VERIFY-003's
 * longest-prefix rule silently degrades to whole-string equality.
 *
 * Prose verbatim; every other kind tagged with `\u001f`, which prose cannot contain.
 */
const partText = (part) => {
  const kind = SEMANTIC_PART_CASES[part.tag]
  const fields = part.fields ?? []

  switch (kind) {
    case 'SemanticText':
      return fields[0]
    case 'SemanticReasoning':
      return `${UNIT}reasoning${UNIT}${fields[0]}`
    case 'SemanticToolCall':
      return `${UNIT}tool-call${UNIT}${fields[0]}${UNIT}${fields[1]}`
    case 'SemanticToolResult':
      return `${UNIT}tool-result${UNIT}${fields[0]}`
    case 'SemanticMedia':
      return `${UNIT}media${UNIT}${fields[0] ?? ''}${UNIT}${fields[1]}`
    default:
      throw new Error(`unknown SemanticPart case at tag ${part.tag}`)
  }
}

/** One message's semantic content, prefix-comparable. Role excluded: the caller selected by it. */
export const messageText = (message) => listToArray(message.Parts).map(partText).join(RECORD);

// ── the production questions, re-exported ───────────────────────────────────
//
// Named exports rather than a namespace object so an accidental local
// re-implementation collides at import time instead of shadowing quietly.

export { fixtureKey, isAppendOnlyPrefix, renderSemantic, renderWire, sealDigest, semanticallyEqual, toSemantic, toolResultDigests };

/**
 * ARCH-004 / COMPANION-009: is `next` an append-only continuation of `previous`.
 *
 * Takes the two WIRE projections, because the seal barrier is about bytes the
 * provider already saw. Replaces testkit's own `isProviderVisiblePrefix`, which
 * hand-compared `JSON.stringify` of tools and then of each message — two
 * comparison rules that could disagree with production's.
 */
export const sealHolds = (previousWire, nextBody) =>
  previousWire === null || previousWire === undefined ? true : isAppendOnlyPrefix(previousWire, wireOf(nextBody));
