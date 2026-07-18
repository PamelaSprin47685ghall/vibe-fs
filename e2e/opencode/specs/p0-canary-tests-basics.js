/**
 * p0-canary-tests-basics.js — File / fuzzy / executor / schema / web tests.
 * Kept under the 300-line Kolmogorov line budget.
 */

import { getSessionId, setupScenario, teardownScenario } from '../harness/scenario.js';
import { content, writeWorkFile, extractToolNames, validateToolSchema, findToolPart, assertFuzzyGrepResult, TIMEOUTS } from './p0-canary-utils.js';
import fs from 'node:fs';

function expectNoSessionError(t, sid) {
  t.events.expectCount({ type: 'session.error', sessionID: sid, count: 0 });
}

const tests = [
  {
    name: 'OC-BOOT-001 opencode serve starts with plugin loaded',
    fn: async (t) => {
      // Exact command list oracle.
      const cmds = await t.client.request('GET', '/command');
      if (!cmds.ok) throw new Error(`GET /command failed: ${cmds.status}`);
      if (!Array.isArray(cmds.data)) throw new Error('/command did not return an array');
      const names = cmds.data.map((c) => c.name);
      if (!names.includes('loop')) throw new Error(`loop command missing; got: ${names.join(', ')}`);

      // Plugin tool registration oracle: first LLM request must contain plugin tools.
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      if (!sid) throw new Error('No session ID');
      t.provider.expectText({ id: 'warmup', text: 'ready' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'say ready');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
      const firstReq = t.provider.requests[0];
      if (!firstReq) throw new Error('No LLM request captured');
      const tools = firstReq.tools || [];
      const toolNames = extractToolNames(tools);
      const unique = new Set(toolNames);
      if (unique.size !== toolNames.length) throw new Error(`duplicate tool names: ${toolNames.join(', ')}`);
      const required = ['write', 'executor', 'fuzzy_find', 'fuzzy_grep', 'coder'];
      for (const name of required) {
        if (!unique.has(name)) throw new Error(`plugin tool missing: ${name} in ${toolNames.join(', ')}`);
      }

      // Plugin load failure must be observable in stderr if it ever occurs.
      const stderr = t.host.stderrLog || '';
      if (/plugin.*(error|fail|cannot load)/i.test(stderr)) {
        throw new Error(`plugin load error in stderr: ${stderr.slice(-500)}`);
      }

      // No-plugin baseline: loop should not appear when plugin is disabled.
      const baseline = await setupScenario({ plugin: false, contextLimit: 20000 });
      try {
        const baselineCmds = await baseline.client.request('GET', '/command');
        if (!baselineCmds.ok) throw new Error(`baseline GET /command failed: ${baselineCmds.status}`);
        const baselineNames = (baselineCmds.data || []).map((c) => c.name);
        if (baselineNames.includes('loop')) {
          throw new Error(`baseline /command unexpectedly contains loop: ${baselineNames.join(', ')}`);
        }
      } finally {
        await teardownScenario(baseline);
      }

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
      const messages = (await t.client.messages(sid)).data || [];
      const part = findToolPart(messages, 'write', (p) => p.state?.input?.filePath === 'hello.txt');
      if (!part) throw new Error('write tool part not found in messages');
      if (part.state?.status !== 'completed') throw new Error(`write tool state: ${part.state?.status}`);
      if (part.state?.input?.content !== content('Hello World')) {
        throw new Error(`write tool input content mismatch: ${JSON.stringify(part.state?.input?.content)}`);
      }
      if (typeof part.state?.output !== 'string' || part.state.output.length === 0) {
        throw new Error('write tool completed but produced no output');
      }
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
      const messages = (await t.client.messages(sid)).data || [];
      const part = findToolPart(messages, 'fuzzy_grep');
      if (!part) throw new Error('fuzzy_grep tool part not found');
      if (part.state?.status !== 'completed') throw new Error(`fuzzy_grep state: ${part.state?.status}`);
      assertFuzzyGrepResult(part.state.output, {
        expectedFile: 'grep_target.txt',
        expectedPattern: 'unique-pattern-xyz',
        minMatches: 2,
      });
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
      validateToolSchema(tools);
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

  {
    name: 'OC-FILE-004 Unicode UTF-8 content survives round-trip',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      const unicodeContent = '\u4f60\u597d\u4e16\u754c \ud83c\udf0d \u65e5\u672c\u8a9e\u30c6\u30b9\u30c8';
      t.provider.expectToolCall({ id: 'uni-file', tool: 'write', args: { filePath: 'unicode.txt', content: content(unicodeContent) } });
      t.provider.expectText({ id: 'uni-done', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'Write unicode.txt with multilingual content');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
      t.fs.expectFile('unicode.txt');
      t.fs.expectFileContent('unicode.txt', content(unicodeContent));
      const msgsStr = JSON.stringify((await t.client.messages(sid)).data);
      if (!msgsStr.includes(unicodeContent)) throw new Error('Unicode content not found in messages');
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-FILE-005 multi-line content preserves every line',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      const multiLine = ['line one', 'line two', 'line three'].join('\n');
      t.provider.expectToolCall({ id: 'ml-file', tool: 'write', args: { filePath: 'multiline.txt', content: content(multiLine) } });
      t.provider.expectText({ id: 'ml-done', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'Write multiline.txt with three lines');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
      t.fs.expectFile('multiline.txt');
      const actual = fs.readFileSync(t.host.workDir + '/multiline.txt', 'utf8');
      if (actual !== content(multiLine)) throw new Error(`multi-line mismatch:\n  expected: ${JSON.stringify(content(multiLine))}\n  got: ${JSON.stringify(actual)}`);
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-FILE-008 write-then-read returns identical content',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      const roundTrip = 'round-trip consistency check 42';
      t.provider.expectToolCall({ id: 'rt-write', tool: 'write', args: { filePath: 'roundtrip.txt', content: content(roundTrip) } });
      t.provider.expectToolCall({ id: 'rt-read', tool: 'read', args: { filePath: 'roundtrip.txt' } });
      t.provider.expectText({ id: 'rt-done', text: 'read complete' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'Write roundtrip.txt then read it back');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
      const messages = (await t.client.messages(sid)).data || [];
      const msgsStr = JSON.stringify(messages);
      if (!msgsStr.includes(roundTrip)) throw new Error('Round-trip content missing from session messages');
      t.fs.expectFile('roundtrip.txt');
      t.fs.expectFileContent('roundtrip.txt', content(roundTrip));
      expectNoSessionError(t, sid);
    },
  },
];

export default tests;
