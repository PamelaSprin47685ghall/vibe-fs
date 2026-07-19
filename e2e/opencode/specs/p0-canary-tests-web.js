/**
 * p0-canary-tests-web.js — E2E tests for Web search, Web fetch, and Browser agent.
 */

import { getSessionId } from '../harness/scenario.js';
import { extractToolNames, findToolPart, readNdjsonLines, TIMEOUTS } from './p0-canary-utils.js';

function expectNoSessionError(t, sid) {
  t.events.expectCount({ type: 'session.error', sessionID: sid, count: 0 });
}

const tests = [
  {
    name: 'OC-WEB-002 what_to_summarize enters subagent prompt',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({
        id: 'web-search-002',
        tool: 'web_search',
        args: {
          query: 'E2E websearch test',
          what_to_summarize: 'extract key technology stacks',
          numResults: 1
        }
      });
      t.provider.expectText({ id: 'web-summary-002', text: 'Test search content for E2E.' });
      t.provider.expectText({ id: 'web-done-002', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'search the web for E2E websearch test');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const subagentReq = t.provider.requests[1];
      if (!subagentReq) throw new Error('websearch executor subagent request missing');
      const last = subagentReq.messages?.[subagentReq.messages.length - 1];
      const lastText = typeof last?.content === 'string' ? last.content : JSON.stringify(last?.content);
      if (!lastText.includes('extract key technology stacks')) {
        throw new Error('what_to_summarize not in subagent prompt: ' + lastText);
      }
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-WEB-003 provider HTTP 500 → tool error',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({
        id: 'web-search-500',
        tool: 'web_search',
        args: {
          query: 'trigger_500',
          what_to_summarize: 'test',
          numResults: 1
        }
      });
      t.provider.expectText({ id: 'web-done-500', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'search the web for trigger_500');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const messages = (await t.client.messages(sid)).data || [];
      const part = findToolPart(messages, 'web_search');
      if (!part) throw new Error('web_search tool part not found in messages');
      if (part.state?.status !== 'completed') throw new Error(`expected completed tool status, got ${part.state?.status}`);
      const output = part.state?.output || '';
      if (!output.includes('failed') && !output.includes('500')) {
        throw new Error(`expected tool error containing 500/failed, got: ${output}`);
      }
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-WEB-004 malformed search JSON no host crash',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({
        id: 'web-search-malformed',
        tool: 'web_search',
        args: {
          query: 'trigger_malformed',
          what_to_summarize: 'test',
          numResults: 1
        }
      });
      t.provider.expectText({ id: 'web-done-malformed', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'search the web for trigger_malformed');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const messages = (await t.client.messages(sid)).data || [];
      const part = findToolPart(messages, 'web_search');
      if (!part) throw new Error('web_search tool part not found in messages');
      if (part.state?.status !== 'completed') throw new Error(`expected completed tool status, got ${part.state?.status}`);
      const output = part.state?.output || '';
      if (!output.includes('failed') && !output.includes('malformed')) {
        throw new Error(`expected failure representation in tool state or output, got: ${JSON.stringify(part.state)}`);
      }
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-WEB-005 webfetch returns fixture content',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({
        id: 'web-fetch-ok',
        tool: 'webfetch',
        args: {
          url: 'http://example.com/fixture'
        }
      });
      t.provider.expectText({ id: 'web-done-fetch', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'fetch http://example.com/fixture');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const messages = (await t.client.messages(sid)).data || [];
      const part = findToolPart(messages, 'webfetch');
      if (!part) throw new Error('webfetch tool part not found in messages');
      if (part.state?.status !== 'completed') throw new Error(`expected completed status, got ${part.state?.status}`);
      const output = part.state?.output || '';
      if (!output.includes('Example Domain') || !output.includes('This domain is for use in documentation examples.')) {
        throw new Error(`webfetch output missing fixture content: ${output}`);
      }
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-WEB-006 webfetch rejects loopback/private/link-local/metadata',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({
        id: 'web-fetch-private',
        tool: 'webfetch',
        args: {
          url: 'http://127.0.0.1/private'
        }
      });
      t.provider.expectText({ id: 'web-done-private', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'fetch http://127.0.0.1/private');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const messages = (await t.client.messages(sid)).data || [];
      const part = findToolPart(messages, 'webfetch');
      if (!part) throw new Error('webfetch tool part not found');
      if (part.state?.status !== 'completed') throw new Error(`expected completed status, got ${part.state?.status}`);
      const output = part.state?.output || '';
      if (!output.includes('Web fetch failed') || !output.includes('host not allowed')) {
        throw new Error(`expected host not allowed error in output, got: ${output}`);
      }
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-WEB-007 Redirect to private still rejected',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({
        id: 'web-fetch-redirect',
        tool: 'webfetch',
        args: {
          url: 'http://mock-redirector.com/to-private'
        }
      });
      t.provider.expectText({ id: 'web-done-redirect', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'fetch http://mock-redirector.com/to-private');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const messages = (await t.client.messages(sid)).data || [];
      const part = findToolPart(messages, 'webfetch');
      if (!part) throw new Error('webfetch tool part not found');
      if (part.state?.status !== 'completed') throw new Error(`expected completed status, got ${part.state?.status}`);
      const output = part.state?.output || '';
      if (!output.includes('Web fetch failed') || !output.includes('redirect to private IP blocked')) {
        throw new Error(`expected redirect to private IP blocked in output, got: ${output}`);
      }
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-WEB-008 OC-WEB-009 OC-WEB-010 OC-WEB-011 browser starts subagent and calls MCP',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);

      // 1. Parent calls browser
      t.provider.expectToolCall({
        id: 'parent-browser-call',
        tool: 'browser',
        args: { intent: 'check debug view' }
      });

      // 2. Child session starts, calls MCP tool
      t.provider.expectToolCall({
        id: 'child-mcp-call',
        tool: 'stealth-browser-mcp_get_debug_view',
        args: {}
      });

      // 3. Child session receives MCP result, returns final text
      t.provider.expectText({
        id: 'child-final-text',
        text: 'Successfully checked debug view: e2e stealth mcp debug view'
      });

      // 4. Parent session receives browser subagent result, returns done
      t.provider.expectText({
        id: 'parent-final-text',
        text: 'done'
      });

      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'start browser and check debug view');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      // Verifications:
      // OC-WEB-008: Check browser started by finding child requests.
      // We look for requests that have stealth-browser-mcp_get_debug_view tool in tools list
      // and do not have tool results in messages (which is childReq1).
      const childReq1 = t.provider.requests.find((r) => {
        const names = extractToolNames(r.tools);
        return names.includes('stealth-browser-mcp_get_debug_view') &&
               !r.messages?.some((m) => m.role === 'tool' || m.role === 'toolResult');
      });
      if (!childReq1) throw new Error('child session browser start request not found');

      // OC-WEB-009: Check MCP tools listed in the tools section of child request
      const tools = childReq1.tools || [];
      const toolNames = extractToolNames(tools);
      if (!toolNames.includes('stealth-browser-mcp_get_debug_view')) {
        throw new Error('stealth-browser-mcp_get_debug_view not in child session tools: ' + JSON.stringify(toolNames));
      }

      // OC-WEB-010: Check MCP tool result entered child session next LLM round.
      // childReq2 is the request that has stealth-browser-mcp_get_debug_view in tools list
      // and carries tool result in messages.
      const childReq2 = t.provider.requests.find((r) => {
        const names = extractToolNames(r.tools);
        return names.includes('stealth-browser-mcp_get_debug_view') &&
               r.messages?.some((m) => m.role === 'tool' || m.role === 'toolResult');
      });
      if (!childReq2) throw new Error('second child session completions request not found');
      const childReq2Str = JSON.stringify(childReq2);
      if (!childReq2Str.includes('e2e stealth mcp debug view')) {
        throw new Error('MCP result did not enter child session next LLM round: ' + childReq2Str);
      }

      // OC-WEB-011: Check browser final text returned to parent tool result
      const messages = (await t.client.messages(sid)).data || [];
      const part = findToolPart(messages, 'browser');
      if (!part) throw new Error('browser tool part not found in messages');
      if (part.state?.status !== 'completed') throw new Error(`expected browser status completed, got: ${part.state?.status}`);
      const output = part.state?.output || '';
      if (!output.includes('Successfully checked debug view: e2e stealth mcp debug view')) {
        throw new Error(`expected subagent output in tool result, got: ${output}`);
      }

      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-WEB-012 MCP process failure = child + resources cleaned',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);

      // 1. Parent calls browser
      t.provider.expectToolCall({
        id: 'parent-browser-fail',
        tool: 'browser',
        args: { intent: 'trigger failure' }
      });

      // 2. Child session starts, tries to call MCP tool
      t.provider.expectToolCall({
        id: 'child-mcp-fail-call',
        tool: 'stealth-browser-mcp_get_debug_view',
        args: {}
      });

      // 3. Child session receives MCP error, returns text and exits
      t.provider.expectText({
        id: 'child-final-fail-exit',
        text: 'Browser subagent failed due to MCP launch error'
      });

      // 4. Parent session receives browser subagent failure result, returns done
      t.provider.expectText({
        id: 'parent-final-fail-text',
        text: 'done'
      });

      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'run browser with failed mcp');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      // Verifications:
      // Verify that the child session tool call failed
      const childReq2 = t.provider.requests.find((r) => {
        const names = extractToolNames(r.tools);
        return names.includes('stealth-browser-mcp_get_debug_view') &&
               r.messages?.some((m) => m.role === 'tool' || m.role === 'toolResult');
      });
      if (!childReq2) throw new Error('second child session completions request not found');

      const childReq2Str = JSON.stringify(childReq2);
      if (!childReq2Str.includes('spawn failed') && !childReq2Str.includes('failed') && !childReq2Str.includes('error')) {
        throw new Error('MCP launch error did not enter child session next LLM round: ' + childReq2Str);
      }

      // Check that browser tool execution completed with status completed
      const messages = (await t.client.messages(sid)).data || [];
      const part = findToolPart(messages, 'browser');
      if (!part) throw new Error('browser tool part not found in messages');
      if (part.state?.status !== 'completed') {
        throw new Error(`expected browser tool status completed (with error inside), got: ${part.state?.status}`);
      }
      const output = part.state?.output || '';
      if (!output.includes('Browser subagent failed due to MCP launch error')) {
        throw new Error(`expected failure output in browser tool result, got: ${output}`);
      }

      // Find the childId from the subagent_spawned event
      const ndjsonLinesBefore = readNdjsonLines(t.host.workDir);
      const subagentSpawnEvent = ndjsonLinesBefore.find(
        (line) => line.Kind === 'subagent_spawned' && line.Session === sid
      );
      if (!subagentSpawnEvent) throw new Error('subagent_spawned event not found');
      const childId = subagentSpawnEvent.Payload?.childId;
      if (!childId) throw new Error('childId missing from subagent_spawned payload');

      // Delete the child session explicitly to trigger subsession physical close and cleanup hooks
      const delChildRes = await t.client.request('DELETE', `/session/${childId}`);
      console.log('DELETE child session result:', JSON.stringify(delChildRes));

      // Wait for the close event to appear in the NDJSON logs (up to 3 seconds)
      let closeEvents = [];
      const deadline = Date.now() + 3000;
      while (Date.now() < deadline) {
        const ndjsonLinesAfter = readNdjsonLines(t.host.workDir);
        closeEvents = ndjsonLinesAfter.filter((line) => {
          if (line.Kind !== 'subsession_decision_committed') return false;
          try {
            const evts = JSON.parse(line.Payload?.events || '[]');
            return evts.some((e) => e.Kind === 'subsession_physical_session_closed' && e.Payload?.sessionId === childId);
          } catch {
            return false;
          }
        });
        if (closeEvents.length > 0) break;
        await new Promise((resolve) => setTimeout(resolve, 100));
      }

      if (closeEvents.length === 0) {
        throw new Error('No subsession_physical_session_closed event found for child session');
      }

      // Also clean up the parent session
      await t.client.request('DELETE', `/session/${sid}`);

      expectNoSessionError(t, sid);
    },
  }
];

export default tests;
