/**
 * Script forest matcher (AGENTS.md KISS-N11).
 *
 * Runtime identity for a *request* is provider-visible full seal (idempotent).
 * Authoring paths (lane.session) are ordered chains: only the current head of
 * each path is eligible. That is sequential path structure, not content mute.
 *
 * - No numbering to pick different responds for the same prefix.
 * - No host message id / session-bind identity.
 * - User-text (and other visible content) forks across paths.
 * - Duplicate identical templates collapse at registration.
 */

import {
  matchesExpectation,
  requestRoleOf,
  sealProviderVisible,
} from './strict-mock-matches.js';

export function edgeWaitIds(edge) {
  const ids = [edge.id];
  if (edge.aliases) {
    for (const a of edge.aliases) ids.push(a);
  }
  return ids;
}

export function edgeLabel(edge) {
  const lane = edge.lane || {};
  return `${edge.id}@${lane.scenario || '?'}/${lane.session || '?'}/${lane.role || '?'}/${lane.requestKind || 'chat'}`;
}

export function allEdges(state) {
  return state.edges || [];
}

export function pendingExpectations(state) {
  return allEdges(state).filter((e) => {
    if (e.blocking === false) return false;
    if (e.pathless || e.neverEnd) return false;
    return !state.observedEdgeIds?.has(e.id);
  });
}

function pathKey(edge) {
  const lane = edge.lane || {};
  return `${lane.scenario || ''}\u001f${lane.session || ''}\u001f${lane.requestKind || 'chat'}`;
}

function specificity(edge) {
  const m = edge.match || {};
  let n = 0;
  if (m.user) n += String(m.user).length;
  if (m.userRegex) n += String(m.userRegex).length;
  for (const t of m.containsText || []) n += String(t).length;
  if (m.requiredTools) n += m.requiredTools.length * 4;
  if (m.messageCount !== undefined) n += 8;
  if (m.afterToolResult === true) n += 50;
  if (m.afterToolResult === false) n += 40;
  if (m.role && m.role !== '*') n += 2;
  if (m.requestKind && m.requestKind !== '*') n += 2;
  return n;
}

function respondDigest(respond) {
  return JSON.stringify({
    type: respond?.type,
    text: respond?.text,
    tool: respond?.tool,
    args: typeof respond?.args === 'function' ? '[fn]' : respond?.args,
    status: respond?.status,
  });
}

function matchDigest(match) {
  return JSON.stringify(match || {});
}


function lastUserIndex(msgs) {
  for (let i = msgs.length - 1; i >= 0; i -= 1) {
    if (msgs[i]?.role === 'user') return i;
  }
  return -1;
}

function lastUserText(msgs) {
  const i = lastUserIndex(msgs);
  if (i < 0) return '';
  const c = msgs[i]?.content;
  if (typeof c === 'string') return c;
  return JSON.stringify(c || '');
}

/** Tool/function result or assistant tool_call after the last user message. */
function hasToolAfterLastUser(body) {
  const msgs = body?.messages || [];
  const u = lastUserIndex(msgs);
  if (u < 0) return false;
  for (let i = u + 1; i < msgs.length; i += 1) {
    const m = msgs[i];
    const role = m?.role;
    if (role === 'tool' || role === 'function' || role === 'toolResult') return true;
    if (role === 'assistant' && Array.isArray(m?.tool_calls) && m.tool_calls.length) return true;
  }
  return false;
}

export function templateMatches(body, edge) {
  const match = { ...(edge.match || {}) };
  const role = match.role;
  if (role && role !== '*' && role !== 'synthetic' && role !== 'title' && role !== 'blogger') {
    const actual = requestRoleOf(body);
    if (actual !== 'unknown' && actual !== role) return false;
  }

  const msgs = body?.messages || [];
  const toolAfter = hasToolAfterLastUser(body);
  // afterToolResult:true  → continuation after tools for the current last user
  // afterToolResult:false → fresh user turn (no tool result yet after that user)
  if (match.afterToolResult === true && !toolAfter) return false;
  if (match.afterToolResult === false && toolAfter) return false;

  const { afterToolResult, ...rest } = match;
  return matchesExpectation(body, { ...edge, match: rest, neverEnd: true }, new Map());
}

function pathHead(state, edge) {
  if (edge.pathless || edge.neverEnd || edge.reusable) return true;
  const key = pathKey(edge);
  const ordered = state.pathEdges?.get(key) || [];
  const cursor = state.pathCursor?.get(key) || 0;
  const idx = ordered.indexOf(edge);
  if (idx < cursor) return false;
  // May skip only optional (blocking === false) edges between cursor and idx.
  // Must not skip any required edge.
  for (let i = cursor; i < idx; i += 1) {
    if (ordered[i].blocking !== false) return false;
  }
  return true;
}

function pathIndex(state, edge) {
  if (edge.pathless || edge.neverEnd || edge.reusable) return 0;
  const key = pathKey(edge);
  const ordered = state.pathEdges?.get(key) || [];
  return ordered.indexOf(edge);
}

/**
 * Select edge for body. Seal hit is pure idempotent cache.
 */
export function selectExpectation(state, body) {
  const seal = sealProviderVisible(body);
  const cached = state.sealToEdgeId?.get(seal);
  if (cached) {
    const edge = allEdges(state).find((e) => e.id === cached);
    if (edge) {
      return {
        match: { key: seal, expectation: edge, fromCache: true },
        candidates: allEdges(state).map((e) => ({ expectation: e })),
      };
    }
  }

  const candidates = allEdges(state);
  const hits = candidates.filter((e) => pathHead(state, e) && templateMatches(body, e));
  if (hits.length === 0) {
    return {
      match: null,
      reason: 'no-prefix-matched',
      candidates: candidates.filter((e) => pathHead(state, e)).map((e) => ({ expectation: e })),
    };
  }

  // Prefer: higher content specificity, then earliest path index (don't skip further than needed).
  hits.sort((a, b) => {
    const ds = specificity(b) - specificity(a);
    if (ds !== 0) return ds;
    return pathIndex(state, a) - pathIndex(state, b);
  });
  if (hits.length > 1 && specificity(hits[0]) === specificity(hits[1]) && pathIndex(state, hits[0]) === pathIndex(state, hits[1])) {
    const top = hits.filter((e) => specificity(e) === specificity(hits[0]) && pathIndex(state, e) === pathIndex(state, hits[0]));
    const digests = new Set(top.map((e) => respondDigest(e.respond)));
    if (digests.size !== 1) {
      return {
        match: null,
        reason: 'ambiguous-prefix',
        candidates: top.map((e) => ({ expectation: e })),
      };
    }
  }

  const edge = hits[0];
  return {
    match: { key: seal, expectation: edge, fromCache: false },
    candidates: candidates.map((e) => ({ expectation: e })),
  };
}

export function consumeExpectation(state, match, _requestSessionID) {
  const edge = match.expectation;
  if (!state.sealToEdgeId) state.sealToEdgeId = new Map();
  if (!state.observedEdgeIds) state.observedEdgeIds = new Set();
  if (!state.pathCursor) state.pathCursor = new Map();

  // Error responds are intentionally non-idempotent for Host retry tests:
  // afterExpectation registers a successor edge for the same provider-visible
  // body. Caching seal→error would permanently trap retries on the failure.
  // Success responds stay seal-cached (KISS-N11 idempotent forest).
  const respondType = edge.respond?.type;
  if (respondType !== 'error' && respondType !== 'disconnect') {
    state.sealToEdgeId.set(match.key, edge.id);
  } else {
    state.sealToEdgeId.delete(match.key);
  }
  state.observedEdgeIds.add(edge.id);

  // Advance path cursor only on first observation of this seal→edge (not cache replay)
  if (!match.fromCache && !edge.pathless && !edge.neverEnd && !edge.reusable) {
    const key = pathKey(edge);
    const ordered = state.pathEdges?.get(key) || [];
    const idx = ordered.indexOf(edge);
    if (idx >= 0) {
      const cursor = state.pathCursor.get(key) || 0;
      // Advance at least past this edge (skips optional intermediates).
      state.pathCursor.set(key, Math.max(cursor, idx + 1));
    }
  }
  return edge;
}

export function normalizeLane(lane) {
  if (!lane || typeof lane !== 'object') {
    throw new Error('StrictMock edge requires lane { scenario, session, role, requestKind }');
  }
  for (const field of ['scenario', 'session', 'role', 'requestKind']) {
    if (!(field in lane) || typeof lane[field] !== 'string' || lane[field].trim() === '') {
      throw new Error(`StrictMock lane is missing ${field}`);
    }
  }
  return {
    scenario: lane.scenario,
    session: lane.session,
    role: lane.role,
    turn: Number.isInteger(lane.turn) ? lane.turn : 1,
    requestKind: lane.requestKind,
    parentSession: lane.parentSession === undefined ? null : lane.parentSession,
  };
}

export function laneLabel(lane) {
  if (!lane) return '?';
  return `${lane.scenario}/${lane.session}/${lane.role}/turn-${lane.turn || 1}/${lane.requestKind}`;
}

export function laneKey(lane) {
  return [lane.scenario, lane.session, lane.role, lane.requestKind].join('\u001f');
}

export function indexPathEdge(state, edge) {
  if (!state.pathEdges) state.pathEdges = new Map();
  if (!state.pathCursor) state.pathCursor = new Map();
  if (edge.pathless || edge.neverEnd) return;
  const key = pathKey(edge);
  let list = state.pathEdges.get(key);
  if (!list) {
    list = [];
    state.pathEdges.set(key, list);
    state.pathCursor.set(key, 0);
  }
  list.push(edge);
  list.sort((a, b) => (a.lane.turn || 1) - (b.lane.turn || 1));
}

export function templateFingerprint(edge) {
  return [
    edge.lane?.requestKind || 'chat',
    matchDigest(edge.match),
    respondDigest(edge.respond),
    edge.neverEnd ? '1' : '0',
    edge.pathless ? '1' : '0',
  ].join('\u001f');
}

export { matchDigest, respondDigest };
