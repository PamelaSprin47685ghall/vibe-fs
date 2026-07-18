/**
 * p0-canary-tests-pty.js — PTY spawn + PTY kill tests.
 * Kept under the 300-line Kolmogorov line budget.
 */

import { getSessionId } from '../harness/scenario.js';
import { isPidAlive } from '../harness/process-host-checks.js';
import { findPtySessionId, parsePtySpawnOutput, parsePtyListOutput, parsePtyKillOutput, TIMEOUTS } from './p0-canary-utils.js';

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

];

export default tests;
