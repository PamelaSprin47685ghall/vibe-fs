/**
 * Exact e2e event ceilings: journal envelopes + SSE (no heartbeat) fail closed on loops.
 */
import assert from 'node:assert/strict';
import test from 'node:test';
import {
  attachEventCeilings,
  eventCeilingSetupProblems,
  isCountedSseEvent,
  normalizeEventCeilings,
} from './e2e/support/event-ceiling.js';
import { resolveEntry } from './e2e/support/runtime-key.js';
import { compileScenario } from './e2e/support/scenario-schema.js';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

test('WHAT[VERIFICATION-SYSTEM-003] isCountedSseEvent excludes server.heartbeat only', () => {
  assert.equal(isCountedSseEvent({ type: 'server.heartbeat' }), false);
  assert.equal(isCountedSseEvent({ type: 'message.updated' }), true);
  assert.equal(isCountedSseEvent({ type: '' }), false);
  assert.equal(isCountedSseEvent({}), false);
});

test('WHAT[VERIFICATION-SYSTEM-003] normalizeEventCeilings rejects non-positive integers', () => {
  assert.deepEqual(normalizeEventCeilings({}), {});
  assert.deepEqual(normalizeEventCeilings({ maxJournalEvents: 12, maxSseEvents: 34 }), {
    maxJournalEvents: 12,
    maxSseEvents: 34,
  });
  assert.throws(() => normalizeEventCeilings({ maxJournalEvents: 0 }), /positive integer/);
  assert.throws(() => normalizeEventCeilings({ maxSseEvents: 1.2 }), /positive integer/);
});

test('WHAT[VERIFICATION-SYSTEM-003] eventCeilingSetupProblems matches schema contract', () => {
  assert.deepEqual(eventCeilingSetupProblems(undefined), []);
  assert.ok(eventCeilingSetupProblems({ maxJournalEvents: 0 })[0].includes('maxJournalEvents'));
  assert.ok(eventCeilingSetupProblems({ maxSseEvents: -3 })[0].includes('maxSseEvents'));
});

test('WHAT[VERIFICATION-SYSTEM-003] attachEventCeilings breaches maxSseEvents without counting heartbeats', () => {
  const listeners = [];
  const scenario = {
    host: { workDir: '/tmp/does-not-need-to-exist-for-sse-only' },
    events: {
      allEvents: [
        { type: 'message.updated' },
        { type: 'server.heartbeat' },
        { type: 'sync' },
      ],
      onEvent(cb) {
        listeners.push(cb);
        return () => {
          const i = listeners.indexOf(cb);
          if (i >= 0) listeners.splice(i, 1);
        };
      },
      dump: () => '',
    },
    watchdog: { stop() {} },
  };

  let breached = null;
  // Journal ceiling omitted — avoid touching a real workDir tip.
  attachEventCeilings(
    scenario,
    { maxSseEvents: 2 },
    {
      onBreach: (detail) => {
        breached = detail;
        throw new Error('ceiling');
      },
    },
  );
  // Already had 2 counted frames at attach; next counted frame breaches.
  assert.equal(breached, null);
  assert.throws(() => listeners[0]({ type: 'session.idle' }), /ceiling/);
  assert.equal(breached.kind, 'maxSseEvents');
  assert.equal(breached.observed, 3);
  assert.equal(breached.limit, 2);
  // Heartbeat must not increment.
  assert.equal(breached.sseEvents, 3);
});

test('WHAT[VERIFICATION-SYSTEM-003] long-stroke.toml declares theoretical exact event ceilings', () => {
  const dir = path.dirname(fileURLToPath(import.meta.url));
  const source = readFileSync(path.join(dir, 'e2e/scenarios/long-stroke.toml'), 'utf8');
  const result = compileScenario(source, { name: 'long-stroke.toml' });
  assert.equal(result.ok, true, result.ok ? '' : result.problems.join('\n'));
  // Explicit preflow authority, real K2 traffic, detached Bookkeeper finalization,
  // and same-physical dual-PERFECT review measured 622-647 durable envelopes and
  // 3118-3351 SSE frames. Pins 700/3450 retain Host-ordering slack while failing
  // before an unconfirmed-reviewer roster can grow quadratically.
  assert.equal(result.scenario.setup.maxJournalEvents, 700);
  assert.equal(result.scenario.setup.maxSseEvents, 3450);
});

test('WHAT[VERIFICATION-SYSTEM-003] Long Stroke selects one exact Manager suffix without moving its sole fault', () => {
  const dir = path.dirname(fileURLToPath(import.meta.url));
  const source = readFileSync(path.join(dir, 'e2e/scenarios/long-stroke.toml'), 'utf8');
  const result = compileScenario(source, { name: 'long-stroke.toml' });
  assert.equal(result.ok, true, result.ok ? '' : result.problems.join('\n'));

  const byId = new Map(result.scenario.entries.map((entry) => [entry.id, entry]));
  const ordinary = byId.get('manager.1');

  assert.deepEqual(
    { optional: ordinary?.optional, lane: ordinary?.lane, step: ordinary?.step },
    { optional: false, lane: 'manager', step: 1 },
  );
  assert.deepEqual(
    result.scenario.faults.filter((fault) => fault.kind === 'provider-error' && fault.status === 400)
      .map((fault) => fault.entryId),
    ['manager.1'],
  );

  const managerTools = ['fork', 'join', 'horizon', 'fission', 'todowrite', 'suicide'];
  const request = (turn, step) => ({
    messages: [
      { role: 'user', content: turn },
      ...Array.from({ length: step }, (_, index) => ({ role: 'assistant', content: `reply-${index}` })),
    ],
    tools: managerTools.map((name) => ({ name })),
  });
  const bindings = new Map([['manager', 'ses_manager']]);
  const context = { sessionId: 'ses_manager' };

  assert.equal(
    resolveEntry(request('Continue after the interrupted join.', 1), result.scenario.entries, bindings, context).matched?.id,
    'manager-resume.1',
  );
  assert.equal(
    resolveEntry(request('# Work remains away.', 1), result.scenario.entries, bindings, context).matched?.id,
    'manager-join-guard.0',
  );

  const suffixWaits = result.scenario.flow.filter((step) => step.waitAny).map((step) => step.waitAny);
  assert.equal(suffixWaits.length, 10);
  for (let index = 0; index < 10; index += 1) {
    const originalId = `manager-resume.${index + 1}`;
    const guardedId = `manager-join-guard.${index}`;
    const original = byId.get(originalId);
    const guarded = byId.get(guardedId);

    assert.deepEqual(suffixWaits[index], [originalId, guardedId]);
    assert.deepEqual(guarded?.respond, original?.respond);
    assert.deepEqual(
      {
        optional: guarded?.optional,
        internal: guarded?.internal,
        step: guarded?.step,
        tools: guarded?.tools,
        forbiddenTools: guarded?.forbiddenTools,
      },
      {
        optional: true,
        internal: true,
        step: index + 1,
        tools: managerTools,
        forbiddenTools: ['commission'],
      },
    );
  }

  assert.ok(result.scenario.must.includes('manager.1'));
  assert.ok(result.scenario.must.includes('manager-resume.0'));
  assert.ok(!result.scenario.must.some((id) => /^manager-resume\.(?:[1-9]|10)$/.test(id)));
  assert.ok(!result.scenario.must.some((id) => id.startsWith('manager-join-guard.')));
});
