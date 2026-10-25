import assert from 'node:assert';
import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const EventsSurface = await import("../../../dist/OpenCode/Host/EventsSurface.js");

const notify = (port, sessionId, outcome) => EventsSurface.notify(port, sessionId, outcome.kind, outcome.providerRun ?? '', outcome.error ?? outcome.value ?? '')
const completed = (providerRun = '') => ({ kind: 'Completed', providerRun })
const failed = (error) => ({ kind: 'Failed', error })
const aborted = (reason) => ({ kind: 'Aborted', error: reason })

test('WHAT[host-boundary-016] EVT_duplicate_completed_for_the_same_provider_run_is_absorbed', () => {
  const port = EventsSurface.create()
  const received = []
  EventsSurface.subscribe(port, (sessionId, outcome) => received.push({ sessionId, outcome }))
  assert.equal(notify(port, 'ses_dup', completed('run-1')), true)
  assert.equal(notify(port, 'ses_dup', completed('run-1')), false)
  assert.equal(received.length, 1)
})
test('WHAT[host-boundary-016] EVT_completed_without_provider_run_is_never_a_duplicate', () => {
  const port = EventsSurface.create()
  const received = []
  EventsSurface.subscribe(port, () => received.push(true))
  notify(port, 'ses_norun', completed())
  notify(port, 'ses_norun', completed())
  assert.equal(received.length, 2)
})
test('WHAT[host-boundary-016] EVT_failed_and_aborted_outcomes_are_not_deduped', () => {
  const port = EventsSurface.create()
  const received = []
  EventsSurface.subscribe(port, (_, outcome) => received.push(outcome.kind))
  notify(port, 'ses_failure', failed('e1'))
  notify(port, 'ses_failure', failed('e1'))
  notify(port, 'ses_abort', aborted('cancelled'))
  notify(port, 'ses_abort', aborted('cancelled'))
  assert.deepEqual(received, ['Failed', 'Failed', 'Aborted', 'Aborted'])
})
test('WHAT[host-boundary-016] EVT_terminal_notification_fans_out_once_to_each_live_physical_listener', () => {
  const port = EventsSurface.create()
  const first = []
  const second = []
  EventsSurface.subscribeFuture(port, (sessionId, outcome) => first.push([sessionId, outcome.kind]))
  EventsSurface.subscribeFuture(port, (sessionId, outcome) => second.push([sessionId, outcome.kind]))

  notify(port, 'ses_fanout', failed('diagnostic'))

  assert.deepEqual(first, [['ses_fanout', 'Failed']])
  assert.deepEqual(second, [['ses_fanout', 'Failed']])
})
test('WHAT[host-boundary-016] EVT_late_subscriber_replays_the_last_sticky_outcome_per_session', () => {
  const port = EventsSurface.create()
  notify(port, 'ses_replay_a', completed('run-a'))
  notify(port, 'ses_replay_b', failed('error-b'))
  const replayed = []
  EventsSurface.subscribe(port, (sessionId, outcome) => replayed.push([sessionId, outcome.kind]))
  assert.deepEqual(replayed.sort(), [['ses_replay_a', 'Completed'], ['ses_replay_b', 'Failed']])
})
test('WHAT[host-boundary-016] EVT_disposed_listener_stops_delivery_and_listener_count_reporting', () => {
  const port = EventsSurface.create()
  const received = []
  const subscription = EventsSurface.subscribe(port, (_, outcome) => received.push(outcome.kind))
  notify(port, 'ses_dispose', failed('before'))
  EventsSurface.dispose(subscription)
  notify(port, 'ses_dispose', failed('after'))
  assert.deepEqual(received, ['Failed'])
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const EventsSurface = await import("../../../dist/OpenCode/Host/EventsSurface.js");

const notify = (port, sessionId, outcome) => EventsSurface.notify(port, sessionId, outcome.kind, outcome.providerRun ?? '', outcome.value ?? outcome.error ?? '')

test('WHAT[host-boundary-016] EXEC_join_NotifyTerminal_then_late_SubscribeTerminal_replays_sticky', () => {
  const port = EventsSurface.create()
  notify(port, 'ses_sticky_child', { kind: 'Completed', providerRun: 'run-1', value: 'done' })
  const seen = []
  EventsSurface.subscribe(port, (sessionId, outcome) => seen.push({ sessionId, outcome }))
  assert.equal(seen.length, 1)
  assert.equal(seen[0].sessionId, 'ses_sticky_child')
  assert.equal(seen[0].outcome.text, 'done')
})
test('WHAT[host-boundary-016] EXEC_join_Failed_outcomes_are_not_provider_run_deduped', () => {
  const port = EventsSurface.create()
  let count = 0
  EventsSurface.subscribe(port, () => { count += 1 })
  notify(port, 'ses_dedupe', { kind: 'Failed', providerRun: 'run-1', error: 'first' })
  notify(port, 'ses_dedupe', { kind: 'Failed', providerRun: 'run-1', error: 'second' })
  assert.equal(count, 2)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const EventsSurface = await import("../../../dist/OpenCode/Host/EventsSurface.js");
const ReconcileSurface = await import("../../../dist/Composition/Turn/ReconcileSurface.js");
const HostSignalSubscribeSurface = await import("../../../dist/OpenCode/Host/HostSignalSubscribeSurface.js");

process.env.WANXIANGSHU_NO_FATAL_EXIT = '1'
const notify = (port, sessionId, outcome) => EventsSurface.notify(port, sessionId, outcome.kind, outcome.providerRun ?? '', outcome.error ?? outcome.value ?? '')
const completed = (providerRun = '') => ({ kind: 'Completed', providerRun })
const failed = (error) => ({ kind: 'Failed', error })
const idleWake = ReconcileSurface.idleWake('s1', 1n)

test('WHAT[host-boundary-016] EXEC_events_sticky_terminal_bounded', () => {
  const port = EventsSurface.create()
  // The production HostEventPort has a stickyCap of 256. Notify 300 sessions
  // and verify the port still functions (sticky eviction is internal).
  for (let index = 0; index < 300; index += 1) {
    notify(port, `ses_${index}`, completed(`run_${index}`))
  }
  // A late subscriber gets replayed at most 256 sticky outcomes.
  const replayed = []
  EventsSurface.subscribe(port, (sessionId) => replayed.push(sessionId))
  assert.ok(replayed.length <= 256, `sticky replay must be bounded: got ${replayed.length}`)
  assert.ok(replayed.length > 0, 'sticky replay must not be empty')
})
test('WHAT[host-boundary-016] mutation_canary_duplicate_completed_is_absorbed', () => {
  // The production EventsSurface must absorb duplicate Completed for the
  // same provider run. If dedup is removed, this canary fails.
  const port = EventsSurface.create()
  const received = []
  EventsSurface.subscribe(port, (_, outcome) => received.push(outcome.kind))
  assert.equal(notify(port, 'ses_dup', completed('run-1')), true)
  assert.equal(notify(port, 'ses_dup', completed('run-1')), false)
  assert.equal(received.length, 1)
})
}

test('WHAT[host-boundary-016] 真实 createHost 取第5项 sharedTerminalPort 证明引用同一性，错引用释放不提前移除且完全释放后清理', async () => {
  const { createHost } = await import('../../../dist/OpenCode/Host/PluginHost.js');
  const TerminalWiring = await import('../../../dist/OpenCode/Host/TerminalWiring.js');
  const workspace = '/tmp/test-wxs-pluginhost-' + Math.random().toString(36).slice(2);

  // 1. 第一次实际调用 createHost
  const hostRes1 = await createHost(workspace, {}, undefined, undefined, undefined, undefined);
  assert.ok(hostRes1.tag === 0, 'host1 创建成功');
  const [eventPort1, sessionPort1, snapshotPort1, key1, source1] = hostRes1.fields[0];
  assert.ok(source1, '第5项返回有效的 sharedTerminalPort');
  assert.ok(key1, '返回有效的 terminalKey');

  // 2. 第二次实际调用 createHost，断言取出的第5项在 Object.is 下严格为同一引用（first-wins 共享）
  const hostRes2 = await createHost(workspace, {}, undefined, undefined, undefined, undefined);
  assert.ok(hostRes2.tag === 0, 'host2 创建成功');
  const [eventPort2, sessionPort2, snapshotPort2, key2, source2] = hostRes2.fields[0];
  assert.equal(key1, key2, '相同工作区分配相同的 terminalKey');
  assert.ok(Object.is(source1, source2), '第5项 sharedTerminalPort 严格同一引用，无多余包装破坏');

  // 3. 错引用释放测试（验证防包装泄漏回归）：若用伪装/错误包装的对象释放，不能触发引用计数递减或提前移除
  const fakeWrappedSource = { ...source1 };
  TerminalWiring.release(key1, fakeWrappedSource);

  // 验证条目未被提前移除：再次获取时仍应返回原 source1，而非换新 candidate
  const probeCandidate1 = TerminalWiring.createSource();
  const probe1 = await TerminalWiring.tryAcquireForWorkspace(workspace, probeCandidate1);
  assert.ok(probe1);
  assert.ok(Object.is(probe1[1], source1), '错误引用释放未减少真实引用，条目保留原引用');
  // 平衡 probe1 带来的引用计数增加
  TerminalWiring.release(key1, source1);

  // 4. 释放第一个 host 的正常引用
  TerminalWiring.release(key1, source1);

  // 此时仍有一个 host 引用，再次获取依然应当是原 source1
  const probeCandidate2 = TerminalWiring.createSource();
  const probe2 = await TerminalWiring.tryAcquireForWorkspace(workspace, probeCandidate2);
  assert.ok(probe2);
  assert.ok(Object.is(probe2[1], source1), '未完全释放前依然复用已有条目');
  // 平衡 probe2 增加的计数
  TerminalWiring.release(key1, source1);

  // 5. 释放第二个 host 的正常引用，此时计数真正归零，条目被彻底清理！
  TerminalWiring.release(key2, source2);

  // 6. 验证条目已被彻底清理：重新 acquire 时应登记新 candidate 并返回该新引用，而非旧 source1
  const probeCandidate3 = TerminalWiring.createSource();
  const probe3 = await TerminalWiring.tryAcquireForWorkspace(workspace, probeCandidate3);
  assert.ok(probe3);
  assert.ok(Object.is(probe3[1], probeCandidate3), '条目已彻底清理，新获取返回新 candidate 引用');
  assert.ok(!Object.is(probe3[1], source1), '不再指向已释放的旧 source1');

  // 收尾释放
  TerminalWiring.release(probe3[0], probe3[1]);
});

test('WHAT[host-boundary-016] EventsSurface subscribeFuture 严禁回放已存在的 sticky 历史，仅接收后续事件', async () => {
  const EventsSurface = await import('../../../dist/OpenCode/Host/EventsSurface.js');
  const port = EventsSurface.create()
  // 1. 预先投递已终结事件建立 sticky 事实
  EventsSurface.notify(port, 'ses-prior', 'Completed', 'run-prior', 'done')

  // 2. 普通 subscribe 必须立即回放该历史
  const stickySeen = []
  const subSticky = EventsSurface.subscribe(port, (sessionId, outcome) => {
    stickySeen.push({ sessionId, kind: outcome.kind })
  })
  assert.equal(stickySeen.length, 1, '普通 subscribe 必须回放已有 sticky 历史')
  assert.equal(stickySeen[0].sessionId, 'ses-prior')

  // 3. subscribeFuture 严禁回放任何已有 sticky 历史
  const futureSeen = []
  const subFuture = EventsSurface.subscribeFuture(port, (sessionId, outcome) => {
    futureSeen.push({ sessionId, kind: outcome.kind })
  })
  assert.equal(futureSeen.length, 0, 'subscribeFuture 严禁回放已有 sticky 历史')

  // 4. 新事件到达时，两者均正常接收
  EventsSurface.notify(port, 'ses-prior', 'Failed', '', 'subsequent failure')
  assert.equal(stickySeen.length, 2)
  assert.equal(futureSeen.length, 1)
  assert.equal(futureSeen[0].sessionId, 'ses-prior')
  assert.equal(futureSeen[0].kind, 'Failed')

  EventsSurface.dispose(subSticky)
  EventsSurface.dispose(subFuture)
})
