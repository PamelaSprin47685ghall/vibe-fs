/**
 * strict-mock-satisfy.js — expectSatisfied() implementation for
 * StrictMockProvider. Kept in its own module so the main provider
 * class file stays under the 200-line Kolmogorov line budget.
 */

import { extractToolNames, extractLastUserMsg } from './strict-mock-matches.js';
import { edgeLabel, pendingExpectations } from './strict-mock-forest.js';

const PREVIEW_LIMIT = 5;

export function checkSatisfied(state) {
  const errors = [];
  if (state.fatal) {
    const kind = state.fatal.reason === 'prefix-cache-invalidated'
      ? 'PREFIX CACHE INVALIDATED'
      : 'FIRST SCRIPT MISMATCH';
    errors.push(
      `${kind} (mock stopped): reason=${state.fatal.reason} session=${state.fatal.sessionId} lastUser=${JSON.stringify(state.fatal.lastUser)} candidates=${JSON.stringify(state.fatal.candidates)}`,
    );
  }
  const expectations = pendingExpectations(state).filter((e) => e.blocking !== false);
  const remaining = expectations.length;
  const unexpectedCount = state.unexpected.length;
  if (remaining > 0) {
    const detail = expectations.slice(0, PREVIEW_LIMIT).map((e) =>
      `  [${e.id}] edge=${edgeLabel(e)} respond=${e.respond.type} match=${JSON.stringify(e.match)}`,
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
