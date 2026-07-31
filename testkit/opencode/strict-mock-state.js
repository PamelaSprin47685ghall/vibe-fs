/**
 * strict-mock-state.js — Provider state for the static scenario forest (K9).
 *
 * Expectation edges are gone: a provider is driven by an attached `ScenarioRuntime`
 * or it fails closed. What remains here is request bookkeeping shared by the
 * dispatch and the diagnostics: recorded requests, first-mismatch fatal, per-session
 * prefix seals and alias bindings.
 */

export function createState() {
  return {
    sessionBindings: new Map(), // diagnostic / canary routing only
    unexpected: [],
    requests: [],
    responseCounter: 0,
    strict: true,
    fatal: null,
    sealedBySession: new Map(),
    stopped: false,
  };
}

export function resetState(state) {
  state.sessionBindings.clear();
  state.unexpected.length = 0;
  state.requests.length = 0;
  state.responseCounter = 0;
  state.stopped = false;
  state.fatal = null;
  state.sealedBySession.clear();
}
