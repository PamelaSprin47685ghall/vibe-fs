import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import test from 'node:test';
import { initSpikePlugin } from '../../../build/next/OpenCode/SpikePlugin.js';

test('manager permission denies global executor tool and executes mailbox path', async () => {
  const journalDirectory = fs.mkdtempSync(path.join(os.tmpdir(), 'wanxiangshu-manager-'));
  try {
    execFileSync('git', ['init', '--quiet', journalDirectory]);
    const hooks = await initSpikePlugin({
      client: {},
      directory: journalDirectory,
      events: { listen: () => () => {} },
    });
    const names = Object.keys(hooks.tool).sort();

    assert.deepEqual(names, ['coder', 'executor', 'fork', 'fork-manager', 'fork-pty', 'inspector', 'join', 'list', 'verdict']);
    assert.deepEqual(Object.keys(hooks.tool.fork.args).sort(), ['agent', 'prompt']);
    assert.deepEqual(Object.keys(hooks.tool['fork-pty'].args).sort(), ['agent', 'prompt', 'signal']);
    assert.deepEqual(Object.keys(hooks.tool['fork-manager'].args).sort(), ['prompt']);

    const config = {};
    hooks.config(config);
    assert.equal(config.agent.manager.permission['*'], 'deny');
    assert.equal(config.agent.manager.permission.executor, 'deny');
    assert.equal(config.agent.manager.permission['fork-pty'], 'deny');
    assert.equal(config.agent.manager.permission.inspector, 'deny');
    assert.equal(config.agent.manager.permission.coder, 'deny');
    assert.equal(config.agent.inspector.permission['*'], 'deny');
    assert.equal(config.agent.inspector.permission.executor, 'allow');
    assert.equal(config.agent.inspector.permission['fork-pty'], 'deny');
    assert.equal(config.agent.inspector.permission.fork, 'deny');
    assert.equal(config.agent.devops.permission['*'], 'deny');
    assert.equal(config.agent.devops.permission['fork-pty'], 'allow');
    assert.equal(config.agent.devops.permission.executor, 'allow');
    assert.equal(config.agent.devops.permission.join, 'allow');
    assert.equal(config.agent.devops.permission.list, 'allow');
    assert.equal(config.agent.devops.permission.read, 'allow');
    assert.equal(config.agent.devops.permission.glob, 'allow');
    assert.equal(config.agent.devops.permission.grep, 'allow');
    assert.equal(config.agent.devops.permission.inspector, 'allow');
    assert.equal(config.agent.devops.permission.coder, 'allow');
    assert.equal(config.agent.devops.permission.write, 'deny');
    assert.equal(config.agent.devops.permission.edit, 'deny');
    assert.equal(config.agent.devops.permission.fork, 'deny');
    assert.equal(config.agent.coder.permission.inspector, 'allow');
    assert.equal(config.agent.orchestrator.permission['*'], 'deny');
    assert.equal(config.agent.orchestrator.permission.fork, 'deny');
    assert.equal(config.agent.orchestrator.permission['fork-manager'], 'allow');
    assert.equal(config.agent.manager.permission.fork, 'allow');
    assert.equal(config.agent.manager.permission['fork-manager'], 'deny');
    assert.equal(config.agent.orchestrator.permission.join, 'allow');
    assert.equal(config.agent.orchestrator.permission.list, 'deny');

    const transformed = { messages: [{ role: 'user', text: 'hello' }] };
    hooks['chat.transform']({}, transformed);
    const markerRe = /\[(CAPS|REVIEW|HINT):/;
    const allText = transformed.messages.flatMap((m) => [
      m.text ?? '',
      ...(m.parts ?? []).map((p) => p.text ?? ''),
    ]);
    assert.ok(transformed.messages.some((m) => m.text === 'hello'), 'original user message preserved');
    assert.ok(!allText.some((t) => markerRe.test(t)), 'no synthetic [CAPS]/[REVIEW]/[HINT] marker injected');

    const context = { sessionID: 'manager-contract' };
    const fork = JSON.parse(await hooks.tool.fork.execute({ agent: 'coder', prompt: 'work' }, context));
    const join = JSON.parse(await hooks.tool.join.execute({}, context));
    const list = JSON.parse(await hooks.tool.list.execute({}, context));

    assert.match(fork.agentId, /^[a-z0-9]{6}$/);
    assert.equal(join.kind, 'agent');
    assert.equal(join.status, 'completed');
    assert.equal(join.agentId, fork.agentId);
    assert.ok(typeof join.finalText === 'string' && join.finalText.length > 0, 'join finalText must be non-empty');
    assert.ok(!Array.isArray(join.outcome), 'join must not emit F# Result arrays');
    assert.equal(list[0].kind, 'agent');
    assert.equal(list[0].agentId, fork.agentId);
    assert.equal(list[0].role, 'coder');
    assert.equal(typeof list[0].hasPendingCompletion, 'boolean');
    assert.ok('currentRunId' in list[0], 'list must expose currentRunId');
    assert.ok('lastCompletionStatus' in list[0], 'list must expose lastCompletionStatus');
    const commonDirectory = execFileSync('git', ['-C', journalDirectory, 'rev-parse', '--git-common-dir'], { encoding: 'utf8' }).trim();
    const gitDirectory = path.isAbsolute(commonDirectory) ? commonDirectory : path.resolve(journalDirectory, commonDirectory);
    const runtimeDirectory = path.join(gitDirectory, 'wanxiangshu-next', 'runtimes');
    assert.equal(fs.readdirSync(runtimeDirectory).filter((name) => name.endsWith('.ndjson')).length, 1);
    assert.ok(!fs.existsSync(path.join(journalDirectory, '.wanxiangshu-next')), 'Journal must not dirty the workspace');
  } finally {
    fs.rmSync(journalDirectory, { recursive: true, force: true });
  }
});
