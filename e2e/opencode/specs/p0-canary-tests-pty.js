/**
 * p0-canary-tests-pty.js — PTY spawn + PTY kill tests.
 * Kept under the 300-line Kolmogorov line budget.
 */

import fs from 'node:fs';
import { getSessionId } from '../harness/scenario.js';
import { findPtySessionId, TIMEOUTS } from './p0-canary-utils.js';

function expectNoSessionError(t, sid) {
  t.events.expectCount({ type: 'session.error', sessionID: sid, count: 0 });
}

const tests = [

  {
    name: 'OC-PTY-001 pty_spawn starts process and returns session ID',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({ id: 'pty-spawn', tool: 'pty_spawn', args: { command: 'echo', args: ['hello-pty'], description: 'test pty spawn' } });
      t.provider.expectText({ id: 'pty-done', text: 'spawned' });
      await t.client.prompt(sid, 'run "echo hello-pty" in a PTY session and confirm it started');
      await t.turn.start().awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
      const msgsStr = JSON.stringify((await t.client.messages(sid)).data);
      if (!msgsStr.includes('pty_')) throw new Error('PTY session ID not found in messages');
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-PTY-005 pty_kill terminates process',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({ id: 'pty-spawn2', tool: 'pty_spawn', args: { command: 'echo', args: ['hello-kill'], description: 'test pty kill' } });
      t.provider.expectToolCall({ id: 'pty-kill', tool: 'pty_kill', args: (reqBody) => ({ id: findPtySessionId(reqBody), cleanup: true }) });
      t.provider.expectText({ id: 'kill-done', text: 'done' });
      await t.client.prompt(sid, 'start a PTY with "echo hello-kill", then kill it with cleanup');
      await t.turn.start().awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
      const msgsStr = JSON.stringify((await t.client.messages(sid)).data);
      if (!msgsStr.includes('killed') && !msgsStr.includes('cleaned_up')) {
        throw new Error('PTY kill result not found');
      }
      expectNoSessionError(t, sid);
    },
  },

];

export default tests;
