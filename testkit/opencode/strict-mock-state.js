/**
 * strict-mock-state.js — Provider state record helpers.
 * Kept in its own module so the main provider class file
 * stays under the 200-line Kolmogorov line budget.
 */

import { laneKey, normalizeLane } from './strict-mock-lanes.js';

export function createState() {
  return {
    lanes: new Map(),
    lastTurnByLane: new Map(),
    sessionBindings: new Map(),
    unexpected: [],
    requests: [],
    responseCounter: 0,
    idCounter: 0,
    strict: true,
    // First unmatched script stops the mock entirely (no more surprises).
    fatal: null,
    // sessionId -> last matched provider-visible seal (append-only prefix check).
    sealedBySession: new Map(),
  };
}

export function pushExpectation(state, respond, opts) {
  const lane = normalizeLane(opts.lane);
  const key = laneKey(lane);
  let queue = state.lanes.get(key);
  const expectedTurn = (state.lastTurnByLane.get(key) || 0) + 1;
  if (lane.turn !== expectedTurn) {
    throw new Error(`StrictMock lane ${key} expected turn ${expectedTurn}, got ${lane.turn}`);
  }
  if (!queue) {
    queue = { expectations: [] };
    state.lanes.set(key, queue);
  }

  const match = { ...(opts.match || {}) };
  if (lane.role !== '*' && match.role === undefined) match.role = lane.role;
  if (lane.requestKind !== '*' && match.requestKind === undefined) match.requestKind = lane.requestKind;
  const flags = {};
  const flagKeys = [
    'delayFirstToken', 'delayDone', 'disconnectMidSse', 'contextOverflow',
    'emptyAssistant', 'reasoningOnly', 'fragmentArgs', 'malformedArgs',
    'toolCallAsText', 'duplicateToolCallId', 'errorAfterToolCall', 'neverEnd',
    'missingUsage'
  ];
  for (const k of flagKeys) {
    if (opts[k] !== undefined) {
      flags[k] = opts[k];
    }
  }
  queue.expectations.push({
    id: opts.id || `exp-${++state.idCounter}`,
    lane,
    match,
    blocking: opts.blocking !== false,
    neverEnd: opts.neverEnd === true,
    respond: { ...respond, ...flags },
  });
  state.lastTurnByLane.set(key, lane.turn);
}

export function resetState(state) {
  state.lanes.clear();
  state.lastTurnByLane.clear();
  state.sessionBindings.clear();
  state.unexpected.length = 0;
  state.requests.length = 0;
  state.responseCounter = 0;
  state.stopped = false;
  state.fatal = null;
  state.sealedBySession.clear();
}
