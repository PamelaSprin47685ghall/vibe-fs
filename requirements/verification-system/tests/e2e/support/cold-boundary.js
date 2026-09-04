/**
 * cold-boundary.js — explicit declared exceptions to the prefix seal.
 *
 * ARCH-004 keeps the provider-visible prefix byte-stable so KV-cache hits. VERIFY-003
 * §"冷边界显式声明" names the only two legitimate exceptions and requires the scenario
 * to say WHERE each happens:
 *
 *   COMPANION-009  epoch switch — new SealRoot, one explicit prefix rebase
 *   FALLBACK-004   fallback side switch — EffectiveAgent moves, so the model does
 *   RELAY-PROJ    Relay typed context — open, phase revision, retirement, and
 *                 successor cut each have their own kind
 *
 * Sniffing is forbidden, and package K1 measured why. The deleted `epochCold`
 * exemption read "tools and the leading system message unchanged" and then admitted
 * ANY body rewrite — which is most of what a wrong prefix replacement looks like. It
 * passed exactly the mutations it existed to catch.
 *
 * ── the fallback boundary is narrower than the old exemption claimed ─────────
 *
 * `modelSideCold` allowed the system prompt to change whenever the model id did.
 * Measured against production: AGENT-001 gives `fast-ROLE` and `deep-ROLE` ONE system
 * prompt, byte-identical (verified for coder/manager/reviewer/devops/inspector). So a
 * fallback switch changes the model field and nothing else — the message prefix stays
 * append-only.
 *
 * That makes `FallbackSide` a far tighter admission than a prefix rebase: messages
 * must still satisfy the ordinary seal, and only the model may move. A scenario that
 * declares it cannot use it to smuggle a message rewrite past the barrier.
 */

import { isDeepStrictEqual } from 'node:util';
import { isAppendOnlyPrefix, wireOf } from './provider-wire.js';

export const BOUNDARY_KINDS = [
  'epoch-switch',
  'fallback-side',
  'prefix-probe',
  'frame-commit',
  'request-kind-switch',
  'relay-context-open',
  'relay-context-revision',
  'relay-retirement-context',
  'relay-successor-cut',
];

// ── declaration lookup ──────────────────────────────────────────────────────

/**
 * Which boundary is declared AT this ENTRY, or `null`.
 *
 * A declaration names the point the break is expected at, so it is consumed by the
 * request that breaks the seal — not by the one before it.
 *
 * Keyed by entry id for the reason `faultFor` documents: this compared the DECLARED turn
 * text against the REQUEST text, and a declaration is a prefix, so the two matched only
 * when the author wrote the utterance out in full. Every cold boundary in every real
 * scenario was inert. `resolveEntry` has already chosen the declaration; asking again
 * with a weaker comparison could only disagree with it.
 */
export function boundaryFor(boundaries, entry) {
  if (entry === null || entry === undefined) return null;

  const matches = (boundaries ?? []).filter((boundary) => boundary.entryId === entry.id);

  if (matches.length === 0) return null;
  if (matches.length > 1) {
    throw new Error(
      `two cold boundaries declared for the same step '${entry.id}': ${matches.map((b) => b.kind).join(', ')}`,
    );
  }
  return matches[0];
}

// ── the seal decision ───────────────────────────────────────────────────────

/**
 * Whether the messages alone are an append-only continuation, ignoring the model.
 *
 * `isAppendOnlyPrefix` compares model fields too, which is right for the ordinary
 * seal — a model change is a real cache break. `FALLBACK-004` is the one case where
 * that break is expected while the transcript must still be intact, so the two
 * questions have to be separable.
 */
const messagesStillAppendOnly = (previousWire, nextWire) =>
  isAppendOnlyPrefix(withModelOf(previousWire, nextWire), nextWire);

/** SyncDelegate Returned→Completion keeps model/system/messages and replaces only tools. */
const requestKindKeepsPrefix = (previousWire, nextWire) =>
  isAppendOnlyPrefix({ ...previousWire, tools: nextWire.tools }, nextWire);

/** `previous` with `next`'s model fields, so only the messages/tools/system differ. */
const withModelOf = (previousWire, nextWire) => ({
  ...previousWire,
  providerId: nextWire.providerId,
  modelId: nextWire.modelId,
  variant: nextWire.variant,
});

/**
 * CTX-010 probe admission: the tool set is a fixed part of the attempt
 * (PROMPT-008), everything else may move.
 *
 * The system prompt is deliberately NOT compared, and this is a measured Host
 * fact rather than a compromise: Host 1.18.9 injects the model name into the
 * system prompt (`../opencode/packages/opencode/src/session/system.ts:67`:
 * "You are powered by the model named ${model.api.id}…"), so a fallback side
 * switch — which is exactly what accompanies a recovery attempt — changes the
 * system bytes by construction. The probe's own claim is about the MESSAGE
 * prefix; the declaration admits the whole recovery request.
 */
const probeKeepsFixedParts = (previousWire, nextWire) => isDeepStrictEqual(previousWire.tools, nextWire.tools);

// ── Relay typed provider context ────────────────────────────────────────────
//
// The Relay projection prepends a synthetic `[RelayContext]` user message once
// durable Road state exists, revises it as incumbency and phase advance, marks
// it retired with the accepted suicide result, and replaces it for the
// successor. Each shape is a distinct boundary kind so a revision cannot
// masquerade as a cut and a cut cannot smuggle rewritten history.

const messageIsRelayContext = (message) =>
  message?.role === 'user'
  && (message?.parts ?? []).some((part) => part?.kind === 'text' && String(part?.text ?? '').includes('[RelayContext]'));

const relayContexts = (wire) => (wire.messages ?? []).filter(messageIsRelayContext);

const relayContextText = (message) =>
  (message?.parts ?? []).filter((part) => part?.kind === 'text').map((part) => String(part?.text ?? '')).join('\n');

const relayContextField = (message, key) => {
  const line = relayContextText(message).split('\n').find((entry) => entry.startsWith(`${key}=`));
  return line === undefined ? null : line.slice(key.length + 1);
};

const sameProviderPlan = (previousWire, nextWire) =>
  previousWire.modelId === nextWire.modelId && isDeepStrictEqual(previousWire.tools, nextWire.tools);

const withoutRelayContexts = (wire) => ({
  ...wire,
  messages: (wire.messages ?? []).filter((message) => !messageIsRelayContext(message)),
});

const preservesPriorMessagesAsSubsequence = (previousWire, nextWire) => {
  let cursor = 0;
  for (const message of nextWire.messages ?? []) {
    if (cursor < (previousWire.messages ?? []).length
        && isDeepStrictEqual(message, previousWire.messages[cursor])) {
      cursor += 1;
    }
  }
  return cursor === (previousWire.messages ?? []).length;
};

const messagesAreOrderedSubsequence = (subset, superset) => {
  let cursor = 0;
  for (const message of superset) {
    if (cursor < subset.length && isDeepStrictEqual(message, subset[cursor])) {
      cursor += 1;
    }
  }
  return cursor === subset.length;
};

/**
 * Relay opens its typed provider context only after the first durable Road state
 * exists. The next request therefore prepends a synthetic `[RelayContext]` message
 * and may interleave materialized assessment/tool evidence around the already-seen
 * transcript. This boundary is deliberately narrower than an epoch reset: the
 * provider plan is byte-stable and every prior message must remain, in order.
 */
const relayContextOpened = (previousWire, nextWire) =>
  sameProviderPlan(previousWire, nextWire)
  && (nextWire.messages ?? []).some(messageIsRelayContext)
  && !(previousWire.messages ?? []).some(messageIsRelayContext)
  && preservesPriorMessagesAsSubsequence(previousWire, nextWire);

const relayContextRevised = (previousWire, nextWire) => {
  const previousContexts = relayContexts(previousWire);
  const nextContexts = relayContexts(nextWire);
  if (previousContexts.length !== 1 || nextContexts.length !== 1) return false;
  const previousIncumbency = relayContextField(previousContexts[0], 'incumbency_id');
  const nextIncumbency = relayContextField(nextContexts[0], 'incumbency_id');
  return sameProviderPlan(previousWire, nextWire)
    && previousIncumbency !== null
    && previousIncumbency === nextIncumbency
    && relayContextText(previousContexts[0]) !== relayContextText(nextContexts[0])
    && preservesPriorMessagesAsSubsequence(withoutRelayContexts(previousWire), withoutRelayContexts(nextWire));
};

const hasAcceptedSuicideResult = (wire) =>
  (wire.messages ?? []).some((message) =>
    message?.role === 'tool'
    && message?.parts?.some(
      (part) => part?.kind === 'tool-result'
        && String(part?.result ?? '').includes('retired = true'),
    ));

const relayRetirementContext = (previousWire, nextWire) => {
  const previousContexts = relayContexts(previousWire);
  const nextContexts = relayContexts(nextWire);
  if (previousContexts.length !== 1 || nextContexts.length !== 1) return false;
  const previousIncumbency = relayContextField(previousContexts[0], 'incumbency_id');
  const nextIncumbency = relayContextField(nextContexts[0], 'incumbency_id');
  const nextPhase = relayContextField(nextContexts[0], 'phase');
  return sameProviderPlan(previousWire, nextWire)
    && previousIncumbency !== null
    && previousIncumbency !== 'none'
    && nextIncumbency === 'none'
    && nextPhase === 'Retired'
    && hasAcceptedSuicideResult(nextWire)
    && preservesPriorMessagesAsSubsequence(withoutRelayContexts(previousWire), withoutRelayContexts(nextWire));
};

const messageIsRelaySuccessorPrompt = (message) =>
  message?.role === 'user'
  && message?.parts?.some((part) => {
    if (part?.kind !== 'text') return false;
    const text = String(part?.text ?? '').replace(/^#\s*/, '');
    return text.startsWith('The previous Manager incumbency is retired. You are the new Manager');
  });

const hasRelaySuccessorPrompt = (wire) => (wire.messages ?? []).some(messageIsRelaySuccessorPrompt);

const isSanitizerAssistantDot = (message) =>
  message?.role === 'assistant'
  && (message?.parts ?? []).length === 1
  && message.parts[0]?.kind === 'text'
  && message.parts[0]?.text === '.';

const relaySuccessorCut = (previousWire, nextWire) => {
  const nextContexts = relayContexts(nextWire);
  if (nextContexts.length !== 1) return false;
  const nextIncumbency = relayContextField(nextContexts[0], 'incumbency_id');
  const nextPhase = relayContextField(nextContexts[0], 'phase');
  const priorMessages = (previousWire.messages ?? []).filter(
    (message) => !messageIsRelayContext(message) && !isSanitizerAssistantDot(message),
  );
  const carriedMessages = (nextWire.messages ?? []).filter(
    (message) =>
      !messageIsRelayContext(message)
      && !messageIsRelaySuccessorPrompt(message)
      && !isSanitizerAssistantDot(message),
  );
  return sameProviderPlan(previousWire, nextWire)
    && nextIncumbency !== null
    && nextIncumbency !== 'none'
    && nextPhase === 'AuditPending'
    && hasRelaySuccessorPrompt(nextWire)
    && !hasAcceptedSuicideResult(nextWire)
    && messagesAreOrderedSubsequence(carriedMessages, priorMessages);
};

/**
 * Decide one chat request against the session's seal.
 *
 * Four outcomes, and the caller must treat them as four:
 *
 *   { held: true }                      the ordinary case, seal intact
 *   { resealed: kind }                  a declared boundary consumed the break
 *   { broken: 'undeclared' }            fail closed — ARCH-004 with no declaration
 *   { broken: 'boundary-not-reached' }  declared, but the seal did NOT break
 *
 * The fourth exists because a declaration that never fires is worse than a missing
 * one: the author believes a cold boundary is covered, and the scenario silently
 * stopped exercising it. Same reasoning as an empty `attempts` list in a fault.
 */
export function sealDecision({ previousWire, body, boundary }) {
  if (previousWire === null || previousWire === undefined) {
    // `frame-commit` and `prefix-probe` share the multi-delivery shape: the first
    // request establishes the seal (nothing to break), later ones break it.
    if (boundary !== null && boundary !== undefined
        && (boundary.kind === 'prefix-probe' || boundary.kind === 'frame-commit')) {
      return { held: true };
    }
    return boundary === null || boundary === undefined
      ? { held: true }
      : { broken: 'boundary-not-reached', kind: boundary.kind };
  }

  const nextWire = wireOf(body);
  const held = isAppendOnlyPrefix(previousWire, nextWire);

  if (boundary === null || boundary === undefined) {
    return held ? { held: true } : { broken: 'undeclared' };
  }

  if (held) {
    // A `prefix-probe` declaration governs an ENTRY, and an entry is delivered
    // several times across a recovery sequence (probe slots and ordinary slots
    // alternate as the cursor advances). Only some of those deliveries break the
    // prefix, so an append-only delivery is legal; "the declaration never fired at
    // all" is checked at scenario end by `ScenarioRuntime.unfiredBoundaries`.
    // `frame-commit` shares this shape: the Blogger session's first request
    // establishes the seal (nothing to break), later requests break it as frames
    // accumulate.
    return boundary.kind === 'prefix-probe' || boundary.kind === 'frame-commit'
      ? { held: true }
      : { broken: 'boundary-not-reached', kind: boundary.kind };
  }

  switch (boundary.kind) {
    // COMPANION-009: the prefix is deliberately rebased, so no message-level claim
    // survives. The declaration is the whole authority, which is why it has to name a
    // single point rather than a range.
    case 'epoch-switch':
      return { resealed: 'epoch-switch' };

    // FALLBACK-004: only the model may move. Messages must still be append-only, so a
    // declared side switch cannot excuse a rewritten transcript.
    case 'fallback-side':
      return messagesStillAppendOnly(previousWire, nextWire)
        ? { resealed: 'fallback-side' }
        : { broken: 'fallback-side-rewrote-messages' };

    // CTX-010: an attempt-local X prefix probe replaces the committed prefix with a
    // synthetic companion-memory head plus the tail after the candidate cutoff. The
    // declaration admits exactly that: the system prompt and the tool set are fixed
    // for the attempt (they belong to the profile, PROMPT-008), while the message
    // prefix is deliberately rebased and the model may move with the fallback side
    // switch that usually accompanies a recovery attempt. Anything that rewrites the
    // fixed parts is not a probe.
    case 'prefix-probe':
      return probeKeepsFixedParts(previousWire, nextWire)
        ? { resealed: 'prefix-probe' }
        : { broken: 'prefix-probe-rewrote-fixed' };

    // ENFORCER-030: a frame commit rebuilds the Blogger session's provider view —
    // the frame list grows by one and the delta advances. The system prompt and
    // the tool set (blog only) are fixed parts of the Blogger profile
    // (ENFORCER-010), so only the message prefix may move.
    case 'frame-commit':
      return probeKeepsFixedParts(previousWire, nextWire)
        ? { resealed: 'frame-commit' }
        : { broken: 'frame-commit-rewrote-fixed' };

    // SyncDelegate: a dedicated Inspector/Coder CE may swap the per-request
    // tool map (Returned → Completion) without replacing its transcript.
    case 'request-kind-switch':
      return requestKindKeepsPrefix(previousWire, nextWire)
        ? { resealed: 'request-kind-switch' }
        : { broken: 'request-kind-switch-rewrote-prefix' };

    // RELAY-PROJ: the first durable Road makes the bounded synthetic Relay
    // context visible. Unlike a generic epoch switch, this may not delete or
    // rewrite anything the provider already saw.
    case 'relay-context-open':
      return relayContextOpened(previousWire, nextWire)
        ? { resealed: 'relay-context-open' }
        : { broken: 'relay-context-open-rewrote-fixed' };

    case 'relay-context-revision':
      return relayContextRevised(previousWire, nextWire)
        ? { resealed: 'relay-context-revision' }
        : { broken: 'relay-context-revision-rewrote-fixed' };

    case 'relay-retirement-context':
      return relayRetirementContext(previousWire, nextWire)
        ? { resealed: 'relay-retirement-context' }
        : { broken: 'relay-retirement-context-rewrote-fixed' };

    case 'relay-successor-cut':
      return relaySuccessorCut(previousWire, nextWire)
        ? { resealed: 'relay-successor-cut' }
        : { broken: 'relay-successor-cut-rewrote-fixed' };

    default:
      throw new Error(`unknown cold boundary kind '${boundary.kind}'`);
  }
}

// ── load-time validation ────────────────────────────────────────────────────

export function validateBoundary(boundary) {
  const problems = [];

  if (!BOUNDARY_KINDS.includes(boundary.kind)) {
    problems.push(
      `unknown cold boundary kind '${boundary.kind}'; ARCH-004 admits only ${BOUNDARY_KINDS.join(', ')}`,
    );
  }
  if (typeof boundary.turn !== 'string' || boundary.turn === '') {
    problems.push('cold boundary must name the turn it happens at');
  }
  if (!Number.isInteger(boundary.step) || boundary.step < 0) {
    problems.push('cold boundary step must be a non-negative integer');
  }

  return problems;
}
