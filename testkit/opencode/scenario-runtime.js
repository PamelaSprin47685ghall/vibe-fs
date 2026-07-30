/**
 * scenario-runtime.js — the compiled scenario, as the provider actually consults it.
 *
 * VERIFY-003. K2-K5 each produced a pure piece: `runtime-key` derives (lane, kind, turn,
 * step) from a request, `delivery-plan` says whether THIS physical delivery is refused,
 * `cold-boundary` says whether a broken prefix seal was declared, `scenario-schema`
 * compiles the source. None of them knew about the others, and measured after K8:
 * every one had zero callers on the provider path.
 *
 * That is the shape `single-constructor` exists to catch one layer down — a checker
 * nothing calls is a checker that is not in force. This module is the composition, and
 * the gate cases beside it are its first real caller.
 *
 * ── what belongs here and what does not ─────────────────────────────────────
 *
 * Exactly three pieces of state, each of which a request genuinely cannot report about
 * itself:
 *
 *   bindings     alias → session id, because a scenario names sessions before the Host
 *                mints them (HOST-008 makes the association durable; the mock is told,
 *                it does not infer)
 *   deliveries   how many physical deliveries a key has seen — identical on attempt 1
 *                and attempt 3, so nothing in the body can say
 *   seals        the previous wire projection per session, to compare the next against
 *
 * What is deliberately NOT here: `pathCursor`, `claimCount`, `matchCount`,
 * `observedEdgeIds` as a matching input, `sealToEdgeId`. The old matcher kept all five
 * and consulted them while选边, which is why its answer depended on how many requests
 * had already arrived. Content is a pure function of the request; the counters above
 * are read only to decide DELIVERY and SEAL, never to decide which content.
 */

import { boundaryFor, sealDecision } from './cold-boundary.js';
import { deliveryOutcome, emptyDeliveries, faultFor, recordDelivery } from './delivery-plan.js';
import { resolveEntry, runtimeKeyOf } from './runtime-key.js';
import { wireOf } from './provider-wire.js';

export class ScenarioRuntime {
  /** @param scenario the `{ entries, faults, boundaries }` shape `compileScenario` returns */
  constructor(scenario) {
    this.scenario = scenario;
    this.bindings = new Map();
    this.deliveries = emptyDeliveries();
    this.seals = new Map();
    /** Which entry ids have been answered at least once — for `expectSatisfied` only. */
    this.answered = new Set();
  }

  /**
   * HOST-008: the harness is TOLD the association when the Host mints the id.
   *
   * One alias may hold SEVERAL sessions. A scenario names lanes by role
   * (`fast-reviewer`), and production forks as many children of that role as the work
   * needs — `orchestrator-publish` reviews twice, before and after the rebase. Treating
   * the second child as a rebind would either throw or silently orphan the first.
   */
  bind(alias, sessionId) {
    const bound = this.bindings.get(alias) ?? new Set();
    bound.add(sessionId);
    this.bindings.set(alias, bound);
  }

  /** The most recently bound session for an alias, for assertions that need one. */
  sessionFor(alias) {
    const bound = this.bindings.get(alias);
    return bound === undefined ? undefined : [...bound].at(-1);
  }

  /**
   * A Host restart makes every previous seal incomparable.
   *
   * The new process rebuilds its request view from the journal, so the next request is a
   * fresh baseline rather than a continuation — comparing it against the pre-restart wire
   * would report an ARCH-004 break for something that is not one. Deliveries and answers
   * survive: `attempts` counts physical deliveries across the whole scenario, and a step
   * answered before the restart was still answered.
   */
  clearSeals() {
    this.seals.clear();
  }

  /**
   * Decide one chat request. Six outcomes, and every one is terminal for this request.
   *
   *   { unmatched }    nothing declared here — fail closed, never a default reply
   *   { ambiguous }    two equal-length prefixes; the scenario does not say what happens
   *   { sealBroken }   ARCH-004 violated with no declaration, or a declaration that did
   *                    not fire
   *   { fault }        a declared transport fault governs this delivery
   *   { entry }        deliver the declared content
   *
   * The order matters and is not arbitrary. The seal is checked BEFORE the delivery
   * counter advances, because a request that breaks the prefix barrier never reached the
   * provider legitimately — counting it as a delivery would let an undeclared rewrite
   * consume a fault's `attempts` slot and change what the next legitimate request gets.
   */
  select(body) {
    const resolved = resolveEntry(body, this.scenario.entries, this.bindings);

    if (resolved.unmatched !== undefined) return { unmatched: resolved.unmatched };
    if (resolved.ambiguousTurn !== undefined) {
      return { ambiguous: { key: resolved.key, entries: resolved.ambiguousTurn } };
    }

    const { key, matched } = resolved;

    const seal = this.#sealFor(body, key);
    if (seal.broken !== undefined) return { sealBroken: { reason: seal.broken, key, kind: seal.kind } };

    const attempt = recordDelivery(this.deliveries, key);
    const outcome = deliveryOutcome(faultFor(this.scenario.faults, key), attempt);

    return outcome.deliver === true
      ? { entry: matched, key, attempt, resealed: seal.resealed }
      : { fault: outcome.fault, entry: matched, key, attempt, resealed: seal.resealed };
  }

  /**
   * Record that this request was answered, so the next one in the session has a seal to
   * compare against.
   *
   * Only chat turns seal. A title request carries the Host's own marker at `messages[0]`
   * and the conversation after it (`../opencode/packages/opencode/src/session/
   * prompt.ts:235`), so its wire projection is a different shape that would break the
   * chat seal on the very next turn.
   *
   * A FAULTED delivery seals too. The refusal happened at the transport, so the next
   * attempt legitimately carries the same prefix — and if it carries a different one,
   * that is a real ARCH-004 break the scenario must declare. The old matcher deleted its
   * cache entry for errors instead, which is the non-idempotency `delivery-plan.js`
   * exists to remove.
   */
  consume(body, selection) {
    if (selection.entry !== undefined) this.answered.add(selection.entry.id);

    const sessionId = this.#sessionIdOf(body);
    if (sessionId !== null && runtimeKeyOf(body, this.bindings).kind === 'chat') {
      this.seals.set(sessionId, wireOf(body));
    }
  }

  /**
   * Declared steps that no request ever reached.
   *
   * `internal` turns are excluded: production decides whether to compose those prompts
   * at all (a re-anchor frame only exists after a restart, a guard nudge only after an
   * unreviewed completion), so their absence is not evidence of a broken scenario. A
   * scenario that needs one to arrive says so with `must`.
   */
  unanswered() {
    return this.scenario.entries.filter((entry) => entry.internal !== true && !this.answered.has(entry.id));
  }

  /** Whether every id named in `must` was answered. */
  unmetMust() {
    return (this.scenario.must ?? []).filter(
      (id) => !this.answered.has(id) && ![...this.answered].some((answered) => answered.startsWith(`${id}.`)),
    );
  }

  #sessionIdOf(body) {
    const id = body?.sessionId ?? body?.sessionID ?? null;
    return typeof id === 'string' && id !== '' ? id : null;
  }

  #sealFor(body, key) {
    if (key.kind !== 'chat') return { held: true };

    const sessionId = this.#sessionIdOf(body);
    if (sessionId === null) return { held: true };

    return sealDecision({
      previousWire: this.seals.get(sessionId) ?? null,
      body,
      boundary: boundaryFor(this.scenario.boundaries, key),
    });
  }
}
