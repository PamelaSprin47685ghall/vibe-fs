/**
 * strict-mock-state.js — Provider state record helpers.
 * Kept in its own module so the main provider class file
 * stays under the 200-line Kolmogorov line budget.
 */

export const NO_MORE_REQUESTS_ID = 'no-more-requests';

export function createState() {
  return {
    expectations: [],
    unexpected: [],
    requests: [],
    syntheticRequests: [],
    nudgeBypassed: 0,
    idCounter: 0,
    strict: true,
    allowSyntheticContinuations: false,
    allowTitleGeneration: false,
    allowOutOfOrder: false,
  };
}

export function pushExpectation(state, respond, opts) {
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
  state.expectations.push({
    id: opts.id || `exp-${++state.idCounter}`,
    match: opts.match || {},
    respond: { ...respond, ...flags },
  });
}

export function pushNoMoreRequests(state) {
  state.expectations.push({
    id: NO_MORE_REQUESTS_ID,
    match: {},
    respond: { type: 'no-more-requests-boundary' },
    terminal: true,
  });
}

export function resetState(state) {
  state.expectations.length = 0;
  state.unexpected.length = 0;
  state.requests.length = 0;
  state.syntheticRequests.length = 0;
  state.stopped = false;
  state.nudgeBypassed = 0;
}
