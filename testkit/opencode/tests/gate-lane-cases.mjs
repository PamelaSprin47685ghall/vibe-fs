import { assertEq, assertTrue, postJson } from './gate-lib.mjs';
import { StrictMockProvider } from '../strict-mock-provider.js';

function lane(role, turn, session = role) {
  return { scenario: 'gate', session, role, turn, requestKind: 'chat' };
}

function chat(content, tools = []) {
  return {
    model: 'test-model',
    messages: [{ role: 'user', content }],
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

async function runStrictMockLanes() {
  const provider = new StrictMockProvider();
  provider.strict = true;
  assertEq(typeof provider.allowOutOfOrder, 'undefined', 'global out-of-order bypass must not exist');
  assertEq(typeof provider.allowBloggerRequests, 'undefined', 'Blogger bypass must not exist');
  assertEq(typeof provider.allowTitleGeneration, 'undefined', 'title bypass must not exist');
  assertEq(typeof provider.allowSyntheticContinuations, 'undefined', 'synthetic continuation bypass must not exist');
  const consumed = [];
  provider.onExpectationConsumed = ({ id, lane: consumedLane }) => consumed.push(`${id}:${consumedLane.role}:${consumedLane.turn}`);
  await provider.start();

  try {
    provider.expectToolCall({
      id: 'manager-first',
      lane: lane('manager', 1),
      tool: 'fork',
      args: { agent: 'coder', prompt: 'work' },
      match: { requiredTools: ['fork', 'join', 'list'], containsText: ['manager first'] },
    });
    provider.expectText({
      id: 'manager-second',
      lane: lane('manager', 2),
      text: 'manager done',
      match: { requiredTools: ['fork', 'join', 'list'], containsText: ['manager second'] },
    });
    provider.expectToolCall({
      id: 'coder-write',
      lane: lane('coder', 1),
      tool: 'write',
      args: { filePath: 'x.txt', content: 'ok\n' },
      match: { requiredTools: ['write'] },
    });

    const coderFirst = await postJson(`${provider.url}/v1/chat/completions`, chat('write x.txt', ['write']));
    assertTrue(coderFirst.ok, 'independent coder lane may run before manager lane');
    const managerFirst = await postJson(`${provider.url}/v1/chat/completions`, chat('manager first', ['fork', 'join', 'list']));
    assertTrue(managerFirst.ok, 'manager lane head should match after coder interleaving');
    const managerSecond = await postJson(`${provider.url}/v1/chat/completions`, chat('manager second', ['fork', 'join', 'list']));
    assertTrue(managerSecond.ok, 'manager lane second turn should follow its first turn');
    assertEq(provider.remainingExpectations, 0, 'all lane expectations should be consumed');
    assertEq(consumed.join(','), 'coder-write:coder:1,manager-first:manager:1,manager-second:manager:2', 'consumption records causal lane order');
    provider.expectSatisfied();

    const noExpRes = await postJson(`${provider.url}/v1/chat/completions`, chat('unexpected blogger', []));
    assertEq(noExpRes.status, 500, 'empty queue should return 500');
    assertEq(provider.unexpectedRequests.length, 1, 'empty queue should record unexpected');

    let satisfiedThrew = false;
    try { provider.expectSatisfied(); } catch (err) {
      satisfiedThrew = true;
      assertTrue(err.message.includes('unexpected'), 'expectSatisfied should fail on unexpected request');
    }
    assertTrue(satisfiedThrew, 'expectSatisfied must throw');

    const order = new StrictMockProvider();
    order.strict = true;
    await order.start();
    try {
      order.expectText({
        id: 'order-first',
        lane: lane('manager', 1, 'order'),
        text: 'first',
        match: { requiredTools: ['fork', 'join', 'list'], containsText: ['manager first'] },
      });
      order.expectText({
        id: 'order-second',
        lane: lane('manager', 2, 'order'),
        text: 'second',
        match: { requiredTools: ['fork', 'join', 'list'], containsText: ['manager second'] },
      });
      const mismatchRes = await postJson(`${order.url}/v1/chat/completions`, chat('manager second', ['fork', 'join', 'list']));
      assertEq(mismatchRes.status, 500, 'later turn must not bypass its lane head');
      assertEq(order.remainingExpectations, 2, 'lane-order mismatch must not consume expectation');
    } finally {
      await order.stop();
    }

    const missing = new StrictMockProvider();
    missing.strict = true;
    await missing.start();
    try {
      missing.expectText({
        id: 'missing-lane',
        lane: lane('manager', 1, 'missing'),
        text: 'missing',
        match: { requiredTools: ['fork', 'join', 'list'] },
      });
      let missingThrew = false;
      try { missing.expectSatisfied(); } catch (err) {
        missingThrew = true;
        assertTrue(err.message.includes('remaining expectations'), 'missing expectation should report remaining lane head');
      }
      assertTrue(missingThrew, 'missing expectation must fail expectSatisfied');
    } finally {
      await missing.stop();
    }
  } finally {
    await provider.stop();
  }
}

async function runSessionBoundLanes() {
  const provider = new StrictMockProvider();
  await provider.start();
  try {
    provider.bindSession('parent', 'parent-1');
    provider.expectText({
      id: 'child-first',
      lane: { ...lane('coder', 1, 'child'), parentSession: 'parent' },
      text: 'first',
      match: { requiredTools: ['write'], containsText: ['first child turn'] },
    });
    provider.afterExpectation('child-first', () => {
      provider.expectText({
        id: 'child-second',
        lane: { ...lane('coder', 2, 'child'), parentSession: 'parent' },
        text: 'second',
        match: { requiredTools: ['write'], containsText: ['second child turn'] },
      });
    });

    const headers = {
      'x-session-affinity': 'child-1',
      'x-parent-session-id': 'parent-1',
    };
    const tools = [{ type: 'function', function: { name: 'write' } }];
    const firstBody = {
      model: 'test-model',
      tools,
      messages: [{ role: 'user', content: 'first child turn' }],
    };
    const first = await postJson(`${provider.url}/v1/chat/completions`, firstBody, headers);
    assertTrue(first.ok, 'parent-bound child lane must accept its first request');
    // Same session: full provider-visible history must keep sealed prefix (append-only).
    const secondBody = {
      model: 'test-model',
      tools,
      messages: [
        { role: 'user', content: 'first child turn' },
        { role: 'assistant', content: 'first' },
        { role: 'user', content: 'second child turn' },
      ],
    };
    const second = await postJson(`${provider.url}/v1/chat/completions`, secondBody, headers);
    assertTrue(second.ok, 'afterExpectation must register the causal successor atomically');
    provider.expectSatisfied();
  } finally {
    await provider.stop();
  }

  const wrongParent = new StrictMockProvider();
  await wrongParent.start();
  try {
    wrongParent.bindSession('parent', 'parent-1');
    wrongParent.expectText({
      id: 'child',
      lane: { ...lane('coder', 1, 'child'), parentSession: 'parent' },
      text: 'never',
      match: { requiredTools: ['write'] },
    });
    const rejected = await postJson(
      `${wrongParent.url}/v1/chat/completions`,
      chat('wrong parent', ['write']),
      { 'x-session-affinity': 'child-1', 'x-parent-session-id': 'other-parent' },
    );
    assertEq(rejected.status, 500, 'lane must reject a child with another parent session');
  } finally {
    await wrongParent.stop();
  }
}

async function runSyntheticSessionLane() {
  const provider = new StrictMockProvider();
  await provider.start();
  try {
    provider.bindSession('inspector', 'inspector-1');
    provider.expectText({
      id: 'inspector-zwsp',
      lane: { ...lane('synthetic', 1, 'inspector'), requestKind: 'synthetic' },
      text: 'continued',
      match: { containsText: ['\u200B'] },
    });

    const response = await postJson(
      `${provider.url}/v1/chat/completions`,
      chat('\u200B', ['executor']),
      { 'x-session-affinity': 'inspector-1' },
    );
    assertTrue(response.ok, 'session-bound zero-width request must consume its explicit synthetic lane');
    provider.expectSatisfied();
  } finally {
    await provider.stop();
  }
}

// neverEnd SSE responses keep the stream open; read status without waiting for body.
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

async function runNeverEndStaysPending() {
  const provider = new StrictMockProvider();
  await provider.start();
  try {
    provider.expectText({
      id: 'neverend-alive',
      lane: lane('manager', 1),
      neverEnd: true,
      blocking: false,
      text: 'neverend response',
      match: { containsText: ['hello from neverend'] },
    });
    const first = await postJsonNoBody(`${provider.url}/v1/chat/completions`, chat('hello from neverend', ['fork', 'join', 'list']));
    assertTrue(first.ok, 'neverEnd: first request must match');
    assertEq(provider.remainingExpectations, 1, 'neverEnd: one expectation remains after first match');
    const second = await postJsonNoBody(`${provider.url}/v1/chat/completions`, chat('hello from neverend', ['fork', 'join', 'list']));
    assertTrue(second.ok, 'neverEnd: second request must match again');
    assertEq(provider.remainingExpectations, 1, 'neverEnd: one expectation remains after second match');
    provider.expectSatisfied();
  } finally {
    await provider.stop();
  }
}

async function runNeverEndBlocksSameLaneTurn() {
  const provider = new StrictMockProvider();
  await provider.start();
  try {
    provider.expectText({
      id: 'neverend-blocker',
      lane: lane('manager', 1),
      neverEnd: true,
      blocking: false,
      text: 'neverend',
      match: { containsText: ['neverend content'] },
    });
    provider.expectText({
      id: 'blocked-turn-2',
      lane: lane('manager', 2),
      text: 'turn2',
      match: { containsText: ['turn 2 content'] },
    });
    // Match the neverEnd head
    const first = await postJsonNoBody(`${provider.url}/v1/chat/completions`, chat('neverend content', ['fork', 'join', 'list']));
    assertTrue(first.ok, 'neverEnd blocks: first request matches neverEnd head');
    // Turn 2 is behind neverEnd head — unreachable
    const second = await postJson(`${provider.url}/v1/chat/completions`, chat('turn 2 content', ['fork', 'join', 'list']));
    assertEq(second.status, 500, 'neverEnd blocks: turn 2 behind neverEnd head must 500');
    assertEq(provider.remainingExpectations, 2, 'neverEnd blocks: both expectations still remain');
  } finally {
    await provider.stop();
  }
}

async function runBloggerDistinctMarkers() {
  const provider = new StrictMockProvider();
  await provider.start();
  try {
    // Two unbound blogger heads in separate lane queues with different markers
    provider.expectText({
      id: 'blogger-orch',
      lane: lane('blogger', 1, 'orch-blogger'),
      neverEnd: true,
      blocking: false,
      text: 'orch bg',
      match: { containsText: ['orchestrator blog activity'] },
    });
    provider.expectText({
      id: 'blogger-manager',
      lane: lane('blogger', 1, 'manager-blogger'),
      neverEnd: true,
      blocking: false,
      text: 'manager bg',
      match: { containsText: ['manager blog activity'] },
    });
    // Request with orch marker — only orch-blogger matches
    const orchReq = await postJsonNoBody(`${provider.url}/v1/chat/completions`, bloggerChat('orchestrator blog activity'));
    assertTrue(orchReq.ok, 'blogger distinct: orch request matches orch blogger');
    // Request with manager marker — only manager-blogger matches
    const mgrReq = await postJsonNoBody(`${provider.url}/v1/chat/completions`, bloggerChat('manager blog activity'));
    assertTrue(mgrReq.ok, 'blogger distinct: manager request matches manager blogger');
    provider.expectSatisfied();
  } finally {
    await provider.stop();
  }
}

async function runBloggerAmbiguousMarkers() {
  const provider = new StrictMockProvider();
  await provider.start();
  try {
    // Two unbound blogger heads with identical containsText
    provider.expectText({
      id: 'blogger-a',
      lane: lane('blogger', 1, 'blogger-a'),
      neverEnd: true,
      blocking: false,
      text: 'a bg',
      match: { containsText: ['shared marker'] },
    });
    provider.expectText({
      id: 'blogger-b',
      lane: lane('blogger', 1, 'blogger-b'),
      neverEnd: true,
      blocking: false,
      text: 'b bg',
      match: { containsText: ['shared marker'] },
    });
    // Both match — ambiguous
    const res = await postJson(`${provider.url}/v1/chat/completions`, bloggerChat('shared marker'));
    assertEq(res.status, 500, 'blogger ambiguous: identical markers must 500');
    const unexp = provider.unexpectedRequests;
    assertTrue(unexp.length >= 1, 'blogger ambiguous: must record unexpected');
    assertTrue(unexp.some((u) => u.reason === 'ambiguous-lane-heads'), 'blogger ambiguous: reason must be ambiguous-lane-heads');
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
      match: { containsText: ['stable system prefix'] },
    });
    provider.expectText({
      id: 'turn-2',
      lane: lane('manager', 2),
      text: 'ok2',
      match: { containsText: ['appended user turn'] },
    });
    const body1 = {
      model: 'test-model',
      tools: [{ function: { name: 'fork' } }, { function: { name: 'join' } }, { function: { name: 'list' } }],
      messages: [
        { role: 'system', content: 'stable system prefix' },
        { role: 'user', content: 'stable system prefix first user' },
      ],
      sessionId: 'prefix-session-1',
    };
    const first = await postJson(`${provider.url}/v1/chat/completions`, body1, {
      'x-session-affinity': 'prefix-session-1',
    });
    assertTrue(first.ok, 'prefix: first request matches');

    // Mutate sealed system message — must fail closed as prefix-cache-invalidated.
    const body2 = {
      model: 'test-model',
      tools: [{ function: { name: 'fork' } }, { function: { name: 'join' } }, { function: { name: 'list' } }],
      messages: [
        { role: 'system', content: 'MUTATED system prefix' },
        { role: 'user', content: 'stable system prefix first user' },
        { role: 'user', content: 'appended user turn' },
      ],
      sessionId: 'prefix-session-1',
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
      match: { containsText: ['append-ok first'] },
    });
    provider.expectText({
      id: 'turn-2',
      lane: lane('manager', 2),
      text: 'ok2',
      match: { containsText: ['append-ok second'] },
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

export const laneCases = [
  { name: 'strict mock lanes and unexpected requests', fn: runStrictMockLanes },
  { name: 'session-bound lanes and causal successors', fn: runSessionBoundLanes },
  { name: 'session-bound synthetic continuation lane', fn: runSyntheticSessionLane },
  { name: 'neverEnd stays pending and re-matches second request', fn: runNeverEndStaysPending },
  { name: 'neverEnd head prevents later same-lane turn from matching', fn: runNeverEndBlocksSameLaneTurn },
  { name: 'two unbound blogger heads with different markers match distinctly', fn: runBloggerDistinctMarkers },
  { name: 'two unbound blogger heads with identical markers produce ambiguous-lane-heads', fn: runBloggerAmbiguousMarkers },
  { name: 'prefix cache invalidation fails closed', fn: runPrefixCacheInvalidation },
  { name: 'prefix cache append-only succeeds', fn: runPrefixCacheAppendOk },
];
