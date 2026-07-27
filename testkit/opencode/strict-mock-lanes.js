import { matchesExpectation, requestSessionOf } from './strict-mock-matches.js';

const requiredFields = ['scenario', 'session', 'role', 'turn', 'requestKind'];

function requiredString(value, field) {
  if (typeof value !== 'string' || value.trim() === '') {
    throw new Error(`StrictMock lane requires non-empty ${field}`);
  }
  return value;
}

export function normalizeLane(lane) {
  if (!lane || typeof lane !== 'object') {
    throw new Error('StrictMock expectation requires lane { scenario, session, role, turn, requestKind }');
  }

  for (const field of requiredFields) {
    if (!(field in lane)) throw new Error(`StrictMock lane is missing ${field}`);
  }

  if (!Number.isInteger(lane.turn) || lane.turn < 1) {
    throw new Error('StrictMock lane turn must be a positive integer');
  }

  const parentSession = lane.parentSession === undefined
    ? null
    : requiredString(lane.parentSession, 'parentSession');

  return {
    scenario: requiredString(lane.scenario, 'scenario'),
    session: requiredString(lane.session, 'session'),
    role: requiredString(lane.role, 'role'),
    turn: lane.turn,
    requestKind: requiredString(lane.requestKind, 'requestKind'),
    parentSession,
  };
}

export function laneKey(lane) {
  return [lane.scenario, lane.session, lane.role, lane.requestKind].join('\u001f');
}

export function laneLabel(lane) {
  return `${lane.scenario}/${lane.session}/${lane.role}/turn-${lane.turn}/${lane.requestKind}`;
}

export function pendingExpectations(state) {
  return [...state.lanes.values()].flatMap((queue) => queue.expectations);
}

export function pendingLaneHeads(state) {
  return [...state.lanes.entries()]
    .map(([key, queue]) => ({ key, expectation: queue.expectations[0] }))
    .filter(({ expectation }) => expectation);
}

/**
 * Script model:
 * - Each lane is a script queue; only the head is eligible.
 * - A request matches the unique head whose first-message content
 *   (containsText / role / tools / requestKind) fits.
 * - Test authors MUST make those first messages mutually unique across heads.
 * - After first consume, session alias bind makes later turns of the same script
 *   prefer that bound session — still only one head should match.
 *
 * No parent/recovery collapse heuristics. Ambiguity is a test-author bug.
 */
export function selectExpectation(state, body) {
  const heads = pendingLaneHeads(state);
  const matches = heads.filter(({ expectation }) =>
    matchesExpectation(body, expectation, state.sessionBindings));

  const sessionID = requestSessionOf(body);
  let selected = sessionID
    ? matches.filter(({ expectation }) =>
      state.sessionBindings.get(expectation.lane.session) === sessionID)
    : [];
  if (selected.length === 0) selected = matches;

  // Most-specific first-message wins (longest user/userRegex/containsText).
  if (selected.length > 1) {
    const score = (expectation) => {
      const m = expectation.match || {};
      let n = 0;
      if (m.user) n += String(m.user).length;
      if (m.userRegex) n += String(m.userRegex).length;
      for (const t of m.containsText || []) n += String(t).length;
      return n;
    };
    const ranked = [...selected].sort((a, b) => score(b.expectation) - score(a.expectation));
    if (score(ranked[0].expectation) > score(ranked[1].expectation)) {
      selected = [ranked[0]];
    }
  }

  if (selected.length === 1) return { match: selected[0], candidates: heads };
  if (selected.length === 0) return { match: null, reason: 'no-lane-head-matched', candidates: heads };
  return { match: null, reason: 'ambiguous-lane-heads', candidates: selected };
}

// neverEnd: head stays forever after first match; at most one neverEnd per lane,
// and it must be the last expectation in that lane.
export function consumeExpectation(state, match, requestSessionID) {
  const queue = state.lanes.get(match.key);
  if (!queue || queue.expectations[0] !== match.expectation) {
    throw new Error(`StrictMock lane head changed before consuming ${match.expectation.id}`);
  }
  if (!match.expectation.neverEnd) {
    queue.expectations.shift();
    if (queue.expectations.length === 0) state.lanes.delete(match.key);
  }
  if (requestSessionID && !match.expectation.neverEnd) {
    // neverEnd scripts (blogger sidecars) re-fire on new physical sessions after
    // restart; first-message content is the script key. Multi-turn scripts bind.
    const bound = state.sessionBindings.get(match.expectation.lane.session);
    if (bound && bound !== requestSessionID) {
      throw new Error(`StrictMock lane ${match.expectation.lane.session} changed session identity`);
    }
    if (!bound) state.sessionBindings.set(match.expectation.lane.session, requestSessionID);
  }
  return match.expectation;
}
