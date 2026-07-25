import { matchesExpectation } from './strict-mock-matches.js';

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

  return {
    scenario: requiredString(lane.scenario, 'scenario'),
    session: requiredString(lane.session, 'session'),
    role: requiredString(lane.role, 'role'),
    turn: lane.turn,
    requestKind: requiredString(lane.requestKind, 'requestKind'),
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

export function selectExpectation(state, body) {
  const heads = pendingLaneHeads(state);
  const matches = heads.filter(({ expectation }) => matchesExpectation(body, expectation));

  if (matches.length === 1) return { match: matches[0], candidates: heads };
  if (matches.length === 0) return { match: null, reason: 'no-lane-head-matched', candidates: heads };
  return { match: null, reason: 'ambiguous-lane-heads', candidates: matches };
}

export function consumeExpectation(state, match) {
  const queue = state.lanes.get(match.key);
  if (!queue || queue.expectations[0] !== match.expectation) {
    throw new Error(`StrictMock lane head changed before consuming ${match.expectation.id}`);
  }
  queue.expectations.shift();
  if (queue.expectations.length === 0) state.lanes.delete(match.key);
  return match.expectation;
}
