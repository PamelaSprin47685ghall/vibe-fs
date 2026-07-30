/**
 * strict-mock-state.js — Provider state for script forest (KISS-N11).
 */

import { indexPathEdge, normalizeLane, templateFingerprint } from './strict-mock-forest.js';

export function createState() {
  return {
    edges: [],
    edgeIds: new Set(),
    templateIndex: new Map(), // fingerprint → edge (dedupe identical templates)
    aliasToEdge: new Map(),
    pathEdges: new Map(),
    pathCursor: new Map(),
    sealToEdgeId: new Map(),
    observedEdgeIds: new Set(),
    sessionBindings: new Map(), // diagnostic / canary routing only
    unexpected: [],
    requests: [],
    responseCounter: 0,
    idCounter: 0,
    strict: true,
    fatal: null,
    sealedBySession: new Map(),
    /** @type {Map<string, string>} last provider model id per session (fallback reseal) */
    stopped: false,
  };
}

export function pushExpectation(state, respond, opts) {
  const lane = normalizeLane(opts.lane);
  const match = { ...(opts.match || {}) };
  if (lane.role !== '*' && match.role === undefined) match.role = lane.role;
  if (lane.requestKind !== '*' && match.requestKind === undefined) match.requestKind = lane.requestKind;

  const id = opts.id || `exp-${++state.idCounter}`;
  if (state.edgeIds.has(id)) {
    throw new Error(`StrictMock duplicate edge id: ${id}`);
  }

  const flags = {};
  const flagKeys = [
    'delayFirstToken', 'delayDone', 'disconnectMidSse', 'contextOverflow',
    'emptyAssistant', 'reasoningOnly', 'fragmentArgs', 'malformedArgs',
    'toolCallAsText', 'duplicateToolCallId', 'errorAfterToolCall', 'neverEnd',
    'missingUsage',
  ];
  for (const k of flagKeys) {
    if (opts[k] !== undefined) flags[k] = opts[k];
  }

  // Title requests are pathless reusable templates (same visible shape across sessions).
  const pathless = opts.pathless === true
    || lane.requestKind === 'title'
    || opts.neverEnd === true;
  // reusable only when explicit or pathless (title/neverEnd). Sequential path
  // edges advance a cursor; content must disambiguate across paths.
  const reusable = opts.reusable === true || pathless;

  const edge = {
    id,
    lane,
    match,
    blocking: opts.blocking !== false && opts.neverEnd !== true && !pathless,
    neverEnd: opts.neverEnd === true,
    pathless,
    reusable,
    respond: { ...respond, ...flags },
  };

  // Collapse only pathless identical templates (title / neverEnd sidecars).
  // Sequential path edges stay distinct so path cursors remain well-defined.
  if (edge.pathless || edge.reusable) {
    const fp = templateFingerprint(edge);
    const existing = state.templateIndex.get(fp);
    if (existing) {
      if (!existing.aliases) existing.aliases = new Set();
      existing.aliases.add(id);
      state.edgeIds.add(id);
      // Map alias id for waitForExpectation
      if (!state.aliasToEdge) state.aliasToEdge = new Map();
      state.aliasToEdge.set(id, existing);
      return existing;
    }
    state.templateIndex.set(fp, edge);
  }

  state.edges.push(edge);
  state.edgeIds.add(id);
  if (!edge.pathless && !edge.reusable) indexPathEdge(state, edge);
  return edge;
}

export function resetState(state) {
  state.edges.length = 0;
  state.edgeIds.clear();
  state.templateIndex.clear();
  state.aliasToEdge.clear();
  state.pathEdges.clear();
  state.pathCursor.clear();
  state.sealToEdgeId.clear();
  state.observedEdgeIds.clear();
  state.sessionBindings.clear();
  state.unexpected.length = 0;
  state.requests.length = 0;
  state.responseCounter = 0;
  state.stopped = false;
  state.fatal = null;
  state.sealedBySession.clear();
}
