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
 * Which lanes this request could belong to — a SET, not one name.
 *
 * A lane is addressed by the session the Host issued, and aliases exist because a
 * scenario names sessions before they exist (`fast-manager`, `coder-after`). The
 * binding is established when the Host mints the real id; HOST-008 makes the
 * association durable and the mock is told it rather than inferring it.
 *
 * ── why a set ───────────────────────────────────────────────────────────────
 *
 * Measured: every converted scenario binds TWO aliases to its primary session, e.g.
 * `bind = ["inspector-title", "fast-inspector"]`, because the Host titles a session
 * on the same id it chats on. A reverse lookup returning the first match is then
 * decided by Map insertion order, not by the request — the exact impurity this
 * module exists to remove, hidden inside the function that names the key.
 *
 * So membership is the question the request can actually answer: "is this entry's
 * lane one of the aliases bound to this session?" `kind` then separates the title
 * entry from the chat entry, which is what it was added for. Two entries that still
 * tie after that are an author error `resolveEntry` reports rather than resolves.
 */
export function lanesOf(body, bindings) {
  const sessionId = sessionIdOf(body);
  if (sessionId === null) return new Set();

  const lanes = new Set();
  for (const [alias, bound] of bindings ?? []) {
    // A binding is alias → SET of sessions. `fast-reviewer` is not one thread, it is "any
    // reviewer thread": `orchestrator-publish` forks a Reviewer before the rebase and
    // another after it, and both belong to that lane. Their `turn` and `step` tell them
    // apart because each has its own conversation.
    //
    // A plain string is accepted too, so a unit fixture can write `new Map([['a', 'ses']])`.
    const matches = bound instanceof Set ? bound.has(sessionId) : bound === sessionId;
    if (matches) lanes.add(alias);
  }
  return lanes;
}

/**
 * The session id the Host put on this request.
 *
 * ── measured: it is a HEADER, and the body never carries it ─────────────────
 *
 * This function read `body.sessionId ?? body.sessionID` and nothing else, so on a real
 * request it returned `null` — every lane unbound, every request unmatched. The gate
 * cases did not catch it because their fixtures were hand-built objects carrying a
 * `sessionID` field the wire has never had. A test that manufactures the data it is
 * checking for proves only that the checker reads the field the test invented.
 *
 * OpenCode sets three headers on every non-`opencode` provider request
 * (`../opencode/packages/opencode/src/session/llm/request.ts:197`):
 *
 *   x-session-affinity   input.sessionID
 *   X-Session-Id         input.sessionID
 *   x-parent-session-id  input.parentSessionID, when there is one
 *
 * These are PRODUCTION headers, not harness bookkeeping — a real provider receives them,
 * so VERIFY-003 permits reading them. That distinction was blurred by the capture field
 * being named `__testkitHeaders`: the name says "harness", the contents are the wire.
 *
 * `x-opencode-session` is the same value under the first-party provider branch. Both are
 * read because a scenario should not depend on which provider id the Host was configured
 * with.
 */
const HEADER_SESSION_KEYS = ['x-session-affinity', 'x-session-id', 'x-opencode-session'];

export function sessionIdOf(body) {
  const headers = body?.__testkitHeaders ?? {};
  for (const key of HEADER_SESSION_KEYS) {
    const value = headers[key];
    if (typeof value === 'string' && value !== '') return value;
  }

  // Body fields are kept as a fallback so unit fixtures can build a request without
  // spelling headers, but they are NOT what production sends.
  const id = body?.sessionId ?? body?.sessionID ?? null;
  return typeof id === 'string' && id !== '' ? id : null;
}

// ── kind ────────────────────────────────────────────────────────────────────

/**
 * The Host's own title marker, and where it actually appears.
 *
 * ── measured: not at messages[0] ────────────────────────────────────────────
 *
 * The comment this replaces cited `prompt.ts:235` for
 * `messages: [{ role: "user", content: "Generate a title…" }, ...msgs]` and so tested
 * `messages[0].role === 'user'`. The real request is:
 *
 *   roles   ["system", "user", "user"]
 *   [0]     "You are a title generator. You output ONLY a thread title…"
 *   [1]     "Generate a title for this conversation:\n"
 *
 * The title agent's system prompt comes first, so the marker is at [1]. With the old
 * check every title request classified as `chat`, and no title entry could ever match.
 *
 * Both defects here — this one and `sessionIdOf` — were invisible to 175 green gate
 * cases because the fixtures built bodies in the shape the code expected. Hence
 * `gate-lane-cases.mjs`, which posts to a live mock: the shape has to come from the
 * Host, not from the test author's memory of it.
 *
 * Scanning for the marker instead of indexing a fixed position: the number of leading
 * system messages is a Host prompt-assembly detail, and pinning it here would make this
 * function wrong again the next time that assembly changes.
 */
const TITLE_MARKER = 'Generate a title for this conversation:';

/** How far in to look. The marker is a preamble; a real turn cannot push it back. */
const TITLE_PREAMBLE_LIMIT = 4;

/**
 * `title` or `chat`. Read from the marker's presence in the preamble, not from prose
 * matching over the whole conversation.
 *
 * There is deliberately no `synthetic`. The old classifier had one, decided by
 * `NUDGE_MARKERS` — a table of production prompt sentences copied into the mock, which
 * the extinction list condemns as a cross-product dead heuristic. It is unnecessary
 * anyway: a nudge's LAST user message IS the nudge sentence, so `turnOf` already tells
 * it apart from any real turn. Only the title case needs help, because its marker sits
 * in the preamble while `turnOf` looks at the end.
 */
export function kindOf(body) {
  const messages = Array.isArray(body?.messages) ? body.messages : [];

  for (const message of messages.slice(0, TITLE_PREAMBLE_LIMIT)) {
    const content = message?.content;
    if (typeof content === 'string' && content.startsWith(TITLE_MARKER)) return 'title';
  }
  return 'chat';
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

/**
 * The whole key.
 *
 * `lanes` is plural for the reason `lanesOf` explains. The diagnostic field `lane` is
 * the sorted join, so an unmatched-request report names every alias the session holds
 * instead of one arbitrarily chosen member.
 */
export function runtimeKeyOf(body, bindings) {
  const lanes = lanesOf(body, bindings);
  return {
    lanes,
    lane: lanes.size === 0 ? null : [...lanes].sort().join('|'),
    kind: kindOf(body),
    turn: turnOf(body),
    step: stepOf(body),
  };
}

// ── longest-prefix lookup ───────────────────────────────────────────────────

/**
 * Which declared turns this request's turn begins with, longest first.
 *
 * A declaration is a semantic-text prefix. `null` turn (no user message at all)
 * matches nothing — a request with no user message cannot begin with any declared
 * user text, and admitting it would make every scenario match a bare continuation.
 */
/**
 * A declaration's fragments, in order. A plain string is a one-fragment declaration.
 *
 * ── why more than one fragment exists ───────────────────────────────────────
 *
 * Measured in K9 against REVIEW-002. When a Manager forks a Reviewer, production does not
 * send the Manager's assignment as the prompt — it WRAPS it
 * (`../next/Session/HostForkRuntimeFork.fs:98`):
 *
 *   [Original user requirements — authoritative review scope]
 *   …verified HumanRoot prompts since the prior review…
 *
 *   User prompt 1:
 *   <the human's actual prompt>
 *
 *   [Manager review request — supplementary]
 *   <the assignment the scenario declared>
 *
 * So the text a scenario knows sits at the END. A single prefix cannot reach it, and
 * declaring only the wrapper is worse than useless: `manager-full-loop` forks a Reviewer
 * twice (a REVISE round, then a dual-PERFECT round) and both requests share that wrapper
 * byte for byte. One prefix declaration would make them the same key, and the load-time
 * duplicate check would rightly refuse to guess which response belongs to which.
 *
 * ── why this is not `containsText` coming back ──────────────────────────────
 *
 * The retired `containsText` was an UNORDERED BAG of substrings, each free to match
 * anywhere. Three properties separate this from it:
 *
 *   anchored    fragment 0 must be a true prefix of the turn
 *   ordered     each later fragment must occur after the previous one ends
 *   additive    the uniqueness weight is total fragment length, so "more declared text"
 *               still means "more specific" and ties are still author errors
 *
 * A bag has none of the three, which is why it needed `specificity` scoring to choose
 * between overlapping matches. This is the same longest-prefix rule with a hole punched
 * through the middle at a point the author names explicitly.
 */
export const turnFragments = (turn) => (Array.isArray(turn) ? turn : [turn]);

/**
 * How much declared text matches, or `null` when the declaration does not apply.
 *
 * The weight is what replaces the old prefix `.length`, and it must be the SUM rather than
 * the span: measuring "distance from start of first to end of last" would make a
 * declaration look more specific for skipping more text it never named.
 */
const matchWeight = (turn, declaration) => {
  const fragments = turnFragments(declaration.turn);
  if (!turn.startsWith(fragments[0])) return null;

  let cursor = fragments[0].length;
  for (const fragment of fragments.slice(1)) {
    const found = turn.indexOf(fragment, cursor);
    if (found < 0) return null;
    cursor = found + fragment.length;
  }

  return fragments.reduce((total, fragment) => total + fragment.length, 0);
};

const prefixMatches = (turn, declarations) =>
  turn === null
    ? []
    : declarations
        .map((declaration) => ({ declaration, weight: matchWeight(turn, declaration) }))
        .filter(({ weight }) => weight !== null)
        .sort((left, right) => right.weight - left.weight);

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
      (entry.lane === undefined || key.lanes.has(entry.lane)) &&
      (entry.kind ?? 'chat') === key.kind &&
      entry.step === key.step,
  );

  const matches = prefixMatches(key.turn, atKey);
  if (matches.length === 0) {
    return { unmatched: { key, candidates: atKey } };
  }

  const heaviest = matches[0].weight;
  const tied = matches.filter((match) => match.weight === heaviest);
  if (tied.length > 1) {
    return { ambiguousTurn: tied.map((match) => match.declaration), key };
  }

  return { matched: matches[0].declaration, key };
}
