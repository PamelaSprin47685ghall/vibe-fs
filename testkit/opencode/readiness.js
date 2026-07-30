/**
 * readiness.js — the startup window, as a ladder of causal stages (VERIFY-004, W5).
 *
 * The clause requires seamless coverage:
 *
 *   watchdog 装好之前的窗口同样需要因果判据。启动阶段（进程拉起到就绪）必须有独立的就绪判据，
 *   不得只靠兜底 wall-clock 覆盖。
 *   禁止：存在一段「只有总超时保护的时间窗」
 *
 * Before this module the launcher had one flat window from `spawn` to the `[setupScenario] ready`
 * bark. Inside it, nothing was a criterion: a canary that bound its port and then wedged looked
 * exactly like one still compiling, and both were reported as "failed to emit ready" — a statement
 * true of the symptom and silent about the cause.
 *
 * ── the evidence already existed ────────────────────────────────────────────
 *
 * No production change was needed, which is the part worth recording. `setupScenario` and
 * `process-host` already print a timing line as each stage completes, so the startup sequence was
 * observable all along and simply unobserved. A ladder over those lines is therefore a reading of
 * existing evidence rather than a new instrumentation surface — and evidence that must be emitted
 * for the ladder's sake would be evidence the ladder could not trust.
 *
 * ── why a ladder rather than a shorter flat window ───────────────────────────
 *
 * A shorter window would fail slow machines; a longer one covers nothing. The ladder makes the
 * question causal instead: each stage gets its own budget, and reaching a stage re-arms it. Total
 * startup time is then unbounded in the same way a healthy canary's runtime is unbounded — what is
 * bounded is silence, which is the clause's whole thesis stated one layer earlier.
 */

/**
 * The stages, in the order the harness actually prints them.
 *
 * ORDER IS MEASURED, NOT ASSUMED. The first draft of this list read `workspace` before `provider`
 * because that is the order the two read naturally — prepare a workspace, then start a provider.
 * `scenario-parallel.js` prints them the other way (`provider.start took` at :88, `prepareWorkspace
 * took` at :94). Since `observe` only ever advances on the NEXT expected marker, that inversion
 * stalls the climb at 1/6 and fails every canary at the stage budget: a readiness gate that reports
 * 「stuck」 for a perfectly healthy startup. `gate-readiness-cases.mjs` pins the order against the
 * sources so the next reordering of production prints fails there instead.
 *
 * Matched as substrings of a line the child already prints. Deliberately NOT anchored to the exact
 * format: these lines carry timings, and pinning their shape would make the ladder break on a log
 * tweak — which is how a readiness gate turns into a pseudo-gate that reports "never reached stage
 * 3" for a run that reached it and phrased it differently.
 *
 * `ready` is last and is the same exact-match bark the launcher already gates on, so the ladder
 * ends where the existing contract ends rather than replacing it.
 */
export const READINESS_STAGES = Object.freeze([
  { name: 'provider', marker: '[setupScenario] provider.start took' },
  { name: 'workspace', marker: '[setupScenario] prepareWorkspace took' },
  { name: 'port-bound', marker: '[host.start] _waitForListening took' },
  { name: 'host-healthy', marker: '[host.start] _waitForHealth took' },
  { name: 'events', marker: '[setupScenario] events.connect took' },
  { name: 'ready', marker: '[setupScenario] ready' },
]);

/**
 * One canary's climb.
 *
 * Monotonic by construction: `observe` only ever moves the index forward. A canary that reprints an
 * earlier stage (a retried health check) must not reset the ladder, or a retry loop would renew the
 * startup budget forever — the same 「反复重连的 SSE 读者」 shape the clause names for the watchdog.
 */
export class ReadinessLadder {
  constructor() {
    this._reached = 0;
    this._reachedAt = Date.now();
  }

  /**
   * Feed one chunk of child output. Returns the stage names newly reached, in order.
   *
   * A chunk may contain several stages — output arrives in blocks — so this drains as far as the
   * chunk allows rather than advancing one step per call.
   */
  observe(text) {
    const advanced = [];

    while (this._reached < READINESS_STAGES.length) {
      const next = READINESS_STAGES[this._reached];
      if (!text.includes(next.marker)) break;
      this._reached += 1;
      this._reachedAt = Date.now();
      advanced.push(next.name);
    }

    return advanced;
  }

  get isReady() {
    return this._reached >= READINESS_STAGES.length;
  }

  /** How long the climb has been stalled, for the diagnostic. */
  get silentForMs() {
    return Date.now() - this._reachedAt;
  }

  /**
   * What the diagnostic says when the budget runs out.
   *
   * Names the stage reached AND the one awaited, because those are different facts and the pair is
   * what makes a startup failure actionable: "reached provider, awaiting port-bound" points at the
   * Host's listen call, while "reached nothing" points at module load.
   */
  describe() {
    const reachedName = this._reached === 0 ? '(nothing)' : READINESS_STAGES[this._reached - 1].name;
    const awaitingName = this.isReady ? '(ready)' : READINESS_STAGES[this._reached].name;

    return (
      `reached ${reachedName} (${this._reached}/${READINESS_STAGES.length}), ` +
      `awaiting ${awaitingName}, silent for ${this.silentForMs}ms`
    );
  }
}
