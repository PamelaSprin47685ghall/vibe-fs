/**
 * strict-mock-satisfy.js — expectSatisfied() implementation for
 * StrictMockProvider. Kept in its own module so the main provider
 * class file stays under the 200-line Kolmogorov line budget.
 */

import { extractToolNames, extractLastUserMsg } from './strict-mock-matches.js';
import { laneLabel, pendingExpectations } from './strict-mock-lanes.js';

const PREVIEW_LIMIT = 5;

export function checkSatisfied(state) {
  const expectations = pendingExpectations(state).filter((e) => e.blocking !== false);
  const remaining = expectations.length;
  const unexpectedCount = state.unexpected.length;
  const errors = [];
  if (remaining > 0) {
    const detail = expectations.slice(0, PREVIEW_LIMIT).map((e) =>
      `  [${e.id}] lane=${laneLabel(e.lane)} respond=${e.respond.type} match=${JSON.stringify(e.match)}`,
    ).join('\n');
    errors.push(`remaining expectations = ${remaining}:\n${detail}`);
  }
  if (unexpectedCount > 0) {
    const detail = state.unexpected.slice(0, PREVIEW_LIMIT).map((u) =>
      `  session=${u.sessId || '?'} tools=${JSON.stringify(extractToolNames(u.body))} msgs=${u.body?.messages?.length || 0} toolResults=${u.hasToolResults || false} lastUser=${extractLastUserMsg(u.body) || '(none)'} reason=${u.reason || '?'} candidates=${JSON.stringify(u.candidates || [])}`,
    ).join('\n');
    errors.push(`unexpected requests = ${unexpectedCount} (UnexpectedLlmRequest):\n${detail}`);
  }
  if (errors.length > 0) {
    throw new Error(`Mock provider assertions failed:\n${errors.join('\n')}`);
  }
}
