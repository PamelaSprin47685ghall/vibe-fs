/**
 * strict-mock-satisfy.js — expectSatisfied() implementation for
 * StrictMockProvider. Kept in its own module so the main provider
 * class file stays under the 200-line Kolmogorov line budget.
 */

import { extractToolNames, extractLastUserMsg } from './strict-mock-matches.js';

const PREVIEW_LIMIT = 5;

export function checkSatisfied(expectations, unexpected) {
  const remaining = expectations.length;
  const unexpectedCount = unexpected.length;
  const errors = [];
  if (remaining > 0) {
    const detail = expectations.slice(0, PREVIEW_LIMIT).map((e) =>
      `  [${e.id}] respond=${e.respond.type} match=${JSON.stringify(e.match)}`,
    ).join('\n');
    errors.push(`${remaining} unmatched expectation(s):\n${detail}`);
  }
  if (unexpectedCount > 0) {
    const detail = unexpected.slice(0, PREVIEW_LIMIT).map((u) =>
      `  session=${u.sessId || '?'} tools=${JSON.stringify(extractToolNames(u.body))} msgs=${u.body?.messages?.length || 0} toolResults=${u.hasToolResults || false} lastUser=${extractLastUserMsg(u.body) || '(none)'} reason=${u.reason || '?'}`,
    ).join('\n');
    errors.push(`${unexpectedCount} unexpected LLM request(s):\n${detail}`);
  }
  if (errors.length > 0) {
    throw new Error(`Mock provider assertions failed:\n${errors.join('\n')}`);
  }
}
