/**
 * p0-canary.js — P0 canary tests using shared-host mode.
 * Run: node e2e/opencode/specs/p0-canary.js
 */
import fs from 'node:fs';
import { runSuite, waitForSessionIdle, getSessionId } from '../harness/scenario.js';

function nl() { return Buffer.from([0x0a]).toString(); }
function content(s) { return s + nl(); }

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
    await t.client.prompt(sid, 'say ready');
    if (!await waitForSessionIdle(t.client, t.events, sid)) throw new Error('Not idle');
    const s = await t.client.sessionStatus(sid);
    const tokens = s.data?.data?.tokens || s.data?.tokens || {};
    if ((tokens.input || 0) === 0) throw new Error('No token usage');
    t.events.expectCount({ type: 'session.error', sessionID: sid, count: 0 });
    t.provider.expectSatisfied();
    t.provider.reset();
  }
},

{
  name: 'OC-FILE-001 write creates file with exact bytes',
  fn: async (t) => {
    const sess = await t.client.createSession();
    const sid = getSessionId(sess);
    t.provider.expectToolCall({ id: 'write-file', tool: 'write', args: { filePath: 'hello.txt', content: content('Hello World') } });
    t.provider.expectText({ id: 'write-done', text: 'done' });
    await t.client.prompt(sid, 'Write hello.txt with content "Hello World\\\\n"');
    if (!await waitForSessionIdle(t.client, t.events, sid)) throw new Error('Not idle');
    t.fs.expectFile('hello.txt');
    t.fs.expectFileContent('hello.txt', content('Hello World'));
    const msgsStr = JSON.stringify((await t.client.messages(sid)).data);
    if (!msgsStr.includes('Hello World')) throw new Error('Tool result not in messages');
    t.events.expectCount({ type: 'session.error', sessionID: sid, count: 0 });
    t.provider.expectSatisfied();
    t.provider.reset();
  }
},

{
  name: 'OC-FILE-006 read returns real file content',
  fn: async (t) => {
    const fileContent = content('Read test content\nsecond line');
    fs.writeFileSync(t.host.workDir + '/readme.txt', fileContent, 'utf8');
    const sess = await t.client.createSession();
    const sid = getSessionId(sess);
    t.provider.expectToolCall({ id: 'read-file', tool: 'read', args: { filePath: 'readme.txt' } });
    t.provider.expectText({ id: 'read-done', text: 'file content read' });
    await t.client.prompt(sid, 'read readme.txt and summarize it');
    if (!await waitForSessionIdle(t.client, t.events, sid)) throw new Error('Not idle');
    const msgsStr = JSON.stringify((await t.client.messages(sid)).data);
    if (!msgsStr.includes('Read test content')) throw new Error('Read content not found');
    t.events.expectCount({ type: 'session.error', sessionID: sid, count: 0 });
    t.provider.expectSatisfied();
    t.provider.reset();
  }
},

{
  name: 'OC-EXEC-001 shell command stdout correct',
  fn: async (t) => {
    const sess = await t.client.createSession();
    const sid = getSessionId(sess);
    t.provider.expectToolCall({ id: 'exec-echo', tool: 'executor', args: { language: 'shell', command: 'echo hello-e2e' } });
    t.provider.expectText({ id: 'exec-done', text: 'done' });
    await t.client.prompt(sid, 'run "echo hello-e2e" in shell');
    if (!await waitForSessionIdle(t.client, t.events, sid)) throw new Error('Not idle');
    const msgsStr = JSON.stringify((await t.client.messages(sid)).data);
    if (!msgsStr.includes('hello-e2e')) throw new Error('Executor output not found');
    t.events.expectCount({ type: 'session.error', sessionID: sid, count: 0 });
    t.provider.expectSatisfied();
    t.provider.reset();
  }
},

{
  name: 'OC-FUZZY-003 fuzzy_grep returns file, line, text',
  fn: async (t) => {
    fs.writeFileSync(t.host.workDir + '/grep_target.txt', content('unique-pattern-xyz\nhello world\nunique-pattern-xyz again'), 'utf8');
    const sess = await t.client.createSession();
    const sid = getSessionId(sess);
    t.provider.expectToolCall({ id: 'fuzzy-grep', tool: 'fuzzy_grep', args: { pattern: ['unique-pattern-xyz'] } });
    t.provider.expectText({ id: 'grep-done', text: 'found matches' });
    await t.client.prompt(sid, 'search for unique-pattern-xyz');
    if (!await waitForSessionIdle(t.client, t.events, sid)) throw new Error('Not idle');
    const msgsStr = JSON.stringify((await t.client.messages(sid)).data);
    if (!msgsStr.includes('unique-pattern-xyz')) throw new Error('Grep results not found');
    t.events.expectCount({ type: 'session.error', sessionID: sid, count: 0 });
    t.provider.expectSatisfied();
    t.provider.reset();
  }
},

{
  name: 'OC-REV-001 /loop task activates review mode',
  fn: async (t) => {
    const sess = await t.client.createSession();
    const sid = getSessionId(sess);
    t.provider.expectText({ id: 'rev-warm', text: 'ok' });
    await t.client.prompt(sid, 'hello');
    await waitForSessionIdle(t.client, t.events, sid, 30000);
    t.provider.reset();
    fetch(t.client._baseUrl + '/session/' + sid + '/command', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'x-opencode-directory': t.host.workDir },
      body: JSON.stringify({ command: 'loop', arguments: 'implement feature X' }),
    }).catch(() => {});
    await new Promise(r => setTimeout(r, 1500));
    const ndjsonPath = t.host.workDir + '/.wanxiangshu.ndjson';
    let found = false;
    if (fs.existsSync(ndjsonPath)) { found = fs.readFileSync(ndjsonPath, 'utf8').includes('loop_activated'); }
    if (!found) { found = t.events.allEvents.some(e => e.type === 'loop_activated' || e.properties?.type === 'loop_activated'); }
    if (!found) throw new Error('loop_activated not found');
    t.events.expectCount({ type: 'session.error', sessionID: sid, count: 0 });
    await t.client.abort(sid);
  }
},

{
  name: 'OC-WEB-001 websearch is listed in available tools',
  fn: async (t) => {
    const sess = await t.client.createSession();
    const sid = getSessionId(sess);
    t.provider.expectText({ id: 'web-avail', text: 'tools listed' });
    await t.client.prompt(sid, 'list your available tools');
    await waitForSessionIdle(t.client, t.events, sid, 30000);
    const firstReq = t.provider.requests[0];
    if (!firstReq) throw new Error('No LLM request made');
    const toolNames = (firstReq.tools || []).map(t => t.function?.name || t.name);
    if (!toolNames.includes('websearch')) { throw new Error('websearch not in tool list'); }
    t.events.expectCount({ type: 'session.error', sessionID: sid, count: 0 });
    t.provider.reset();
  }
},

{
  name: 'OC-FILE-002 write overwrites file with no trailing residue',
  fn: async (t) => {
    fs.writeFileSync(t.host.workDir + '/overwrite.txt', content('old content to overwrite'));
    const sess = await t.client.createSession();
    const sid = getSessionId(sess);
    t.provider.expectToolCall({ id: 'ow-file', tool: 'write', args: { filePath: 'overwrite.txt', content: content('new content only') } });
    t.provider.expectText({ id: 'ow-done', text: 'done' });
    await t.client.prompt(sid, 'Write overwrite.txt with content "new content only\\\\n"');
    if (!await waitForSessionIdle(t.client, t.events, sid)) throw new Error('Not idle');
    t.fs.expectFileContent('overwrite.txt', content('new content only'));
    t.events.expectCount({ type: 'session.error', sessionID: sid, count: 0 });
    t.provider.expectSatisfied();
    t.provider.reset();
  }
},

{
  name: 'OC-FILE-003 write empty file exists with length 0',
  fn: async (t) => {
    const sess = await t.client.createSession();
    const sid = getSessionId(sess);
    t.provider.expectToolCall({ id: 'empty-file', tool: 'write', args: { filePath: 'empty.txt', content: '' } });
    t.provider.expectText({ id: 'empty-done', text: 'done' });
    await t.client.prompt(sid, 'Write an empty file called empty.txt');
    if (!await waitForSessionIdle(t.client, t.events, sid)) throw new Error('Not idle');
    t.fs.expectFile('empty.txt');
    const stat = fs.statSync(t.host.workDir + '/empty.txt');
    if (stat.size !== 0) throw new Error('Empty file has non-zero size: ' + stat.size);
    t.events.expectCount({ type: 'session.error', sessionID: sid, count: 0 });
    t.provider.expectSatisfied();
    t.provider.reset();
  }
},

{
  name: 'OC-FUZZY-001 fuzzy_find returns real matching paths',
  fn: async (t) => {
    fs.writeFileSync(t.host.workDir + '/unique_target_a.py', 'x\n', 'utf8');
    fs.writeFileSync(t.host.workDir + '/unique_target_b.py', 'y\n', 'utf8');
    const sess = await t.client.createSession();
    const sid = getSessionId(sess);
    t.provider.expectToolCall({ id: 'fuzzy-find', tool: 'fuzzy_find', args: { pattern: ['unique_target'] } });
    t.provider.expectText({ id: 'find-done', text: 'done' });
    await t.client.prompt(sid, 'find files named unique_target');
    if (!await waitForSessionIdle(t.client, t.events, sid)) throw new Error('Not idle');
    const msgsStr = JSON.stringify((await t.client.messages(sid)).data);
    if (!msgsStr.includes('unique_target_a.py')) { throw new Error('fuzzy_find results not found'); }
    t.events.expectCount({ type: 'session.error', sessionID: sid, count: 0 });
    t.provider.expectSatisfied();
    t.provider.reset();
  }
},

{
  name: 'OC-SCHEMA-001 tools have description and valid schema',
  fn: async (t) => {
    const sess = await t.client.createSession();
    const sid = getSessionId(sess);
    t.provider.expectText({ id: 'schema-warm', text: 'ok' });
    await t.client.prompt(sid, 'list your tools');
    await waitForSessionIdle(t.client, t.events, sid, 30000);
    const firstReq = t.provider.requests[0];
    if (!firstReq) throw new Error('No LLM request');
    const tools = firstReq.tools || [];
    if (tools.length === 0) throw new Error('No tools in request');
    const writeTool = tools.find(t => (t.function?.name || t.name) === 'write');
    if (!writeTool) throw new Error('write tool missing');
    const fn = writeTool.function || writeTool;
    if (!fn.description || fn.description.length < 5) throw new Error('write tool missing description');
    if (!fn.parameters) throw new Error('write tool missing parameters');
    t.events.expectCount({ type: 'session.error', sessionID: sid, count: 0 });
    t.provider.expectSatisfied();
    t.provider.reset();
  }
},

];

const exitCode = await runSuite({ plugin: true, timeoutMs: 90000 }, tests);
process.exit(exitCode);
