import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { SpikePlugin_initSpikePlugin } from '../../../build/next/OpenCode/SpikePlugin.js';

test('manager exposes only fork join list and executes the mailbox path', async () => {
  const journalDirectory = fs.mkdtempSync(path.join(os.tmpdir(), 'wanxiangshu-manager-'));
  try {
    const hooks = await SpikePlugin_initSpikePlugin({ client: {}, journalDirectory });
    const names = Object.keys(hooks.tool).sort();

    assert.deepEqual(names, ['fork', 'join', 'list']);
    assert.deepEqual(Object.keys(hooks.tool.fork.args).sort(), ['agent', 'prompt']);

    const transformed = { messages: [{ role: 'user', text: 'hello' }] };
    hooks['chat.transform']({}, transformed);
    assert.equal(transformed.messages[0].role, 'system');
    assert.match(transformed.messages[0].text, /CAPS:/);

    const context = { sessionID: 'manager-contract' };
    const fork = JSON.parse(await hooks.tool.fork.execute({ agent: 'coder', prompt: 'work' }, context));
    const join = JSON.parse(await hooks.tool.join.execute({}, context));
    const list = JSON.parse(await hooks.tool.list.execute({}, context));

    assert.match(fork.agentId, /^[a-z0-9]{6}$/);
    assert.equal(join.agentId, fork.agentId);
    assert.equal(join.outcome[0], 'Ok');
    assert.equal(list[0].agentId, fork.agentId);
    assert.equal(list[0].role, 'Coder');
    assert.equal(fs.readdirSync(journalDirectory).filter((name) => name.endsWith('.ndjson')).length, 1);
  } finally {
    fs.rmSync(journalDirectory, { recursive: true, force: true });
  }
});
