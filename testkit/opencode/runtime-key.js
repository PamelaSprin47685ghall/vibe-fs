/**
 * runtime-key.js — the scenario lookup key, as three pure functions of the request.
 *
 * VERIFY-003. Every component is derived from the request alone; nothing here reads
 * or writes state. That is the whole point: the matcher this replaces kept a
 * `pathCursor` per path and a `claimCount` per edge, so the answer depended on how
 * many requests had already arrived rather than on what this request says.
 *
 *   lane   which conversation thread — longest matching head discriminant
 *   turn   the last user message's semantic content — longest-prefix matched
 *   step   how many assistant messages follow that user message
 *
 * `step` is what makes the cursor unnecessary. The Host appends exactly one
 * assistant message per provider step (`../opencode/packages/opencode/src/session/
 * prompt.ts:1186`, `parentID: lastUser.id`), so "which step of this turn" is a
 * countable property of the request. The cursor was bookkeeping for something the
 * request already carried.
 *
 * ── longest prefix, not predicate conjunction ───────────────────────────────
 *
 * The old key was a conjunction of up to eleven predicates (`user`, `userRegex`,
 * `containsText`, `requiredTools`, `forbiddenTools`, `messageCount`, `model`,
 * `sessionId`, `requestKind`, `role`, `afterToolResult`). A conjunction can match
 * several edges at once, which is why a `specificity` score existed — it summed
 * substring lengths and added magic numbers (`afterToolResult === true` → +50) to
 * decide which edge was "more specific".
 *
 * A true prefix cannot be ambiguous. Either the request's turn begins with the
 * declared text or it does not, and among several matches the longest is unique.
 * Two declarations of the SAME length that both match is an author error, not a
 * tie to break: `ambiguousTurn` reports it and the caller fails closed.
 */

import { messageText, semanticOf } from './provider-wire.js';
import { toArray as listToArray } from '../../build/next/fable_modules/fable-library-js.5.13.0/List.js';

// ── lane ────────────────────────────────────────────────────────────────────

/**
 * Which conversation thread this request belongs to.
 *
 * A lane is addressed by the session the Host issued. Aliases exist because a
 * scenario names sessions before they exist (`fast-manager`, `coder-after`), and
 * the binding is established when the Host mints the real id.
 *
 * Returns `null` for an unbound session rather than guessing. HOST-008 makes the
 * association durable; a mock inventing one would answer a question it cannot know.
 */
export function laneOf(body, bindings) {
  const sessionId = sessionIdOf(body);
  if (sessionId === null) return null;

  for (const [alias, bound] of bindings ?? []) {
    if (bound === sessionId) return alias;
  }
  return null;
}

/**
 * The session id the Host put on this request.
 *
 * `__testkitHeaders` is deliberately absent. Reading harness-injected headers here
 * would let the mock answer from its own bookkeeping instead of from the wire, and
 * VERIFY-003 forbids the mock observing anything the provider cannot see.
 */
export function sessionIdOf(body) {
  const id = body?.sessionId ?? body?.sessionID ?? null;
  return typeof id === 'string' && id !== '' ? id : null;
}

// ── kind ────────────────────────────────────────────────────────────────────

/**
 * The Host's own title marker, prepended as the FIRST message.
 *
 * `../opencode/packages/opencode/src/session/prompt.ts:235`
 *   messages: [{ role: "user", content: "Generate a title for this conversation:\n" }, ...msgs]
 *
 * So a title request carries the whole conversation after the marker, and `turnOf`
 * reads the LAST user message — which is the same text the ordinary chat request for
 * that turn carries. Measured: the two produce an identical `turn` and an identical
 * `step`, so without a fourth component a title edge and a chat edge collide.
 */
const TITLE_MARKER = 'Generate a title for this conversation:';

/**
 * `title` or `chat`. Read from position, not from prose matching.
 *
 * There is deliberately no `synthetic`. The old classifier had one, decided by
 * `NUDGE_MARKERS` — a table of production prompt sentences copied into the mock, which
 * the extinction list condemns as a cross-product dead heuristic. It is unnecessary
 * anyway: a nudge's LAST user message IS the nudge sentence, so `turnOf` already tells
 * it apart from any real turn. Only the title case needs help, because its marker sits
 * at `messages[0]` while `turnOf` looks at the end.
 */
export function kindOf(body) {
  const first = Array.isArray(body?.messages) ? body.messages[0] : undefined;
  if (first?.role !== 'user') return 'chat';

  const content = first.content;
  const text = typeof content === 'string' ? content : '';
  return text.startsWith(TITLE_MARKER) ? 'title' : 'chat';
}

// ── turn ────────────────────────────────────────────────────────────────────

const isUser = (message) => message?.role === 'user';
const isAssistant = (message) => message?.role === 'assistant';

const lastUserIndex = (messages) => {
  for (let index = messages.length - 1; index >= 0; index -= 1) {
    if (isUser(messages[index])) return index;
  }
  return -1;
};

/**
 * The last user message, as prefix-comparable semantic text.
 *
 * Goes through the production semantic projection, so a scenario matches exactly
 * what VERIFY-007 says the exchange meant — ids and runtime metadata excluded,
 * multimodal parts reduced to their digest.
 *
 * No truncation. The old `extractLastUserMsg` sliced at 2000 characters, which made
 * two long prompts identical whenever they differed only past the cut — and a prompt
 * long enough to be truncated is exactly the kind a scenario needs to tell apart.
 */
export function turnOf(body) {
  const messages = Array.isArray(body?.messages) ? body.messages : [];
  const index = lastUserIndex(messages);
  if (index < 0) return null;

  const projected = listToArray(semanticOf({ messages: [messages[index]] }).Messages);
  return projected.length === 0 ? null : messageText(projected[0]);
}

// ── step ────────────────────────────────────────────────────────────────────

/**
 * How many assistant messages follow the last user message.
 *
 * Zero on the first provider step of a turn, one after the model's first reply, and
 * so on. Counted from the request, never accumulated.
 */
export function stepOf(body) {
  const messages = Array.isArray(body?.messages) ? body.messages : [];
  const index = lastUserIndex(messages);
  if (index < 0) return 0;

  let step = 0;
  for (let cursor = index + 1; cursor < messages.length; cursor += 1) {
    if (isAssistant(messages[cursor])) step += 1;
  }
  return step;
}

/** The whole key. */
export function runtimeKeyOf(body, bindings) {
  return { lane: laneOf(body, bindings), kind: kindOf(body), turn: turnOf(body), step: stepOf(body) };
}

// ── longest-prefix lookup ───────────────────────────────────────────────────

/**
 * Which declared turns this request's turn begins with, longest first.
 *
 * A declaration is a semantic-text prefix. `null` turn (no user message at all)
 * matches nothing — a request with no user message cannot begin with any declared
 * user text, and admitting it would make every scenario match a bare continuation.
 */
const prefixMatches = (turn, declarations) =>
  turn === null
    ? []
    : declarations
        .filter((declaration) => turn.startsWith(declaration.turn))
        .sort((left, right) => right.turn.length - left.turn.length);

/**
 * Resolve a request against declared (lane, turn, step) entries.
 *
 * Returns one of three shapes, never a scored best guess:
 *
 *   { matched: entry }              exactly one longest prefix at this lane+step
 *   { ambiguousTurn: [...] }        two declarations of equal length both match
 *   { unmatched: { key, ... } }     nothing declared for this request
 *
 * `ambiguousTurn` is a load-time author error surfaced at runtime. It cannot be
 * resolved by picking one: two same-length prefixes that both match describe the
 * same point in the conversation with two different responses, so the scenario does
 * not say what the model does next.
 */
export function resolveEntry(body, entries, bindings) {
  const key = runtimeKeyOf(body, bindings);

  const atKey = entries.filter(
    (entry) =>
      (entry.lane === undefined || entry.lane === key.lane) &&
      (entry.kind ?? 'chat') === key.kind &&
      entry.step === key.step,
  );

  const matches = prefixMatches(key.turn, atKey);
  if (matches.length === 0) {
    return { unmatched: { key, candidates: atKey } };
  }

  const longest = matches[0].turn.length;
  const tied = matches.filter((entry) => entry.turn.length === longest);
  if (tied.length > 1) {
    return { ambiguousTurn: tied, key };
  }

  return { matched: matches[0], key };
}
