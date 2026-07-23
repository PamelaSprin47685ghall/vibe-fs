/**
 * p0-canary-tests-child-pty.js — Child session parentID/cleanup and PTY E2E tests.
 * Kept under the 300-line Kolmogorov line budget.
 */

import { getSessionId, runScenario } from '../../../testkit/opencode/scenario.js';
import { isPidAlive } from '../../../testkit/opencode/process-host-checks.js';
import {
  findPtySessionId,
  findToolPart,
  parsePtySpawnOutput,
  parsePtyListOutput,
  parsePtyKillOutput,
  sleep,
  TIMEOUTS,
} from './p0-canary-utils.js';
import { pathToFileURL } from 'node:url';

function expectNoSessionError(t, sid) {
  t.events.expectCount({ type: 'session.error', sessionID: sid, count: 0 });
}

async function ensurePluginLoaded(t) {
  const cmds = await t.client.request('GET', '/command');
  if (!cmds.ok) throw new Error(`GET /command failed: ${cmds.status}`);
  const names = (cmds.data || []).map((c) => c.name);
  if (!names.includes('loop')) {
    throw new Error('DIAGNOSTIC: Plugin failed to load. Custom command "loop" is not registered.');
  }
}

async function awaitTerminal(t, turn, sid, timeoutMs = TIMEOUTS.prompt) {
  await t.events.assertNever(
    (e) => e.type === 'session.error' && e.sessionID === sid && e.error?.name !== 'MessageAbortedError',
    100,
  );
  await turn.awaitTerminal({
    timeoutMs,
    requireActivity: true,
    requireAssistantTerminal: false,
    requireIdleAfterActivity: true,
  });
}

function findChildCreatedEvent(t, sid) {
  const ev = t.events.allEvents.find(
    (e) => e.type === 'session.created' && e.sessionID && e.sessionID !== sid,
  );
  if (!ev) throw new Error('child session.created event not found');
  return ev;
}

function assertChildParentID(ev, sid) {
  const parentID = ev.properties?.info?.parentID;
  if (parentID !== sid) {
    throw new Error(`Expected child parentID to be ${sid}, got ${parentID}`);
  }
}

async function fetchChildMessages(t, childSid) {
  const res = await t.client.messages(childSid);
  if (!res.ok) throw new Error(`GET child messages failed: ${res.status}`);
  return res.data || [];
}

function assertPtySpawnPart(childMessages) {
  const spawnPart = findToolPart(childMessages, 'pty_spawn');
  if (!spawnPart) throw new Error('child pty_spawn tool part missing');
  const spawnInfo = parsePtySpawnOutput(spawnPart.state?.output || '');
  if (!spawnInfo.id || !spawnInfo.id.startsWith('pty_')) {
    throw new Error(`invalid pty id: ${spawnInfo.id}`);
  }
  if (!spawnInfo.pid || Number.isNaN(spawnInfo.pid)) {
    throw new Error(`invalid pid: ${spawnPart.state?.output}`);
  }
  if (spawnInfo.status !== 'running') {
    throw new Error(`spawn status not running: ${spawnInfo.status}`);
  }
  return spawnInfo;
}

function assertPtyWritePart(childMessages) {
  const writePart = findToolPart(childMessages, 'pty_write');
  if (!writePart) throw new Error('child pty_write tool part missing');
  if (writePart.state?.status !== 'completed') {
    throw new Error(`pty_write status: ${writePart.state?.status}`);
  }
}

function assertPtyReadPart(childMessages, expectedText) {
  const readPart = findToolPart(childMessages, 'pty_read');
  if (!readPart) throw new Error('child pty_read tool part missing');
  if (readPart.state?.status !== 'completed') {
    throw new Error(`pty_read status: ${readPart.state?.status}`);
  }
  if (!readPart.state?.output?.includes(expectedText)) {
    throw new Error(`pty_read output missing ${expectedText}: ${readPart.state?.output}`);
  }
}

function assertPtyListPart(childMessages, expectedId, expectedPid, expectedCommand) {
  const listPart = findToolPart(childMessages, 'pty_list');
  if (!listPart) throw new Error('child pty_list tool part missing');
  const listed = parsePtyListOutput(listPart.state?.output || '', expectedId);
  if (!listed) throw new Error(`pty ${expectedId} not visible in list`);
  if (listed.status !== 'running') throw new Error(`list status: ${listed.status}`);
  if (listed.pid !== expectedPid) {
    throw new Error(`list pid mismatch: ${listed.pid} vs ${expectedPid}`);
  }
  if (expectedCommand && !listed.command?.trim().startsWith(expectedCommand)) {
    throw new Error(`list command mismatch: ${listed.command} vs ${expectedCommand}`);
  }
}

function assertPtyKillPart(childMessages, expectedId) {
  const killPart = findToolPart(childMessages, 'pty_kill');
  if (!killPart) throw new Error('child pty_kill tool part missing');
  const killInfo = parsePtyKillOutput(killPart.state?.output || '');
  if (killInfo.action !== 'killed') throw new Error(`kill action: ${killInfo.action}`);
  if (!killInfo.cleanup) throw new Error('kill cleanup not true');
  if (killInfo.id !== expectedId) {
    throw new Error(`kill id mismatch: ${killInfo.id} vs ${expectedId}`);
  }
}

async function waitUntilPidDead(pid, timeoutMs = 5000) {
  const deadline = Date.now() + timeoutMs;
  while (isPidAlive(pid)) {
    if (Date.now() > deadline) {
      throw new Error(`PID ${pid} still alive after ${timeoutMs}ms`);
    }
    await sleep(100);
  }
}

async function waitUntilSessionGone(client, sid, timeoutMs = 5000) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    const res = await client.sessionStatus(sid);
    if (res.status === 404) return;
    await sleep(100);
  }
  const last = await client.sessionStatus(sid);
  if (last.status !== 404) {
    throw new Error(`Expected session ${sid} to be gone (404), got ${last.status}`);
  }
}

const tests = [
  {
    name: 'OC-SUB-002 OC-SUB-006 child session parentID and PTY list/read/write/kill E2E',
    fn: async (t) => {
      await ensurePluginLoaded(t);
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);

      // Parent calls inspector to spawn a child that will drive a PTY.
      t.provider.expectToolCall({
        id: 'parent-inspector',
        tool: 'inspector',
        args: {
          intents: [{
            objective: 'child pty test',
            background: 'verify child session parentID and PTY tools',
            questions: ['run pty_spawn, pty_write, pty_read, pty_list, pty_kill'],
          }],
        },
      });

      // Child calls the full PTY lifecycle.
      t.provider.expectToolCall({
        id: 'child-pty-spawn',
        tool: 'pty_spawn',
        args: { command: 'cat', args: [], description: 'child pty cat echo' },
      });
      t.provider.expectToolCall({
        id: 'child-pty-write',
        tool: 'pty_write',
        args: (body) => ({ id: findPtySessionId(body), data: 'hello-child-pty\n' }),
      });
      t.provider.expectToolCall({
        id: 'child-pty-read',
        tool: 'pty_read',
        args: (body) => ({ id: findPtySessionId(body), offset: 0, limit: 10 }),
      });
      t.provider.expectToolCall({
        id: 'child-pty-list',
        tool: 'pty_list',
        args: {},
      });
      t.provider.expectToolCall({
        id: 'child-pty-kill',
        tool: 'pty_kill',
        args: (body) => ({ id: findPtySessionId(body), cleanup: true }),
      });
      t.provider.expectText({ id: 'child-final', text: 'child-pty-done' });
      t.provider.expectText({ id: 'parent-final', text: 'parent-pty-done' });

      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'run inspector to test child PTY');
      await awaitTerminal(t, turn, sid);

      const childEv = findChildCreatedEvent(t, sid);
      assertChildParentID(childEv, sid);
      const childSid = childEv.sessionID;

      const childMessages = await fetchChildMessages(t, childSid);
      const spawnInfo = assertPtySpawnPart(childMessages);
      assertPtyWritePart(childMessages);
      assertPtyReadPart(childMessages, 'hello-child-pty');
      assertPtyListPart(childMessages, spawnInfo.id, spawnInfo.pid, 'cat');
      assertPtyKillPart(childMessages, spawnInfo.id);

      // Verify the process was actually killed.
      if (isPidAlive(spawnInfo.pid)) {
        throw new Error(`child pty pid ${spawnInfo.pid} still alive after pty_kill`);
      }

      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-PTY-011 child PTY and session cleaned up on parent delete E2E',
    fn: async (t) => {
      await ensurePluginLoaded(t);
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);

      // Parent calls inspector; child spawns a long-running PTY and completes.
      t.provider.expectToolCall({
        id: 'parent-inspector',
        tool: 'inspector',
        args: {
          intents: [{
            objective: 'child pty cleanup test',
            background: 'verify deleting parent cleans child PTY and session',
            questions: ['spawn a long sleep in a PTY'],
          }],
        },
      });
      t.provider.expectToolCall({
        id: 'child-pty-spawn',
        tool: 'pty_spawn',
        args: { command: 'sleep', args: ['30'], description: 'child pty long sleep' },
      });
      t.provider.expectText({ id: 'child-final', text: 'child-cleanup-done' });
      t.provider.expectText({ id: 'parent-final', text: 'parent-cleanup-done' });

      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'run inspector to spawn a long PTY process');
      await awaitTerminal(t, turn, sid);

      const childEv = findChildCreatedEvent(t, sid);
      assertChildParentID(childEv, sid);
      const childSid = childEv.sessionID;

      const childMessages = await fetchChildMessages(t, childSid);
      const spawnInfo = assertPtySpawnPart(childMessages);

      if (!isPidAlive(spawnInfo.pid)) {
        throw new Error(`expected child pty pid ${spawnInfo.pid} to be alive before parent delete`);
      }

      // Deleting the parent must propagate cleanup to the child and its PTY.
      const delRes = await t.client.request('DELETE', '/session/' + sid);
      if (!delRes.ok) throw new Error(`DELETE parent session failed: ${delRes.status}`);

      await waitUntilPidDead(spawnInfo.pid);
      await waitUntilSessionGone(t.client, childSid);

      expectNoSessionError(t, sid);
    },
  },
];

export default tests;

// Standalone runner so this spec can be executed directly without modifying p0-canary.js.
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const code = await runScenario(
    { plugin: true, timeoutMs: 90000, contextLimit: 20000, allowSynthetic: true, allowTitleGen: true },
    tests,
  );
  process.exit(code);
}
