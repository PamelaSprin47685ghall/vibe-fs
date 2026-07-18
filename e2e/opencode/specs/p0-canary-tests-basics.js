/**
 * p0-canary-tests-basics.js — File / fuzzy / executor / schema / web tests.
 * Kept under the 300-line Kolmogorov line budget.
 */

import { getSessionId } from '../harness/scenario.js';
import { content, writeWorkFile, extractToolNames, TIMEOUTS } from './p0-canary-utils.js';

function expectNoSessionError(t, sid) {
  t.events.expectCount({ type: 'session.error', sessionID: sid, count: 0 });
}

const tests = [
  {
    name: 'OC-BOOT-001 opencode serve starts with plugin loaded',
    fn: async (t) => {
      const cmds = await t.client.request('GET', '/command');
      if (!cmds.ok) throw new Error(`GET /command failed: ${cmds.status}`);
      if (!JSON.stringify(cmds.data).includes('loop')) throw new Error('loop command not found');
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      if (!sid) throw new Error('No session ID');
      t.provider.expectText({ id: 'warmup', text: 'ready' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'say ready');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
      const s = await t.client.sessionStatus(sid);
      const tokens = s.data?.data?.tokens || s.data?.tokens || {};
      if ((tokens.input || 0) === 0) throw new Error('No token usage');
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-FILE-001 write creates file with exact bytes',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({ id: 'write-file', tool: 'write', args: { filePath: 'hello.txt', content: content('Hello World') } });
      t.provider.expectText({ id: 'write-done', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'Write hello.txt with content "Hello World\\\\n"');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
      t.fs.expectFile('hello.txt');
      t.fs.expectFileContent('hello.txt', content('Hello World'));
      const msgsStr = JSON.stringify((await t.client.messages(sid)).data);
      if (!msgsStr.includes('Hello World')) throw new Error('Tool result not in messages');
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-FILE-006 read returns real file content',
    fn: async (t) => {
      writeWorkFile(t.host.workDir, 'readme.txt', content('Read test content\nsecond line'));
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({ id: 'read-file', tool: 'read', args: { filePath: 'readme.txt' } });
      t.provider.expectText({ id: 'read-done', text: 'file content read' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'read readme.txt and summarize it');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
      const msgsStr = JSON.stringify((await t.client.messages(sid)).data);
      if (!msgsStr.includes('Read test content')) throw new Error('Read content not found');
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-EXEC-001 shell command stdout correct',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({ id: 'exec-echo', tool: 'executor', args: { language: 'shell', command: 'echo hello-e2e' } });
      t.provider.expectText({ id: 'exec-done', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'run "echo hello-e2e" in shell');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
      const msgsStr = JSON.stringify((await t.client.messages(sid)).data);
      if (!msgsStr.includes('hello-e2e')) throw new Error('Executor output not found');
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-FUZZY-003 fuzzy_grep returns file, line, text',
    fn: async (t) => {
      writeWorkFile(t.host.workDir, 'grep_target.txt', content('unique-pattern-xyz\nhello world\nunique-pattern-xyz again'));
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({ id: 'fuzzy-grep', tool: 'fuzzy_grep', args: { pattern: ['unique-pattern-xyz'] } });
      t.provider.expectText({ id: 'grep-done', text: 'found matches' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'search for unique-pattern-xyz');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
      const msgsStr = JSON.stringify((await t.client.messages(sid)).data);
      if (!msgsStr.includes('unique-pattern-xyz')) throw new Error('Grep results not found');
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-FILE-002 write overwrites file with no trailing residue',
    fn: async (t) => {
      writeWorkFile(t.host.workDir, 'overwrite.txt', content('old content to overwrite'));
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({ id: 'ow-file', tool: 'write', args: { filePath: 'overwrite.txt', content: content('new content only') } });
      t.provider.expectText({ id: 'ow-done', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'Write overwrite.txt with content "new content only\\\\n"');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
      t.fs.expectFileContent('overwrite.txt', content('new content only'));
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-FILE-003 write empty file exists with length 0',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({ id: 'empty-file', tool: 'write', args: { filePath: 'empty.txt', content: '' } });
      t.provider.expectText({ id: 'empty-done', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'Write an empty file called empty.txt');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
      t.fs.expectFile('empty.txt');
      const stat = fs.statSync(t.host.workDir + '/empty.txt');
      if (stat.size !== 0) throw new Error('Empty file has non-zero size: ' + stat.size);
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-FUZZY-001 fuzzy_find returns real matching paths',
    fn: async (t) => {
      writeWorkFile(t.host.workDir, 'unique_target_a.py', 'x\n');
      writeWorkFile(t.host.workDir, 'unique_target_b.py', 'y\n');
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({ id: 'fuzzy-find', tool: 'fuzzy_find', args: { pattern: ['unique_target'] } });
      t.provider.expectText({ id: 'find-done', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'find files named unique_target');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
      const msgsStr = JSON.stringify((await t.client.messages(sid)).data);
      if (!msgsStr.includes('unique_target_a.py')) throw new Error('fuzzy_find results not found');
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-SCHEMA-001 tools have description and valid schema',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectText({ id: 'schema-warm', text: 'ok' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'list your tools');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.quick });
      const firstReq = t.provider.requests[0];
      if (!firstReq) throw new Error('No LLM request');
      const tools = firstReq.tools || [];
      if (tools.length === 0) throw new Error('No tools in request');
      const writeTool = tools.find((tool) => (tool.function?.name || tool.name) === 'write');
      if (!writeTool) throw new Error('write tool missing');
      const fn = writeTool.function || writeTool;
      if (!fn.description || fn.description.length < 5) throw new Error('write tool missing description');
      if (!fn.parameters) throw new Error('write tool missing parameters');
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-EXEC-002 JavaScript command stdout correct',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({ id: 'exec-js', tool: 'executor', args: { language: 'javascript', command: 'console.log("hello-js-e2e")' } });
      t.provider.expectText({ id: 'exec-js-done', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'run JavaScript: console.log("hello-js-e2e")');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
      const msgsStr = JSON.stringify((await t.client.messages(sid)).data);
      if (!msgsStr.includes('hello-js-e2e')) throw new Error('JS executor output not found');
      expectNoSessionError(t, sid);
    },
  },
];

import fs from 'node:fs';
export default tests;
