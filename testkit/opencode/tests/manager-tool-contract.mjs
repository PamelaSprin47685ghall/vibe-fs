import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import test from 'node:test';
import { SpikePlugin_initSpikePlugin } from '../../../build/next/OpenCode/SpikePlugin.js';

test('manager permission denies global executor tool and executes mailbox path', async () => {
  const journalDirectory = fs.mkdtempSync(path.join(os.tmpdir(), 'wanxiangshu-manager-'));
  try {
    execFileSync('git', ['init', '--quiet', journalDirectory]);
    const hooks = await SpikePlugin_initSpikePlugin({ client: {}, directory: journalDirectory });
    const names = Object.keys(hooks.tool).sort();

    assert.deepEqual(names, ['executor', 'fork', 'inspector', 'join', 'list', 'verdict']);
    assert.deepEqual(Object.keys(hooks.tool.fork.args).sort(), ['agent', 'prompt', 'signal']);

    const config = {};
    hooks.config(config);
    assert.equal(config.agent.manager.permission['*'], 'deny');
    assert.equal(config.agent.manager.permission.executor, undefined);
    assert.equal(config.agent.manager.permission.inspector, undefined);
    assert.equal(config.agent.inspector.permission['*'], 'deny');
    assert.equal(config.agent.inspector.permission.executor, 'allow');
    assert.equal(config.agent.inspector.permission.fork, undefined);
    assert.equal(config.agent.coder.permission.inspector, 'allow');
    assert.equal(config.agent.orchestrator.permission['*'], 'deny');
    assert.equal(config.agent.orchestrator.permission.fork, 'allow');
    assert.equal(config.agent.orchestrator.permission.join, 'allow');
    assert.equal(config.agent.orchestrator.permission.list, undefined);

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
    assert.equal(join.agentId, fork.agentId);
    assert.equal(join.outcome[0], 'Ok');
    assert.equal(list[0].agentId, fork.agentId);
    assert.equal(list[0].role, 'Coder');
    const commonDirectory = execFileSync('git', ['-C', journalDirectory, 'rev-parse', '--git-common-dir'], { encoding: 'utf8' }).trim();
    const gitDirectory = path.isAbsolute(commonDirectory) ? commonDirectory : path.resolve(journalDirectory, commonDirectory);
    const runtimeDirectory = path.join(gitDirectory, 'wanxiangshu-next', 'runtimes');
    assert.equal(fs.readdirSync(runtimeDirectory).filter((name) => name.endsWith('.ndjson')).length, 1);
    assert.ok(!fs.existsSync(path.join(journalDirectory, '.wanxiangshu-next')), 'Journal must not dirty the workspace');
  } finally {
    fs.rmSync(journalDirectory, { recursive: true, force: true });
  }
});
