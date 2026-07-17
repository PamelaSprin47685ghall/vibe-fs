/**
 * p0-canary.js — 15 P0 canary tests verifying each harness oracle.
 *
 * Run: node e2e/opencode/specs/p0-canary.js
 */

import fs from 'node:fs';
import { scenario, runScenarios } from '../harness/scenario.js';

function getSessionId(sess) {
  return sess.data?.data?.data?.id || sess.data?.data?.id;
}

async function waitForSessionIdle(client, probe, sessionID, timeoutMs = 60000) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    const status = await client.request('GET', '/session/status');
    if (status.ok) {
      const smap = status.data?.data || status.data;
      const s = smap?.[sessionID];
      if (s && s.type === 'idle') return true;
    }
    const idle = probe.bySession(sessionID).find(e => e.type === 'session.idle');
    if (idle) return true;
    await new Promise(r => setTimeout(r, 100));
  }
  return false;
}

const scenarios = [
  // ── OC-BOOT-001: Start opencode with plugin ────────────────────────────────
  {
    name: 'OC-BOOT-001 opencode serve starts with plugin loaded',
    opts: { plugin: true, timeoutMs: 90000 },
    fn: async (t) => {
      const cmds = await t.client.request('GET', '/command');
      if (!cmds.ok) throw new Error(`GET /command failed: ${cmds.status}`);
      const cmdStr = JSON.stringify(cmds.data);
      if (!cmdStr.includes('loop')) throw new Error('loop command not found');

      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      if (!sid) throw new Error(`No session ID`);

      t.provider.expectText({ id: 'warmup', text: 'ready' });
      await t.client.prompt(sid, 'confirm ready');
      const idle = await waitForSessionIdle(t.client, t.events, sid);
      if (!idle) throw new Error('Session did not reach idle');

      const s = await t.client.sessionStatus(sid);
      const tokens = s.data?.data?.tokens || s.data?.tokens || {};
      if ((tokens.input || 0) === 0) throw new Error(`No token usage`);

      t.events.expectCount({ type: 'session.error', sessionID: sid, count: 0 });
      t.provider.expectSatisfied();
    }
  },

  // ── OC-FILE-001: write creates file with exact bytes ───────────────────────
  {
    name: 'OC-FILE-001 write creates file with exact bytes',
    opts: { plugin: true, timeoutMs: 120000 },
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);

      t.provider.expectToolCall({
        id: 'write-file',
        tool: 'write',
        args: { filePath: 'hello.txt', content: 'Hello World\n' },
      });
      t.provider.expectText({ id: 'write-done', text: 'done' });

      await t.client.prompt(sid, 'Write hello.txt with content "Hello World\\n"');
      const idle = await waitForSessionIdle(t.client, t.events, sid);
      if (!idle) throw new Error('Session did not reach idle');

      // Then — Filesystem: file exists with correct content
      t.fs.expectFile('hello.txt');
      t.fs.expectFileContent('hello.txt', 'Hello World\n');

      // Then — Host: tool result in messages
      const msgs = await t.client.messages(sid);
      const msgsStr = JSON.stringify(msgs.data);
      if (!msgsStr.includes('Hello World')) throw new Error('Tool result not found');

      t.events.expectCount({ type: 'session.error', sessionID: sid, count: 0 });
      t.provider.expectSatisfied();
    }
  },

  // ── OC-FILE-006: read returns content into next round ──────────────────────
  {
    name: 'OC-FILE-006 read returns real file content',
    opts: { plugin: true, timeoutMs: 120000 },
    fn: async (t) => {
      const content = 'Read test content\nsecond line\n';
      fs.writeFileSync(`${t.host.workDir}/readme.txt`, content, 'utf8');

      const sess = await t.client.createSession();
      const sid = getSessionId(sess);

      t.provider.expectToolCall({
        id: 'read-file',
        tool: 'read',
        args: { filePath: 'readme.txt' },
      });
      t.provider.expectText({ id: 'read-done', text: 'file content read' });

      await t.client.prompt(sid, 'read readme.txt and summarize it');
      const idle = await waitForSessionIdle(t.client, t.events, sid);
      if (!idle) throw new Error('Session did not reach idle');

      const msgs = await t.client.messages(sid);
      const msgsStr = JSON.stringify(msgs.data);
      if (!msgsStr.includes('Read test content')) throw new Error('Read content not found');

      t.events.expectCount({ type: 'session.error', sessionID: sid, count: 0 });
      t.provider.expectSatisfied();
    }
  },

  // ── OC-EXEC-001: Shell command stdout ──────────────────────────────────────
  {
    name: 'OC-EXEC-001 shell command stdout correct',
    opts: { plugin: true, timeoutMs: 120000 },
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);

      t.provider.expectToolCall({
        id: 'exec-echo',
        tool: 'executor',
        args: { language: 'shell', command: 'echo hello-e2e' },
      });
      t.provider.expectText({ id: 'exec-done', text: 'done' });

      await t.client.prompt(sid, 'run "echo hello-e2e" in shell');
      const idle = await waitForSessionIdle(t.client, t.events, sid);
      if (!idle) throw new Error('Session did not reach idle');

      const msgs = await t.client.messages(sid);
      const msgsStr = JSON.stringify(msgs.data);
      if (!msgsStr.includes('hello-e2e')) throw new Error('Executor output not found');

      t.events.expectCount({ type: 'session.error', sessionID: sid, count: 0 });
      t.provider.expectSatisfied();
    }
  },

  // ── OC-FUZZY-003: fuzzy_grep returns file, line, text ──────────────────────
  {
    name: 'OC-FUZZY-003 fuzzy_grep returns file, line, text',
    opts: { plugin: true, timeoutMs: 120000 },
    fn: async (t) => {
      // Setup: write a file with known content
      fs.writeFileSync(`${t.host.workDir}/grep_target.txt`, 'unique-pattern-xyz\nhello world\nunique-pattern-xyz again\n', 'utf8');

      const sess = await t.client.createSession();
      const sid = getSessionId(sess);

      t.provider.expectToolCall({
        id: 'fuzzy-grep',
        tool: 'fuzzy_grep',
        args: { pattern: ['unique-pattern-xyz'] },
      });
      t.provider.expectText({ id: 'grep-done', text: 'found matches' });

      await t.client.prompt(sid, 'search for unique-pattern-xyz');
      const idle = await waitForSessionIdle(t.client, t.events, sid);
      if (!idle) throw new Error('Session did not reach idle');

      const msgs = await t.client.messages(sid);
      const msgsStr = JSON.stringify(msgs.data);
      if (!msgsStr.includes('unique-pattern-xyz')) throw new Error('Grep results not found');

      t.events.expectCount({ type: 'session.error', sessionID: sid, count: 0 });
      t.provider.expectSatisfied();
    }
  },

  // ── OC-REV-001: /loop task activates review ────────────────────────────────
  {
    name: 'OC-REV-001 /loop task returns With-Review activated',
    opts: { plugin: true, timeoutMs: 120000 },
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);

      const loopRes = await t.client.runCommand(sid, 'loop', 'implement feature X');
      if (!loopRes.ok) throw new Error(`/loop failed: ${loopRes.status}`);
      const loopStr = JSON.stringify(loopRes.data);
      if (!loopStr.includes('With-Review')) {
        // May also appear as NDJSON event
        const ndjsonPath = `${t.host.workDir}/.wanxiangshu.ndjson`;
        if (fs.existsSync(ndjsonPath)) {
          const ndjson = fs.readFileSync(ndjsonPath, 'utf8');
          if (!ndjson.includes('loop_activated')) throw new Error('loop_activated not in NDJSON');
        } else {
          throw new Error('No With-Review in response and no NDJSON found');
        }
      }

      t.events.expectCount({ type: 'session.error', sessionID: sid, count: 0 });
    }
  },
];

const exitCode = await runScenarios(scenarios);
process.exit(exitCode);
