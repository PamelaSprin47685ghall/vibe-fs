import plugin from '../build/src/Opencode/Plugin.js';
import { createRegistration, getPluginToolPolicy, buildCapsFileReadData, deduplicateReadOutputs, deduplicateReadOutputsWithSeen, collectReadOutputs } from '../build/src/Index.js';
import { runSubagent } from '../build/src/Opencode/Session.js';
import { registerChildAgent, unregisterChildAgent } from '../build/src/Opencode/ChildAgent.js';
import checkSyntax from '../build/src/Shell/TreeSitterSyntax.mjs';
import * as fs from 'node:fs/promises';
import * as path from 'node:path';

function check(label, condition) {
  if (!condition) {
    console.error('FAIL:', label);
    process.exitCode = 1;
  } else {
    console.log('PASS:', label);
  }
}

const p = await plugin({ directory: '/tmp/vibe' });

check('plugin.name', p.name === 'kunwei');
check('plugin.tool', typeof p.tool === 'object');
check('plugin.config', typeof p.config === 'function');
check('plugin.event', typeof p.event === 'function');
check('plugin.mcp', typeof p.mcp === 'object');
check('plugin.tool.execute.after', typeof p['tool.execute.after'] === 'function');
check('plugin.experimental.chat.messages.transform', typeof p['experimental.chat.messages.transform'] === 'function');
check('plugin.command.execute.before', typeof p['command.execute.before'] === 'function');

const reg = await createRegistration({});

check('mux.toolNames', Array.isArray(reg.toolNames));
check('mux.tools', Array.isArray(reg.tools));
check('mux.mcpServers', typeof reg.mcpServers === 'object');
check('mux.contextInjector', typeof reg.contextInjector === 'object');
const policy = reg.getToolPolicy('x', 'orchestrator');
check('mux.getToolPolicy non-null', policy !== null && typeof policy === 'object');
check('mux.getToolPolicy shape', Array.isArray(policy?.add) && Array.isArray(policy?.remove));

const syntax = await checkSyntax('const x = 1;', 'test.js');
check('tree-sitter returns a result', typeof syntax === 'object');
check('tree-sitter ok', syntax.ok === true);
check('tree-sitter no errors', Array.isArray(syntax.errors) && syntax.errors.length === 0);

// ── Fix #1: getPluginToolPolicy(agentId, role) two-arg signature ──
const pol1 = getPluginToolPolicy('some-agent', 'orchestrator');
check('getPluginToolPolicy(agentId, role) returns policy', pol1 !== undefined && Array.isArray(pol1.remove));
const pol2 = getPluginToolPolicy('some-agent');
check('getPluginToolPolicy(agentId) with undefined role returns policy', pol2 !== undefined);

// ── Fix #2: eventHook is a two-argument JS function ──
check('eventHook.length === 2', reg.eventHook.length === 2);
const ehResult = reg.eventHook({ type: 'stream-abort', workspaceId: 'test-ws' }, null);
check('eventHook(event, helpers) returns Promise', ehResult && typeof ehResult.then === 'function');
await ehResult;

// ── Fix #3: syntax wrapper passes config and appends diagnostics ──
const syntaxWrapper = reg.wrappers.find(w => w.targetTool === 'file_edit_replace_string');
check('syntax wrapper exists', !!syntaxWrapper);
check('syntax wrapper.targetTool set', syntaxWrapper.targetTool === 'file_edit_replace_string');
check('syntax wrapper.wrapper is function', typeof syntaxWrapper.wrapper === 'function');
const mockEditTool = { execute: async (args) => 'File written' };
const wrappedEdit = syntaxWrapper.wrapper(mockEditTool, { cwd: '/tmp' });
check('wrapped tool has execute', typeof wrappedEdit.execute === 'function');
const editResult = await wrappedEdit.execute({ file_path: 'nonexistent.js' });
check('syntax wrapper returns result for missing file', typeof editResult === 'string');

// ── Fix #3b: todo_write nudge wrapper ──
const todoWrapper = reg.wrappers.find(w => w.targetTool === 'todo_write');
check('todo_write wrapper exists', !!todoWrapper);
const mockTodoTool = { execute: async () => 'Todos updated' };
const wrappedTodo = todoWrapper.wrapper(mockTodoTool, {});
const todoResult = await wrappedTodo.execute({});
check('todo_write wrapper appends reverie nudge', todoResult.includes('Think thrice'));

// ── Fix #4: webfetch schema and execute handle all declared params ──
const webfetchDef = reg.tools.find(t => t.name === 'webfetch');
const wfParams = webfetchDef.parameters.properties;
check('webfetch schema has url', !!wfParams.url);
check('webfetch schema has extract_main', !!wfParams.extract_main);
check('webfetch schema has prefer_llms_txt', !!wfParams.prefer_llms_txt);
check('webfetch schema has prompt', !!wfParams.prompt);
check('webfetch schema has timeout', !!wfParams.timeout);
check('webfetch execute is function', typeof webfetchDef.execute === 'function');

// ── Fix #5: slash commands ──
check('slash commands count', reg.slashCommands.length === 2);
const loopCmd = reg.slashCommands.find(c => c.key === 'loop');
check('loop command exists', !!loopCmd);
check('loop command has execute', typeof loopCmd.execute === 'function');
const loopResult = await loopCmd.execute('test-ws', '');
check('loop with empty args cancels', loopResult === 'Loop mode cancelled.');
const loopReviewCmd = reg.slashCommands.find(c => c.key === 'loop-review');
check('loop-review command exists', !!loopReviewCmd);
check('loop-review execute.length === 2', loopReviewCmd.execute.length === 2);

// ── Wrapper completeness ──
check('wrapper count === 5', reg.wrappers.length === 5);
const wrapperTargets = reg.wrappers.map(w => w.targetTool).sort();
check('wrapper targets correct', JSON.stringify(wrapperTargets) === JSON.stringify(['file_edit_insert', 'file_edit_replace_string', 'todo_write', 'web_fetch', 'web_search']));

// ── Tool completeness ──
check('tool count === 11', reg.tools.length === 11);
const toolNames = reg.tools.map(t => t.name).sort();
check('has editor tool', toolNames.includes('editor'));
check('has webfetch tool', toolNames.includes('webfetch'));
check('has write tool', toolNames.includes('write'));
check('has submit_review tool', toolNames.includes('submit_review'));

// ── web_search override wrapper forwards config ──
const wsOverride = reg.wrappers.find(w => w.targetTool === 'web_search');
check('web_search wrapper exists', !!wsOverride);
const wsWrapped = wsOverride.wrapper(null, { cwd: '/tmp', workspaceId: 'ws1' });
check('web_search override has description', typeof wsWrapped.description === 'string');
check('web_search override has parameters', typeof wsWrapped.parameters === 'object');
check('web_search override has execute', typeof wsWrapped.execute === 'function');

// ── buildCapsFileReadData returns real array ──
const tmpDir = await fs.mkdtemp(path.join('/tmp', 'caps-test-'));
await fs.writeFile(path.join(tmpDir, 'CAPS.md'), '# Capabilities\nTest content');
const capsEntries = await buildCapsFileReadData(tmpDir);
check('buildCapsFileReadData returns array', Array.isArray(capsEntries));
check('buildCapsFileReadData finds caps file', capsEntries.length === 1);
check('caps entry has path', capsEntries[0]?.path === 'CAPS.md');
check('caps entry has callId', typeof capsEntries[0]?.callId === 'string');
check('caps entry output has content', typeof capsEntries[0]?.output?.content === 'string');
await fs.rm(tmpDir, { recursive: true });

// ── messages.transform CAPS injection ──
const makeMessage = (info = {}, parts = []) => ({ info: { id: 'msg-1', sessionID: 'sess-1', agent: 'orchestrator', ...info }, parts });

await fs.mkdir('/tmp/vibe', { recursive: true });

const originalMsg = makeMessage();
const noCapsOut = { messages: [originalMsg] };
await p['experimental.chat.messages.transform']({}, noCapsOut);
check('caps transform leaves messages when no caps files', noCapsOut.messages.length === 1 && noCapsOut.messages[0] === originalMsg);

await fs.writeFile('/tmp/vibe/CAPS.md', '# Capabilities\nTest content');

const emptyCapsOut = { messages: [] };
await p['experimental.chat.messages.transform']({}, emptyCapsOut);
check('caps transform leaves empty messages', emptyCapsOut.messages.length === 0);

const excludedOut = { messages: [makeMessage({ agent: 'browser' })] };
await p['experimental.chat.messages.transform']({}, excludedOut);
check('caps transform skips excluded agent', excludedOut.messages.length === 1);

const normalOut = { messages: [originalMsg] };
await p['experimental.chat.messages.transform']({}, normalOut);
check('caps transform injects two messages', normalOut.messages.length === 3);
check('caps transform user role', normalOut.messages[0].info.role === 'user');
check('caps transform assistant role', normalOut.messages[1].info.role === 'assistant');
check('caps transform assistant has tool parts', normalOut.messages[1].parts.length >= 1);
check('caps transform tool part is read', normalOut.messages[1].parts[0].tool === 'read');
check('caps transform read output contains path', String(normalOut.messages[1].parts[0].state.output).includes('CAPS.md'));
check('caps transform read output contains content', String(normalOut.messages[1].parts[0].state.output).includes('Test content'));
check('caps transform preserves original message', normalOut.messages[2] === originalMsg);

const existingCapsOut = {
  messages: [
    { info: { id: 'caps-synth-user-old', agent: 'orchestrator' }, parts: [] },
    { info: { id: 'caps-synth-assistant-old', agent: 'orchestrator' }, parts: [] },
    makeMessage()
  ]
};
await p['experimental.chat.messages.transform']({}, existingCapsOut);
check('caps transform replaces existing caps messages', existingCapsOut.messages.length === 3);
check('caps transform new user id prefix', String(existingCapsOut.messages[0].info.id).startsWith('caps-synth-user-'));
check('caps transform new assistant id prefix', String(existingCapsOut.messages[1].info.id).startsWith('caps-synth-assistant-'));

// caps transform must mutate output.messages in place, never swap the array reference
const inPlaceFresh = { messages: [originalMsg] };
const inPlaceFreshRef = inPlaceFresh.messages;
await p['experimental.chat.messages.transform']({}, inPlaceFresh);
check('caps transform mutates array in place (fresh inject)', inPlaceFresh.messages === inPlaceFreshRef);

const inPlaceReplace = {
  messages: [
    { info: { id: 'caps-synth-user-stale', agent: 'orchestrator' }, parts: [] },
    { info: { id: 'caps-synth-assistant-stale', agent: 'orchestrator' }, parts: [] },
    makeMessage()
  ]
};
const inPlaceReplaceRef = inPlaceReplace.messages;
await p['experimental.chat.messages.transform']({}, inPlaceReplace);
check('caps transform preserves array ref when replacing caps', inPlaceReplace.messages === inPlaceReplaceRef);

await fs.unlink('/tmp/vibe/CAPS.md');

const inPlaceNoCaps = { messages: [makeMessage()] };
const inPlaceNoCapsRef = inPlaceNoCaps.messages;
await p['experimental.chat.messages.transform']({}, inPlaceNoCaps);
check('caps transform preserves array ref when no caps files', inPlaceNoCaps.messages === inPlaceNoCapsRef);

// ── deduplicateReadOutputs collapses repeated reads ──
const deduped = deduplicateReadOutputs([
  { parts: [{ type: 'dynamic-tool', toolName: 'read', state: 'output-available', output: 'same content', toolCallId: '1' }] },
  { parts: [{ type: 'dynamic-tool', toolName: 'file_read', state: 'output-available', output: 'same content', toolCallId: '2' }] },
]);
check('deduplicateReadOutputs returns array', Array.isArray(deduped));
check('deduplicateReadOutputs keeps first', deduped[0]?.parts?.[0]?.output === 'same content');
check('deduplicateReadOutputs replaces repeat with marker', deduped[1]?.parts?.[0]?.output === '[No Change Since Previous Read/Write]');

// ── collectReadOutputs + deduplicateReadOutputsWithSeen seed from history ──
const history = [
  { parts: [{ type: 'dynamic-tool', toolName: 'read', state: 'output-available', output: 'seen before', toolCallId: 'h1' }] },
];
const seen = collectReadOutputs(history);
check('collectReadOutputs returns array', Array.isArray(seen) && seen.length === 1 && seen[0] === 'seen before');
const seeded = deduplicateReadOutputsWithSeen(seen, [
  { parts: [{ type: 'dynamic-tool', toolName: 'file_read', state: 'output-available', output: 'seen before', toolCallId: 'n1' }] },
]);
check('deduplicateReadOutputsWithSeen replaces historical repeat', seeded[0]?.parts?.[0]?.output === '[No Change Since Previous Read/Write]');

// ── opencode messages.transform: read-output dedup MUST mutate in place ──
// REGRESSION GUARD. The opencode host keys internal bookkeeping (UI state,
// tool-call maps, render caches) off the part and state object references
// found in the messages it hands to the transform hook. Building a fresh
// object chain — `{...part, state: {...state, output: marker}}` or the Fable
// equivalent `Dyn.withKey` — leaves the host pointing at the stale objects;
// the marker lands on a copy nobody reads, so the second read silently keeps
// its full original text. The only correct write is `state.output = marker`
// on the live object. These assertions pin that contract: every enclosing
// reference must stay identical, only the `output` field may change.
const stableContent = `${'line of stable content\n'.repeat(8)}`;
const readStateA = { output: stableContent };
const readStateB = { output: stableContent };
const readPartA = { type: 'tool', tool: 'read', state: readStateA };
const readPartB = { type: 'tool', tool: 'read', state: readStateB };
const dedupInPlace = {
  messages: [
    { info: { id: 'dedup-m1', agent: 'orchestrator' }, parts: [readPartA] },
    { info: { id: 'dedup-m2', agent: 'orchestrator' }, parts: [readPartB] },
  ],
};
const dedupMessagesRef = dedupInPlace.messages;
await p['experimental.chat.messages.transform']({}, dedupInPlace);

check('opencode dedup keeps messages array ref (host contract)', dedupInPlace.messages === dedupMessagesRef);
check('opencode dedup keeps first part ref (host bookkeeping)', dedupInPlace.messages[0].parts[0] === readPartA);
check('opencode dedup keeps second part ref (host bookkeeping)', dedupInPlace.messages[1].parts[0] === readPartB);
check('opencode dedup keeps first state ref (host bookkeeping)', dedupInPlace.messages[0].parts[0].state === readStateA);
check('opencode dedup keeps second state ref (host bookkeeping)', dedupInPlace.messages[1].parts[0].state === readStateB);
check('opencode dedup keeps first read output', dedupInPlace.messages[0].parts[0].state.output === stableContent);
check('opencode dedup replaces exact duplicate with marker', dedupInPlace.messages[1].parts[0].state.output === '[No Change Since Previous Read/Write]');

// Same contract under the substring-repeat shape: second read appends new
// content to the first. Still must mutate state.output in place.
const supersetState = { output: `${stableContent}${'new content\n'.repeat(8)}` };
const supersetPart = { type: 'tool', tool: 'read', state: supersetState };
const dedupSuperset = {
  messages: [
    { info: { id: 'dedup-s1', agent: 'orchestrator' }, parts: [readPartA] },
    { info: { id: 'dedup-s2', agent: 'orchestrator' }, parts: [supersetPart] },
  ],
};
await p['experimental.chat.messages.transform']({}, dedupSuperset);
check('opencode dedup superset keeps state ref', dedupSuperset.messages[1].parts[0].state === supersetState);
check('opencode dedup superset replaces with marker', dedupSuperset.messages[1].parts[0].state.output === '[No Change Since Previous Read/Write]');

// ── writeTool field-missing vs empty-string semantics ──
const writeDef = reg.tools.find(t => t.name === 'write');
const writeMissingPath = await writeDef.execute({ cwd: '/tmp' }, { content: 'x' });
check('write missing file_path error', writeMissingPath.includes('file_path'));
const writeMissingContent = await writeDef.execute({ cwd: '/tmp' }, { file_path: 'a.txt' });
check('write missing content error', writeMissingContent.includes('content'));
const writeEmptyDir = await fs.mkdtemp(path.join('/tmp', 'write-test-'));
const writeEmptyResult = await writeDef.execute({ cwd: writeEmptyDir }, { file_path: 'empty.txt', content: '' });
check('write empty string succeeds', writeEmptyResult.includes('Successfully wrote'));
check('write empty file has correct content', await fs.readFile(path.join(writeEmptyDir, 'empty.txt'), 'utf-8') === '');
await fs.rm(writeEmptyDir, { recursive: true });

// ── loop command returns Promise ──
const loopCmd2 = reg.slashCommands.find(c => c.key === 'loop');
const loopExecResult = loopCmd2.execute('test-ws', 'some task');
check('loop execute returns Promise', loopExecResult && typeof loopExecResult.then === 'function');
const loopResolved = await loopExecResult;
check('loop resolve is string with task', typeof loopResolved === 'string' && loopResolved.includes('some task'));

// ── Agent config: browser/summarizer get proper builtin parent config ──
const agentCfgResult = await p.config({ agent: { browser: { model: 'kimi-for-coding/k2p7' }, summarizer: { model: 'opencode-go/deepseek-v4-flash' }, plan: { disable: true }, custom: { model: 'custom-model' } } });
check('agent config returns object', typeof agentCfgResult === 'object');
const browserAgent = agentCfgResult?.agent?.browser;
check('browser has prompt', typeof browserAgent?.prompt === 'string' && browserAgent.prompt.includes('browser automation'));
check('browser mode is subagent', browserAgent?.mode === 'subagent');
check('browser mcps includes stealth-browser-mcp', Array.isArray(browserAgent?.mcps) && browserAgent.mcps.includes('stealth-browser-mcp'));
check('browser permission allows stealth-browser-mcp', browserAgent?.permission?.['stealth-browser-mcp_*'] === 'allow');
check('browser tools enables stealth-browser-mcp', browserAgent?.tools?.['stealth-browser-mcp_*'] === true);
const summarizerAgent = agentCfgResult?.agent?.summarizer;
check('summarizer has prompt', typeof summarizerAgent?.prompt === 'string' && summarizerAgent.prompt.includes('output summarizer'));
check('summarizer mode is subagent', summarizerAgent?.mode === 'subagent');
check('summarizer tools only agent_report', summarizerAgent?.tools?.agent_report === true && summarizerAgent?.tools?.read === false);
check('user plan disable preserved', agentCfgResult?.agent?.plan?.disable === true);

// ── Role defaults applied to all agents, preserving user fields ──
const customAgent = agentCfgResult?.agent?.custom;
check('custom agent exists', typeof customAgent === 'object');
check('custom agent model preserved', customAgent?.model === 'custom-model');
check('custom agent gets reverie bash deny', customAgent?.permission?.bash === 'deny');
check('custom agent gets reverie stealth-browser deny', customAgent?.permission?.['stealth-browser-mcp_*'] === 'deny');
check('custom agent mode subagent', customAgent?.mode === 'subagent');

const planAgent = agentCfgResult?.agent?.plan;
check('plan disable preserved', planAgent?.disable === true);
check('plan gets reverie bash deny', planAgent?.permission?.bash === 'deny');
check('plan gets reverie stealth-browser deny', planAgent?.permission?.['stealth-browser-mcp_*'] === 'deny');
check('plan mode subagent', planAgent?.mode === 'subagent');

const orchestratorAgent = agentCfgResult?.agent?.orchestrator;
check('orchestrator exists', typeof orchestratorAgent === 'object');
check('orchestrator stealth-browser denied', orchestratorAgent?.permission?.['stealth-browser-mcp_*'] === 'deny');
check('orchestrator mcps empty', Array.isArray(orchestratorAgent?.mcps) && orchestratorAgent.mcps.length === 0);

// ── Legacy agents dropped ──
const legacyResult = await p.config({ agent: { basher: { customField: 'from-basher' }, runner: { customField: 'from-runner' } } });
check('basher dropped', legacyResult?.agent?.basher === undefined);
check('runner dropped', legacyResult?.agent?.runner === undefined);

// ── chat.message enforces tool boundaries ──
check('plugin.chat.message', typeof p['chat.message'] === 'function');

const orchChat = { message: { tools: { 'stealth-browser-mcp_*': true, 'stealth-browser-mcp_foo': true, 'read': true } } };
await p['chat.message']({ sessionID: 'root', agent: 'orchestrator' }, orchChat);
check('orch stealth-browser-mcp_* disabled', orchChat.message.tools['stealth-browser-mcp_*'] === false);
check('orch stealth-browser-mcp_foo disabled', orchChat.message.tools['stealth-browser-mcp_foo'] === false);
check('orch read preserved', orchChat.message.tools['read'] === true);

const editorChat = { message: { tools: { 'stealth-browser-mcp_bar': true, 'patch': true } } };
await p['chat.message']({ sessionID: 'root', agent: 'editor' }, editorChat);
check('editor stealth-browser-mcp_bar disabled', editorChat.message.tools['stealth-browser-mcp_bar'] === false);
check('editor stealth-browser-mcp_* disabled', editorChat.message.tools['stealth-browser-mcp_*'] === false);
check('editor patch preserved', editorChat.message.tools['patch'] === true);

const browserChat = { message: { tools: { 'stealth-browser-mcp_*': true, 'stealth-browser-mcp_foo': true, 'read': true } } };
await p['chat.message']({ sessionID: 'root', agent: 'browser' }, browserChat);
check('browser stealth-browser-mcp_* preserved', browserChat.message.tools['stealth-browser-mcp_*'] === true);
check('browser stealth-browser-mcp_foo preserved', browserChat.message.tools['stealth-browser-mcp_foo'] === true);
check('browser read preserved', browserChat.message.tools['read'] === true);

registerChildAgent('child-browser-session', 'browser', undefined);
const childChat = { message: { tools: { 'stealth-browser-mcp_*': true } } };
await p['chat.message']({ sessionID: 'child-browser-session' }, childChat);
check('child session resolves to browser', childChat.message.tools['stealth-browser-mcp_*'] === true);
unregisterChildAgent('child-browser-session');

const childOrchChat = { message: { tools: { 'stealth-browser-mcp_*': true } } };
registerChildAgent('child-orch-session', 'orchestrator', undefined);
await p['chat.message']({ sessionID: 'child-orch-session' }, childOrchChat);
check('child session resolves to orchestrator', childOrchChat.message.tools['stealth-browser-mcp_*'] === false);
unregisterChildAgent('child-orch-session');

// ── chat.message websearch/webfetch tool boundaries ──
const orchWebChat = { message: { tools: { websearch: true, webfetch: true, read: true } } };
await p['chat.message']({ sessionID: 'root', agent: 'orchestrator' }, orchWebChat);
check('orch websearch preserved', orchWebChat.message.tools.websearch === true);
check('orch webfetch preserved', orchWebChat.message.tools.webfetch === true);
check('orch read preserved alongside web tools', orchWebChat.message.tools.read === true);

const editorWebChat = { message: { tools: { websearch: true, webfetch: true, patch: true } } };
await p['chat.message']({ sessionID: 'root', agent: 'editor' }, editorWebChat);
check('editor websearch forced false', editorWebChat.message.tools.websearch === false);
check('editor webfetch forced false', editorWebChat.message.tools.webfetch === false);
check('editor patch preserved', editorWebChat.message.tools.patch === true);

const greperWebChat = { message: { tools: { websearch: true, webfetch: true, fuzzy_find: true } } };
await p['chat.message']({ sessionID: 'root', agent: 'greper' }, greperWebChat);
check('greper websearch forced false', greperWebChat.message.tools.websearch === false);
check('greper webfetch forced false', greperWebChat.message.tools.webfetch === false);
check('greper fuzzy_find preserved', greperWebChat.message.tools.fuzzy_find === true);

const reverieWebChat = { message: { tools: { websearch: true, webfetch: true } } };
await p['chat.message']({ sessionID: 'root', agent: 'reverie' }, reverieWebChat);
check('reverie websearch forced false', reverieWebChat.message.tools.websearch === false);
check('reverie webfetch forced false', reverieWebChat.message.tools.webfetch === false);

const browserWebChat = { message: { tools: { websearch: true, webfetch: true, 'stealth-browser-mcp_*': true } } };
await p['chat.message']({ sessionID: 'root', agent: 'browser' }, browserWebChat);
check('browser websearch forced false', browserWebChat.message.tools.websearch === false);
check('browser webfetch forced false', browserWebChat.message.tools.webfetch === false);
check('browser stealth-browser preserved', browserWebChat.message.tools['stealth-browser-mcp_*'] === true);

check('plugin.tool.definition', typeof p['tool.definition'] === 'function');
check('plugin.tool.execute.before', typeof p['tool.execute.before'] === 'function');

// tool.definition: strip internal `_ui` from editor/greper schemas
const editorDefOut = { parameters: { properties: { intents: { type: 'array' }, _ui: { type: 'string' } }, required: ['intents', '_ui'] } };
await p['tool.definition']({ toolID: 'editor' }, editorDefOut);
check('tool.definition strips editor _ui property', editorDefOut.parameters.properties._ui === undefined);
check('tool.definition strips editor _ui required', !editorDefOut.parameters.required.includes('_ui'));
check('tool.definition keeps editor intents property', editorDefOut.parameters.properties.intents !== undefined);

const greperDefOut = { parameters: { properties: { intents: { type: 'array' }, _ui: { type: 'string' } }, required: ['intents', '_ui'] } };
await p['tool.definition']({ toolID: 'greper' }, greperDefOut);
check('tool.definition strips greper _ui property', greperDefOut.parameters.properties._ui === undefined);
check('tool.definition strips greper _ui required', !greperDefOut.parameters.required.includes('_ui'));

const otherDefOut = { parameters: { properties: { x: { type: 'string' }, _ui: { type: 'string' } }, required: ['x', '_ui'] } };
await p['tool.definition']({ toolID: 'read' }, otherDefOut);
check('tool.definition leaves other tools alone', otherDefOut.parameters.properties._ui !== undefined && otherDefOut.parameters.required.includes('_ui'));

// tool.execute.before: populate `_ui` from joined intents
const editorExecOut = { args: { intents: [['fix bug', ['a.ts']], ['add feature', ['b.ts']]] } };
await p['tool.execute.before']({ tool: 'editor', sessionID: 's1', callID: 'c1' }, editorExecOut);
check('tool.execute.before populates editor _ui', editorExecOut.args._ui === 'fix bug; add feature');

const greperExecOut = { args: { intents: ['find usages', 'list exports'] } };
await p['tool.execute.before']({ tool: 'greper', sessionID: 's1', callID: 'c2' }, greperExecOut);
check('tool.execute.before populates greper _ui', greperExecOut.args._ui === 'find usages; list exports');

const invalidUiOut = { args: { intents: ['x'], _ui: 123 } };
await p['tool.execute.before']({ tool: 'greper', sessionID: 's1', callID: 'c3' }, invalidUiOut);
check('tool.execute.before rejects non-string _ui', typeof invalidUiOut.args._ui === 'string' && invalidUiOut.args._ui.includes('must be a string'));

// ── Subagent parent relationship: child sessions must receive parentID ──
const createCalls = [];
const promptCalls = [];
const mockClient = {
  session: {
    create: async (arg) => {
      createCalls.push(arg);
      return { data: { id: 'child-session-123' } };
    },
    prompt: async (arg) => {
      promptCalls.push(arg);
    },
    messages: async (arg) => ({
      data: [
        { info: { role: 'user' }, parts: [{ type: 'text', text: 'navigate to example.com' }] },
        { info: { role: 'assistant' }, parts: [{ type: 'text', text: 'Found the page title: Example Domain' }] }
      ]
    }),
    abort: async () => {}
  }
};
const subResult = await runSubagent(mockClient, 'browser', 'Browser', 'navigate to example.com', '/tmp/vibe', 'parent-session-456', { abort: null });
check('runSubagent returns string', typeof subResult === 'string');
check('session.create received parentID', createCalls[0]?.body?.parentID === 'parent-session-456');
check('session.prompt uses child id', promptCalls[0]?.path?.id === 'child-session-123');
check('session.prompt uses browser agent', promptCalls[0]?.body?.agent === 'browser');
check('subagent output returned to caller', subResult.includes('Example Domain'));

// ── Nested subagent parent resolution ──
const createCalls2 = [];
const mockClient2 = {
  session: {
    create: async (arg) => {
      createCalls2.push(arg);
      return { data: { id: `child-${createCalls2.length}` } };
    },
    prompt: async () => {},
    messages: async () => ({ data: [] }),
    abort: async () => {}
  }
};
await runSubagent(mockClient2, 'browser', 'Browser', 'first', '/tmp/vibe', 'root-session', { abort: null });
await runSubagent(mockClient2, 'editor', 'Editor', 'second', '/tmp/vibe', 'child-1', { abort: null });
check('nested subagent resolves to root parent', createCalls2[1]?.body?.parentID === 'root-session');
