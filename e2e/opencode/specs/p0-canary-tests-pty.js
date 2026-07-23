/**
 * p0-canary-tests-pty.js — PTY spawn + PTY kill tests.
 * Kept under the 300-line Kolmogorov line budget.
 */

import { getSessionId } from '../../../testkit/opencode/scenario.js';
import { isPidAlive } from '../../../testkit/opencode/process-host-checks.js';
import { findPtySessionId, parsePtySpawnOutput, parsePtyListOutput, parsePtyKillOutput, findToolPart, extractToolNames, TIMEOUTS } from './p0-canary-utils.js';
import { sleep } from './p0-canary-utils.js';

function expectNoSessionError(t, sid) {
  t.events.expectCount({ type: 'session.error', sessionID: sid, count: 0 });
}

const tests = [

  {
    name: 'OC-PTY-001 pty_spawn starts process and returns session ID',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({ id: 'pty-spawn', tool: 'pty_spawn', args: { command: 'sleep', args: ['10'], description: 'test pty spawn' } });
      t.provider.expectToolCall({ id: 'pty-list', tool: 'pty_list', args: {} });
      t.provider.expectToolCall({ id: 'pty-read', tool: 'pty_read', args: (reqBody) => ({ id: findPtySessionId(reqBody), offset: 0, limit: 50 }) });
      t.provider.expectText({ id: 'pty-done', text: 'spawned' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'run "sleep 10" in a PTY, list it, and read its buffer');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const messages = (await t.client.messages(sid)).data || [];
      const spawnPart = messages.map((m) => m.parts || []).flat().find((p) => p.type === 'tool' && p.tool === 'pty_spawn');
      if (!spawnPart) throw new Error('pty_spawn tool part not found');
      const { id, pid, status } = parsePtySpawnOutput(spawnPart.state?.output || '');
      if (!id || !id.startsWith('pty_')) throw new Error(`pty_spawn did not return a session id: ${id}`);
      if (!pid || Number.isNaN(pid)) throw new Error(`pty_spawn did not return a pid: ${spawnPart.state?.output}`);
      if (status !== 'running') throw new Error(`pty_spawn status not running: ${status}`);
      if (!isPidAlive(pid)) throw new Error(`pty_spawn pid ${pid} is not alive`);

      const listPart = messages.map((m) => m.parts || []).flat().find((p) => p.type === 'tool' && p.tool === 'pty_list');
      if (!listPart) throw new Error('pty_list tool part not found');
      const listed = parsePtyListOutput(listPart.state?.output || '', id);
      if (!listed) throw new Error(`pty ${id} not visible in pty_list output`);
      if (listed.status !== 'running') throw new Error(`pty_list status for ${id}: ${listed.status}`);
      if (listed.pid !== pid) throw new Error(`pty_list pid mismatch: ${listed.pid} vs ${pid}`);

      const readPart = messages.map((m) => m.parts || []).flat().find((p) => p.type === 'tool' && p.tool === 'pty_read');
      if (!readPart) throw new Error('pty_read tool part not found');
      if (readPart.state?.status !== 'completed') throw new Error(`pty_read state: ${readPart.state?.status}`);
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-PTY-005 pty_kill terminates process',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({ id: 'pty-spawn2', tool: 'pty_spawn', args: { command: 'sleep', args: ['30'], description: 'test pty kill' } });
      t.provider.expectToolCall({ id: 'pty-kill', tool: 'pty_kill', args: (reqBody) => ({ id: findPtySessionId(reqBody), cleanup: true }) });
      t.provider.expectToolCall({ id: 'pty-list2', tool: 'pty_list', args: {} });
      t.provider.expectText({ id: 'kill-done', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'start a PTY with "sleep 30", then kill it with cleanup and list');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const messages = (await t.client.messages(sid)).data || [];
      const allParts = messages.map((m) => m.parts || []).flat();
      const spawnPart = allParts.find((p) => p.type === 'tool' && p.tool === 'pty_spawn');
      if (!spawnPart) throw new Error('pty_spawn tool part not found');
      const { id, pid } = parsePtySpawnOutput(spawnPart.state?.output || '');
      if (!id || !pid) throw new Error(`pty_spawn did not return id/pid: ${spawnPart.state?.output}`);

      const killPart = allParts.find((p) => p.type === 'tool' && p.tool === 'pty_kill');
      if (!killPart) throw new Error('pty_kill tool part not found');
      const killInfo = parsePtyKillOutput(killPart.state?.output || '');
      if (killInfo.action !== 'killed') throw new Error(`pty_kill action: ${killInfo.action}`);
      if (!killInfo.cleanup) throw new Error('pty_kill did not cleanup');
      if (killInfo.id !== id) throw new Error(`pty_kill id mismatch: ${killInfo.id} vs ${id}`);
      if (isPidAlive(pid)) throw new Error(`pty pid ${pid} still alive after kill`);

      const listPart = allParts.find((p) => p.type === 'tool' && p.tool === 'pty_list');
      if (!listPart) throw new Error('post-kill pty_list tool part not found');
      const listed = parsePtyListOutput(listPart.state?.output || '', id);
      if (listed && listed.status === 'running') throw new Error(`pty ${id} still running after kill`);
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-PTY-002 pty_list shows newly created PTY',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({ id: 'pty-spawn', tool: 'pty_spawn', args: { command: 'sleep', args: ['5'], description: 'test pty list' } });
      t.provider.expectToolCall({ id: 'pty-list', tool: 'pty_list', args: {} });
      t.provider.expectText({ id: 'pty-done', text: 'spawned' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'spawn sleep 5, then list it');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const messages = (await t.client.messages(sid)).data || [];
      const spawnPart = findToolPart(messages, 'pty_spawn');
      const { id } = parsePtySpawnOutput(spawnPart.state?.output || '');

      const listPart = findToolPart(messages, 'pty_list');
      const listed = parsePtyListOutput(listPart.state?.output || '', id);
      if (!listed) throw new Error(`pty ${id} not visible in list`);
      if (listed.status !== 'running') throw new Error(`status: ${listed.status}`);
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-PTY-003 pty_read reads initial output',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({ id: 'pty-spawn', tool: 'pty_spawn', args: { command: 'echo', args: ['hello-pty-read-output'], description: 'test pty read' } });
      t.provider.expectToolCall({ id: 'pty-read', tool: 'pty_read', args: (reqBody) => ({ id: findPtySessionId(reqBody), offset: 0, limit: 50 }) });
      t.provider.expectText({ id: 'pty-done', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'spawn echo hello-pty-read-output, then read it');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const messages = (await t.client.messages(sid)).data || [];
      const readPart = findToolPart(messages, 'pty_read');
      if (readPart.state?.status !== 'completed') throw new Error(`read status: ${readPart.state?.status}`);
      if (!readPart.state?.output?.includes('hello-pty-read-output')) {
        throw new Error(`expected output not found in read: ${readPart.state?.output}`);
      }
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-PTY-004 pty_write writes stdin, reads response',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({ id: 'pty-spawn', tool: 'pty_spawn', args: { command: 'cat', args: [], description: 'test pty write' } });
      t.provider.expectToolCall({ id: 'pty-write', tool: 'pty_write', args: (reqBody) => ({ id: findPtySessionId(reqBody), data: 'hello-from-stdin\n' }) });
      t.provider.expectToolCall({ id: 'pty-read', tool: 'pty_read', args: (reqBody) => ({ id: findPtySessionId(reqBody), offset: 0, limit: 50 }) });
      t.provider.expectText({ id: 'pty-done', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'spawn cat, write hello-from-stdin, then read it');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const messages = (await t.client.messages(sid)).data || [];
      const writePart = findToolPart(messages, 'pty_write');
      if (writePart.state?.status !== 'completed') throw new Error(`write status: ${writePart.state?.status}`);

      const readPart = findToolPart(messages, 'pty_read');
      if (readPart.state?.status !== 'completed') throw new Error(`read status: ${readPart.state?.status}`);
      if (!readPart.state?.output?.includes('hello-from-stdin')) {
        throw new Error(`expected stdin echo not found in read: ${readPart.state?.output}`);
      }
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-PTY-006 After kill, list no longer reports running',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({ id: 'pty-spawn', tool: 'pty_spawn', args: { command: 'sleep', args: ['5'], description: 'test pty post kill' } });
      t.provider.expectToolCall({ id: 'pty-kill', tool: 'pty_kill', args: (reqBody) => ({ id: findPtySessionId(reqBody), cleanup: false }) });
      t.provider.expectToolCall({ id: 'pty-list', tool: 'pty_list', args: {} });
      t.provider.expectText({ id: 'pty-done', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'spawn sleep 5, kill it without cleanup, then list it');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const messages = (await t.client.messages(sid)).data || [];
      const spawnPart = findToolPart(messages, 'pty_spawn');
      const { id } = parsePtySpawnOutput(spawnPart.state?.output || '');

      const listPart = findToolPart(messages, 'pty_list');
      const listed = parsePtyListOutput(listPart.state?.output || '', id);
      if (listed && listed.status === 'running') {
        throw new Error(`killed pty ${id} still reported as running in list`);
      }
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-PTY-007 Invalid PTY ID returns error, others unaffected',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({ id: 'pty-spawn', tool: 'pty_spawn', args: { command: 'sleep', args: ['5'], description: 'test invalid id' } });
      t.provider.expectToolCall({ id: 'pty-read-invalid', tool: 'pty_read', args: { id: 'pty_invalid' } });
      t.provider.expectToolCall({ id: 'pty-list', tool: 'pty_list', args: {} });
      t.provider.expectText({ id: 'pty-done', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'spawn sleep 5, read an invalid pty ID, then list active ones');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const messages = (await t.client.messages(sid)).data || [];
      const spawnPart = findToolPart(messages, 'pty_spawn');
      const { id } = parsePtySpawnOutput(spawnPart.state?.output || '');

      const readPart = findToolPart(messages, 'pty_read');
      if (readPart.state?.status !== 'error') {
        throw new Error(`expected pty_read to fail, got status: ${readPart.state?.status}`);
      }
      if (!readPart.state?.error?.includes('PTY session not found')) {
        throw new Error(`expected not found error message, got: ${readPart.state?.error}`);
      }

      const listPart = findToolPart(messages, 'pty_list');
      const listed = parsePtyListOutput(listPart.state?.output || '', id);
      if (!listed || listed.status !== 'running') {
        throw new Error(`valid PTY ${id} affected or not running: ${JSON.stringify(listed)}`);
      }
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-PTY-008 Session deleted cleans up its PTYs',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({ id: 'pty-spawn', tool: 'pty_spawn', args: { command: 'sleep', args: ['5'], description: 'test session deleted' } });
      t.provider.expectText({ id: 'pty-done', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'spawn sleep 5 and say done');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const messages = (await t.client.messages(sid)).data || [];
      const spawnPart = findToolPart(messages, 'pty_spawn');
      const { pid } = parsePtySpawnOutput(spawnPart.state?.output || '');
      if (!isPidAlive(pid)) throw new Error('spawned process not alive');

      // Delete the session.
      const delRes = await t.client.request('DELETE', '/session/' + sid);
      if (!delRes.ok) throw new Error(`delete session failed: ${delRes.status}`);

      // Ensure the process is cleaned up (SIGKILLed/SIGTERMed).
      const deadline = Date.now() + 5000;
      while (isPidAlive(pid)) {
        if (Date.now() > deadline) throw new Error(`spawned process ${pid} still alive after session delete`);
        await sleep(100);
      }
    },
  },

  {
    name: 'OC-PTY-009 Different sessions PTYs isolated',
    fn: async (t) => {
      const sessA = await t.client.createSession();
      const sidA = getSessionId(sessA);
      t.provider.expectToolCall({ id: 'pty-spawnA', tool: 'pty_spawn', args: { command: 'sleep', args: ['5'], description: 'sessionA pty' } });
      t.provider.expectText({ id: 'pty-doneA', text: 'done' });
      const turnA = await t.turn.start(sidA);
      await t.client.prompt(sidA, 'spawn sleep 5 in session A');
      await turnA.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const messagesA = (await t.client.messages(sidA)).data || [];
      const spawnPartA = findToolPart(messagesA, 'pty_spawn');
      const { id } = parsePtySpawnOutput(spawnPartA.state?.output || '');

      const sessB = await t.client.createSession();
      const sidB = getSessionId(sessB);
      t.provider.expectToolCall({ id: 'pty-listB', tool: 'pty_list', args: {} });
      t.provider.expectToolCall({ id: 'pty-readB', tool: 'pty_read', args: { id } });
      t.provider.expectToolCall({ id: 'pty-writeB', tool: 'pty_write', args: { id, data: 'echo\n' } });
      t.provider.expectToolCall({ id: 'pty-killB', tool: 'pty_kill', args: { id } });
      t.provider.expectText({ id: 'pty-doneB', text: 'done' });
      const turnB = await t.turn.start(sidB);
      await t.client.prompt(sidB, 'list PTYs, read, write, and kill the PTY from session A');
      await turnB.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const messagesB = (await t.client.messages(sidB)).data || [];

      const listPart = findToolPart(messagesB, 'pty_list');
      const listed = parsePtyListOutput(listPart.state?.output || '', id);
      if (listed) throw new Error('session B should not see session A\'s PTY');

      for (const tool of ['pty_read', 'pty_write', 'pty_kill']) {
        const part = findToolPart(messagesB, tool);
        if (part.state?.status !== 'error') {
          throw new Error(`expected session B ${tool} to fail, got: ${part.state?.status}`);
        }
        if (!part.state?.error?.includes('PTY session not found')) {
          throw new Error(`expected not found error, got: ${part.state?.error}`);
        }
      }
      expectNoSessionError(t, sidA);
      expectNoSessionError(t, sidB);
    },
  },

  {
    name: 'OC-PTY-010 Permission deny = no child process',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      
      const coderArgs = {
        intents: [{
          targets: [{
            file: 'hello.txt',
            guide: 'implement hello world',
          }],
          objective: 'write hello world',
          background: 'test subagent',
        }],
        tdd: 'green',
      };

      t.provider.expectToolCall({ id: 'coder-call', tool: 'coder', args: coderArgs });
      t.provider.expectText({ id: 'coder-child-text', text: 'child-done' });
      t.provider.expectText({ id: 'parent-final', text: 'parent-done' });

      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'run coder to spawn a PTY');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const childSid = t.events.allEvents.find((e) => e.type === 'session.created' && e.sessionID !== sid)?.sessionID;
      if (!childSid) throw new Error('child session not created');

      // Find the first LLM request for the child session.
      const childReq = t.provider.requests.find((r) => r.sessionId === childSid || (r.messages && JSON.stringify(r.messages).includes('hello.txt')));
      if (!childReq) throw new Error('child LLM request not found');
      
      const toolNames = extractToolNames(childReq.tools);
      for (const tool of ['pty_spawn', 'pty_write', 'pty_read', 'pty_list', 'pty_kill']) {
        if (toolNames.includes(tool)) {
          throw new Error(`forbidden tool ${tool} present in coder tool list: ${toolNames.join(', ')}`);
        }
      }
      expectNoSessionError(t, sid);
    },
  },

];

export default tests;
