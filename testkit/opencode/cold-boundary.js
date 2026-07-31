/**
 * cold-boundary.js — the two declared exceptions to the prefix seal.
 *
 * ARCH-004 keeps the provider-visible prefix byte-stable so KV-cache hits. VERIFY-003
 * §"冷边界显式声明" names the only two legitimate exceptions and requires the scenario
 * to say WHERE each happens:
 *
 *   COMPANION-009  epoch switch — new SealRoot, one explicit prefix rebase
 *   FALLBACK-004   fallback side switch — EffectiveAgent moves, so the model does
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

import { isAppendOnlyPrefix, wireOf } from './provider-wire.js';
import { equals } from '../../build/next/fable_modules/fable-library-js.5.13.0/Util.js';

export const BOUNDARY_KINDS = ['epoch-switch', 'fallback-side', 'prefix-probe'];

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

/** `previous` with `next`'s model fields, so only the messages/tools/system differ. */
const withModelOf = (previousWire, nextWire) => ({
  ...previousWire,
  ProviderId: nextWire.ProviderId,
  ModelId: nextWire.ModelId,
  Variant: nextWire.Variant,
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
const probeKeepsFixedParts = (previousWire, nextWire) => equals(previousWire.Tools, nextWire.Tools);

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
    return boundary.kind === 'prefix-probe'
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
