/**
 * Script forest contract tests (AGENTS.md KISS-N11).
 * File name kept for gate-testkit discovery; semantics are prefix/content forest.
 */
import { assertEq, assertTrue, postJson } from './gate-lib.mjs';
import { StrictMockProvider } from '../strict-mock-provider.js';

// neverEnd SSE keeps stream open; read status without waiting for body.
async function postJsonNoBody(url, body, headers = {}) {
  const res = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...headers },
    body: JSON.stringify(body),
  });
  const status = res.status;
  res.body?.cancel();
  return { status, ok: status >= 200 && status < 300 };
}


function lane(role, turn = 1, session = role) {
  return { scenario: 'gate', session, role, turn, requestKind: 'chat' };
}

function chat(content, tools = [], extraMessages = []) {
  return {
    model: 'test-model',
    messages: [...extraMessages, { role: 'user', content }],
    tools: tools.map((name) => ({ type: 'function', function: { name } })),
  };
}

function bloggerChat(content) {
  return {
    model: 'test-model',
    messages: [{ role: 'user', content: 'You are the blogger of a coding agent session.\n' + content }],
    tools: [],
  };
}

async function runForestIndependentPaths() {
  const provider = new StrictMockProvider();
  await provider.start();
  try {
    provider.expectToolCall({
      id: 'manager-first',
      lane: lane('manager', 1),
      tool: 'fork',
      args: { agent: 'coder', prompt: 'work' },
      match: { requiredTools: ['fork', 'join', 'list'], user: 'manager first' },
    });
    provider.expectText({
      id: 'manager-second',
      lane: lane('manager', 2),
      text: 'manager done',
      match: { requiredTools: ['fork', 'join', 'list'], user: 'manager second' },
    });
    provider.expectToolCall({
      id: 'coder-write',
      lane: lane('coder', 1),
      tool: 'write',
      args: { filePath: 'x.txt', content: 'ok\n' },
      match: { requiredTools: ['write'], user: 'write x.txt' },
    });

    const coderFirst = await postJson(`${provider.url}/v1/chat/completions`, chat('write x.txt', ['write']));
    assertTrue(coderFirst.ok, 'independent coder path may run before manager');
    const managerFirst = await postJson(`${provider.url}/v1/chat/completions`, chat('manager first', ['fork', 'join', 'list']));
    assertTrue(managerFirst.ok, 'manager first user matches');
    const managerSecond = await postJson(`${provider.url}/v1/chat/completions`, chat('manager second', ['fork', 'join', 'list']));
    assertTrue(managerSecond.ok, 'manager second user matches without turn queue');
    const again = await postJson(`${provider.url}/v1/chat/completions`, chat('manager first', ['fork', 'join', 'list']));
    assertTrue(again.ok, 'same prefix is idempotent (no mute)');
    provider.expectSatisfied();
  } finally {
    await provider.stop();
  }
}

async function runIdempotentSamePrefix() {
  const provider = new StrictMockProvider();
  await provider.start();
  try {
    provider.expectText({
      id: 'once',
      lane: lane('manager', 1),
      text: 'hello',
      match: { requiredTools: ['fork', 'join', 'list'], user: 'idempotent ping' },
    });
    const body = chat('idempotent ping', ['fork', 'join', 'list']);
    const a = await postJson(`${provider.url}/v1/chat/completions`, body);
    const b = await postJson(`${provider.url}/v1/chat/completions`, body);
    assertTrue(a.ok && b.ok, 'identical requests both succeed');
    // blocking edge observed → satisfied even if "pending" uses observed set
    provider.expectSatisfied();
  } finally {
    await provider.stop();
  }
}

async function runAmbiguousPrefixRejected() {
  const provider = new StrictMockProvider();
  await provider.start();
  try {
    provider.expectText({
      id: 'a',
      lane: lane('manager', 1, 'm-a'),
      text: 'A',
      match: { requiredTools: ['fork', 'join', 'list'], user: 'shared marker' },
    });
    provider.expectText({
      id: 'b',
      lane: lane('manager', 1, 'm-b'),
      text: 'B',
      match: { requiredTools: ['fork', 'join', 'list'], user: 'shared marker' },
    });
    const res = await postJson(`${provider.url}/v1/chat/completions`, chat('shared marker', ['fork', 'join', 'list']));
    assertEq(res.status, 500, 'ambiguous equal-specificity templates must 500');
    assertTrue(
      provider.unexpectedRequests.some((u) => u.reason === 'ambiguous-prefix' || u.reason === 'ambiguous-lane-heads'),
      'reason must be ambiguous-prefix',
    );
  } finally {
    await provider.stop();
  }
}

async function runUserForkDisambiguates() {
  const provider = new StrictMockProvider();
  await provider.start();
  try {
    provider.expectText({
      id: 'job-a',
      lane: lane('manager', 1, 'job-a'),
      text: 'A',
      match: { requiredTools: ['fork', 'join', 'list'], user: 'Ship job-A' },
    });
    provider.expectText({
      id: 'job-b',
      lane: lane('manager', 1, 'job-b'),
      text: 'B',
      match: { requiredTools: ['fork', 'join', 'list'], user: 'Ship job-B' },
    });
    const a = await postJson(`${provider.url}/v1/chat/completions`, chat('Ship job-A please', ['fork', 'join', 'list']));
    const b = await postJson(`${provider.url}/v1/chat/completions`, chat('Ship job-B please', ['fork', 'join', 'list']));
    assertTrue(a.ok && b.ok, 'user-text fork disambiguates parallel paths');
    provider.expectSatisfied();
  } finally {
    await provider.stop();
  }
}

async function runNeverEndStillIdempotent() {
  const provider = new StrictMockProvider();
  await provider.start();
  try {
    provider.expectText({
      id: 'blog',
      lane: { ...lane('blogger', 1), requestKind: 'chat' },
      neverEnd: true,
      blocking: false,
      text: 'bg',
      match: { user: 'You are the blogger' },
    });
    const r1 = await postJsonNoBody(`${provider.url}/v1/chat/completions`, bloggerChat('delta one'));
    const r2 = await postJsonNoBody(`${provider.url}/v1/chat/completions`, bloggerChat('delta two'));
    assertTrue(r1.ok && r2.ok, 'blogger template rematches without mute');
    provider.expectSatisfied();
  } finally {
    await provider.stop();
  }
}

async function runPrefixCacheInvalidation() {
  const provider = new StrictMockProvider();
  await provider.start();
  try {
    provider.expectText({
      id: 'turn-1',
      lane: lane('manager', 1),
      text: 'ok1',
      match: { user: 'stable system prefix' },
    });
    provider.expectText({
      id: 'turn-2',
      lane: lane('manager', 2),
      text: 'ok2',
      match: { user: 'appended user turn' },
    });
    const tools = [
      { type: 'function', function: { name: 'fork' } },
      { type: 'function', function: { name: 'join' } },
      { type: 'function', function: { name: 'list' } },
    ];
    const body1 = {
      model: 'test-model',
      tools,
      messages: [
        { role: 'system', content: 'stable system prefix' },
        { role: 'user', content: 'stable system prefix first user' },
      ],
    };
    const first = await postJson(`${provider.url}/v1/chat/completions`, body1, {
      'x-session-affinity': 'prefix-session-1',
    });
    assertTrue(first.ok, 'prefix: first request matches');

    const body2 = {
      model: 'test-model',
      tools,
      messages: [
        { role: 'system', content: 'MUTATED system prefix' },
        { role: 'user', content: 'stable system prefix first user' },
        { role: 'user', content: 'appended user turn' },
      ],
    };
    const second = await postJson(`${provider.url}/v1/chat/completions`, body2, {
      'x-session-affinity': 'prefix-session-1',
    });
    assertEq(second.status, 500, 'prefix: mutated sealed prefix must 500');
    assertTrue(
      provider.unexpectedRequests.some((u) => u.reason === 'prefix-cache-invalidated'),
      'prefix: reason must be prefix-cache-invalidated',
    );
  } finally {
    await provider.stop();
  }
}

async function runPrefixCacheAppendOk() {
  const provider = new StrictMockProvider();
  await provider.start();
  try {
    provider.expectText({
      id: 'turn-1',
      lane: lane('manager', 1),
      text: 'ok1',
      match: { user: 'append-ok first' },
    });
    provider.expectText({
      id: 'turn-2',
      lane: lane('manager', 2),
      text: 'ok2',
      match: { user: 'append-ok second' },
    });
    const tools = [
      { type: 'function', function: { name: 'fork' } },
      { type: 'function', function: { name: 'join' } },
      { type: 'function', function: { name: 'list' } },
    ];
    const body1 = {
      model: 'test-model',
      tools,
      messages: [
        { role: 'system', content: 'append-ok system' },
        { role: 'user', content: 'append-ok first' },
      ],
    };
    const first = await postJson(`${provider.url}/v1/chat/completions`, body1, {
      'x-session-affinity': 'prefix-session-2',
    });
    assertTrue(first.ok, 'prefix append: first ok');
    const body2 = {
      model: 'test-model',
      tools,
      messages: [
        { role: 'system', content: 'append-ok system' },
        { role: 'user', content: 'append-ok first' },
        { role: 'assistant', content: 'ok1' },
        { role: 'user', content: 'append-ok second' },
      ],
    };
    const second = await postJson(`${provider.url}/v1/chat/completions`, body2, {
      'x-session-affinity': 'prefix-session-2',
    });
    assertTrue(second.ok, 'prefix append: second ok when sealed prefix preserved');
    provider.expectSatisfied();
  } finally {
    await provider.stop();
  }
}

async function runMissingEdgeFailsSatisfied() {
  const provider = new StrictMockProvider();
  await provider.start();
  try {
    provider.expectText({
      id: 'never-hit',
      lane: lane('manager', 1),
      text: 'x',
      match: { requiredTools: ['fork', 'join', 'list'], user: 'never sent' },
    });
    let threw = false;
    try {
      provider.expectSatisfied();
    } catch (err) {
      threw = true;
      assertTrue(err.message.includes('remaining'), 'must report remaining edges');
    }
    assertTrue(threw, 'unobserved blocking edge fails satisfied');
  } finally {
    await provider.stop();
  }
}

async function runHistorySplitsSameLastUser() {
  // Same lastUser phrase, different full history → different seals; both use same template edge (idempotent template).
  const provider = new StrictMockProvider();
  await provider.start();
  try {
    provider.expectToolCall({
      id: 'review-first',
      lane: lane('reviewer', 1),
      tool: 'verdict',
      args: { verdict: 'PERFECT' },
      match: { requiredTools: ['verdict'], user: 'Review the current worktree' },
    });
    provider.expectToolCall({
      id: 'review-confirm',
      lane: lane('reviewer', 2),
      tool: 'verdict',
      args: { verdict: 'PERFECT' },
      match: { requiredTools: ['verdict'], user: 'PERFECT requires confirmation' },
    });
    const tools = [{ type: 'function', function: { name: 'verdict' } }];
    const first = await postJson(`${provider.url}/v1/chat/completions`, {
      model: 'test-model',
      tools,
      messages: [{ role: 'user', content: 'Review the current worktree for correctness.' }],
    }, { 'x-session-affinity': 'rev-1' });
    assertTrue(first.ok, 'first review matches');
    const confirm = await postJson(`${provider.url}/v1/chat/completions`, {
      model: 'test-model',
      tools,
      messages: [
        { role: 'user', content: 'Review the current worktree for correctness.' },
        { role: 'assistant', content: null, tool_calls: [{ id: 'c1', type: 'function', function: { name: 'verdict', arguments: '{}' } }] },
        { role: 'tool', tool_call_id: 'c1', content: 'NEEDS_REVIEW' },
        { role: 'user', content: 'PERFECT requires confirmation. Call again.' },
      ],
    }, { 'x-session-affinity': 'rev-1' });
    assertTrue(confirm.ok, 'confirm user matches different edge');
    // Same first-user request shape on another session (post-rebase style) — same template, ok
    const again = await postJson(`${provider.url}/v1/chat/completions`, {
      model: 'test-model',
      tools,
      messages: [{ role: 'user', content: 'Review the current worktree for correctness.' }],
    }, { 'x-session-affinity': 'rev-2' });
    assertTrue(again.ok, 'identical first-user template is reusable across sessions');
    provider.expectSatisfied();
  } finally {
    await provider.stop();
  }
}

export const laneCases = [
  { name: 'forest independent content paths', fn: runForestIndependentPaths },
  { name: 'identical prefix is idempotent', fn: runIdempotentSamePrefix },
  { name: 'ambiguous equal templates rejected', fn: runAmbiguousPrefixRejected },
  { name: 'user-text fork disambiguates parallel jobs', fn: runUserForkDisambiguates },
  { name: 'blogger template rematches', fn: runNeverEndStillIdempotent },
  { name: 'prefix cache invalidation fails closed', fn: runPrefixCacheInvalidation },
  { name: 'prefix cache append-only succeeds', fn: runPrefixCacheAppendOk },
  { name: 'unobserved blocking edge fails satisfied', fn: runMissingEdgeFailsSatisfied },
  { name: 'history splits first vs confirm review', fn: runHistorySplitsSameLastUser },
];
