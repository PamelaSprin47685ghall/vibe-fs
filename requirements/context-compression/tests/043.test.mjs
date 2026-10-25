import test from 'node:test'
import assert from 'node:assert/strict'
import { outboundViewStore, OutboundViewStore } from 'wanxiangli/pair-hint'

test('WHAT[CONTEXT-COMPRESSION-043] OutboundViewStore: progressive checkAndSeal seals only on full non-empty triad (toolsCaptured=false)', () => {
  const store = new OutboundViewStore()
  const sid = 'sess-triad-043'

  store.recordSystem(sid, ['You are an assistant with strict boundary.'])
  store.recordModel(sid, { providerID: 'anthropic', modelID: 'claude-3-5-sonnet', variant: null })
  assert.equal(store.tryGetSealed(sid), undefined, 'must not seal without messages')

  store.recordMessagesFor(sid, [{ info: { role: 'user', sessionID: sid }, content: 'execute task' }])
  const sealed = store.tryGetSealed(sid)
  assert.ok(sealed, 'sealed snapshot must exist once messages, system, and model are non-empty')
  assert.equal(sealed.sessionId, sid)
  assert.deepEqual(sealed.system, ['You are an assistant with strict boundary.'])
  assert.equal(sealed.model.providerID, 'anthropic')
  assert.equal(sealed.model.modelID, 'claude-3-5-sonnet')
  assert.equal(sealed.toolsCaptured, false, 'progressive checkAndSeal must set toolsCaptured=false')
  assert.equal(sealed.nativePayload, null, 'progressive checkAndSeal has null nativePayload')
})

test('WHAT[CONTEXT-COMPRESSION-043] OutboundViewStore: recordNativeRequest seals atomically with toolsCaptured=true and deep clones payload', () => {
  const store = new OutboundViewStore()
  const sid = 'sess-native-043'

  const nativePayload = {
    messages: [{ role: 'user', content: 'run tool command' }],
    tools: [{ type: 'function', function: { name: 'bash' } }],
    system: ['System instructions.']
  }
  const model = { providerID: 'openai', modelID: 'gpt-4o', variant: 'chat' }

  const ok = store.recordNativeRequest(sid, nativePayload, model)
  assert.equal(ok, true, 'valid native request must return true')

  const sealed = store.tryGetSealed(sid)
  assert.ok(sealed)
  assert.equal(sealed.sessionId, sid)
  assert.equal(sealed.toolsCaptured, true, 'native request capture must set toolsCaptured=true')
  assert.ok(sealed.nativePayload, 'native payload must be preserved')
  assert.deepEqual(sealed.system, ['System instructions.'])
  assert.equal(sealed.model.providerID, 'openai')
  assert.equal(sealed.model.modelID, 'gpt-4o')
  assert.equal(sealed.model.variant, 'chat')

  // Deep clone verification: external mutation must not corrupt sealed snapshot
  nativePayload.messages[0].content = 'MUTATED'
  assert.equal(sealed.messages[0].content, 'run tool command', 'sealed messages must be immutable copy')
})

test('WHAT[CONTEXT-COMPRESSION-043] OutboundViewStore: serializeSnapshot strictly adheres to wire JSON specification', () => {
  const store = new OutboundViewStore()
  const sid = 'sess-spec-json-043'

  store.recordSystem(sid, ['System instruction for scope.'])
  store.recordModel(sid, { providerID: 'custom-provider', modelID: 'custom-model', variant: null })
  store.recordMessagesFor(sid, [{ info: { role: 'user', sessionID: sid }, content: 'question' }])

  const sealed = store.tryGetSealed(sid)
  assert.ok(sealed)

  const jsonStr = store.serializeSnapshot(sealed)
  const parsed = JSON.parse(jsonStr)

  // Verify exact JSON keys and shapes
  assert.equal(parsed.sessionId, sid)
  assert.deepEqual(parsed.system, ['System instruction for scope.'])
  assert.equal(parsed.model.providerID, 'custom-provider')
  assert.equal(parsed.model.modelID, 'custom-model')
  assert.equal(parsed.model.variant, null)
  assert.equal(parsed.toolsCaptured, false)
  assert.equal(parsed.nativePayload, null)
})

test('WHAT[CONTEXT-COMPRESSION-043] OutboundViewStore: discardSealed empties sealed snapshot (single-drain semantics)', () => {
  const store = new OutboundViewStore()
  const sid = 'sess-discard-043'

  store.recordSystem(sid, ['sys'])
  store.recordModel(sid, { providerID: 'p', modelID: 'm' })
  store.recordMessagesFor(sid, [{ info: { role: 'user', sessionID: sid }, content: 'q' }])
  assert.ok(store.tryGetSealed(sid))

  store.discardSealed(sid)
  assert.equal(store.tryGetSealed(sid), undefined, 'discardSealed must empty the sealed snapshot')
})

test('WHAT[CONTEXT-COMPRESSION-043] OutboundViewStore: clearSession purges all session state idempotently', () => {
  const store = new OutboundViewStore()
  const sid = 'sess-purge-043'

  store.recordSystem(sid, ['sys'])
  store.clearSession(sid)

  // Recording model and messages now won't seal because system was discarded
  store.recordModel(sid, { providerID: 'p', modelID: 'm' })
  store.recordMessagesFor(sid, [{ info: { role: 'user', sessionID: sid }, content: 'q' }])
  assert.equal(store.tryGetSealed(sid), undefined, 'prior pending system must have been purged')
})

test('WHAT[CONTEXT-COMPRESSION-043] OutboundViewStore: global singleton outboundViewStore is operational', () => {
  assert.ok(outboundViewStore instanceof OutboundViewStore)
  const sid = 'sess-singleton-043'
  outboundViewStore.recordSystem(sid, ['s'])
  outboundViewStore.clearSession(sid)
})


const dummyFactory = {
  fields: [
    {
      schema: {
        string: () => ({ optional: () => ({ describe: () => ({}) }), describe: () => ({}) }),
        number: () => ({ int: () => ({ nonnegative: () => ({ describe: () => ({ optional: () => ({}) }), optional: () => ({ describe: () => ({}) }) }) }) }),
        enum: () => ({ describe: () => ({}) })
      }
    }
  ]
};

test('WHAT[CONTEXT-COMPRESSION-043] ResetScopeTool 宿主真实表面接线反例：无封存快照时拒绝重置并返回明确错误文本', async () => {
    const { spec } = await import('../../../dist/OpenCode/Tools/ResetScopeTool.js');
    const { ToolHostCodec_argumentsOfRaw } = await import('../../../dist/OpenCode/Codec/ToolHostCodec.js');
    const { ToolCallIdModule_create } = await import('../../../dist/Foundation/Identity.js');
    const { HostToolContext } = await import('../../../dist/Host/ToolContext.js');
    const tool = spec(dummyFactory, undefined);
    const noSnapSid = 'no-snap-session-' + Math.random().toString(36).slice(2);
    const rawArgs = ToolHostCodec_argumentsOfRaw({ newScopeObjective: 'test-objective', newScopeTurnBudget: 5, kpiCompletion: 'met' });
    const ctx = new HostToolContext(
      noSnapSid,
      undefined,
      ToolCallIdModule_create('tc-no-snap'),
      undefined,
      undefined,
      () => {}
    );
    const res = await tool.Execute(rawArgs, ctx);
    assert.equal(typeof res, 'string');
    assert.ok(res.includes('resetScope failed: no sealed outbound view snapshot captured for this session.'));
  });

  test('WHAT[CONTEXT-COMPRESSION-043] ResetScopeTool 宿主真实表面接线正例：单例建立封存快照后可正常归档并排空', async () => {
    const { spec } = await import('../../../dist/OpenCode/Tools/ResetScopeTool.js');
    const { ToolHostCodec_argumentsOfRaw } = await import('../../../dist/OpenCode/Codec/ToolHostCodec.js');
    const { ToolCallIdModule_create, ProviderRunIdentityModule_create } = await import('../../../dist/Foundation/Identity.js');
    const { HostToolContext } = await import('../../../dist/Host/ToolContext.js');
    const snapSid = 'snap-session-' + Math.random().toString(36).slice(2);
    const nativePayload = {
      messages: [{ role: 'user', content: 'hello world' }],
      tools: [{ type: 'function', function: { name: 'bash' } }],
      system: ['System instructions.']
    };
    const model = { providerID: 'openai', modelID: 'gpt-4o', variant: null };
    const ok = outboundViewStore.recordNativeRequest(snapSid, nativePayload, model);
    assert.equal(ok, true);
    assert.ok(outboundViewStore.tryGetSealed(snapSid), '快照必须处于已封存状态');

    const os = await import('node:os');
    const path = await import('node:path');
    const fs = await import('node:fs');
    const workspaceHost = await import('../../../dist/OpenCode/Host/WorkspaceSharedJournal.js');
    const { JournalHandle__get_Journal, JournalSurface_dispose } = await import('../../../dist/Persistence/Journal/Surface.js');
    const tmpdir = fs.mkdtempSync(path.join(os.tmpdir(), 'wxs-reset-positive-'));
    const acquired = await workspaceHost.acquireSharedForWorkspace(tmpdir, process.pid, new Date().toISOString());
    if (!acquired?.ok) {
      throw new Error('journal acquire rejected: ' + (acquired?.error ?? 'unknown'));
    }
    const journal = JournalHandle__get_Journal(acquired.journal);

    const tool = spec(dummyFactory, journal);
    const rawArgs = ToolHostCodec_argumentsOfRaw({
      newScopeObjective: 'verified-objective',
      newScopeTurnBudget: 10,
      kpiCompletion: 'met'
    });
    const ctx = new HostToolContext(
      snapSid,
      undefined,
      ToolCallIdModule_create('tc-snap'),
      ProviderRunIdentityModule_create('provider-run-1'),
      undefined,
      () => {}
    );
    const res = await tool.Execute(rawArgs, ctx);
    assert.equal(typeof res, 'string');
    assert.ok(res.includes('Scope reset. Objective: verified-objective'));
    assert.equal(outboundViewStore.tryGetSealed(snapSid), undefined, '归档提交后必须单次排空该会话快照');

    JournalSurface_dispose(acquired.journal);
    fs.rmSync(tmpdir, { recursive: true, force: true });
  });

