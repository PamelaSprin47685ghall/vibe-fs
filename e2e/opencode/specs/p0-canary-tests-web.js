/**
 * p0-canary-tests-web.js — E2E tests for Web search, Web fetch, and Browser agent.
 */

import { getSessionId } from '../../../testkit/opencode/scenario.js';
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
      t.provider.expectToolCall({ id: 'web-search-002', tool: 'web_search', args: { query: 'E2E websearch test', what_to_summarize: 'extract key technology stacks', numResults: 1 } });
      t.provider.expectText({ id: 'web-summary-002', text: 'Test search content.' });
      t.provider.expectText({ id: 'web-done-002', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'search the web for E2E websearch test');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const subagentReq = t.provider.requests[1];
      if (!subagentReq) throw new Error('subagent request missing');
      const last = subagentReq.messages?.[subagentReq.messages.length - 1];
      const lastText = typeof last?.content === 'string' ? last.content : JSON.stringify(last?.content);
      if (!lastText.includes('extract key technology stacks')) throw new Error('what_to_summarize missing from prompt');
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-WEB-003 provider HTTP 500 → tool error',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({ id: 'web-search-500', tool: 'web_search', args: { query: 'trigger_500', what_to_summarize: 'test', numResults: 1 } });
      t.provider.expectText({ id: 'web-done-500', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'search the web for trigger_500');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const messages = (await t.client.messages(sid)).data || [];
      const part = findToolPart(messages, 'web_search');
      if (!part || part.state?.status !== 'completed') throw new Error('completed status missing');
      const output = part.state?.output || '';
      if (!output.includes('failed') && !output.includes('500')) throw new Error('error output missing');
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-WEB-004 malformed search JSON no host crash',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({ id: 'web-search-malformed', tool: 'web_search', args: { query: 'trigger_malformed', what_to_summarize: 'test', numResults: 1 } });
      t.provider.expectText({ id: 'web-done-malformed', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'search the web for trigger_malformed');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const messages = (await t.client.messages(sid)).data || [];
      const part = findToolPart(messages, 'web_search');
      if (!part || part.state?.status !== 'completed') throw new Error('status missing');
      const output = part.state?.output || '';
      if (!output.includes('failed') && !output.includes('malformed')) throw new Error('failure text missing');
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-WEB-005 webfetch returns fixture content',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({ id: 'web-fetch-ok', tool: 'webfetch', args: { url: 'http://example.com/fixture' } });
      t.provider.expectText({ id: 'web-done-fetch', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'fetch http://example.com/fixture');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const messages = (await t.client.messages(sid)).data || [];
      const part = findToolPart(messages, 'webfetch');
      if (!part || part.state?.status !== 'completed') throw new Error('status missing');
      const output = part.state?.output || '';
      if (!output.includes('Example Domain') || !output.includes('This domain is for use')) throw new Error('content missing');
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-WEB-006 webfetch rejects loopback/private/link-local/metadata',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({ id: 'web-fetch-private', tool: 'webfetch', args: { url: 'http://127.0.0.1/private' } });
      t.provider.expectText({ id: 'web-done-private', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'fetch http://127.0.0.1/private');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const messages = (await t.client.messages(sid)).data || [];
      const part = findToolPart(messages, 'webfetch');
      if (!part || part.state?.status !== 'completed') throw new Error('status missing');
      const output = part.state?.output || '';
      if (!output.includes('Web fetch failed') || !output.includes('host not allowed')) throw new Error('SSRF block missing');
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-WEB-007 Redirect to private still rejected',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({ id: 'web-fetch-redirect', tool: 'webfetch', args: { url: 'http://mock-redirector.com/to-private' } });
      t.provider.expectText({ id: 'web-done-redirect', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'fetch http://mock-redirector.com/to-private');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const messages = (await t.client.messages(sid)).data || [];
      const part = findToolPart(messages, 'webfetch');
      if (!part || part.state?.status !== 'completed') throw new Error('status missing');
      const output = part.state?.output || '';
      if (!output.includes('Web fetch failed') || !output.includes('redirect to private IP blocked')) throw new Error('redirect SSRF block missing');
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-WEB-008 OC-WEB-009 OC-WEB-010 OC-WEB-011 browser starts subagent and calls MCP',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({ id: 'parent-browser-call', tool: 'browser', args: { intent: 'check debug view' } });
      t.provider.expectToolCall({ id: 'child-mcp-call', tool: 'stealth-browser-mcp_get_debug_view', args: {} });
      t.provider.expectText({ id: 'child-final-text', text: 'Successfully checked debug view: e2e stealth mcp debug view' });
      t.provider.expectText({ id: 'parent-final-text', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'start browser and check debug view');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const childReq1 = t.provider.requests.find(r => {
        const names = extractToolNames(r.tools);
        return names.includes('stealth-browser-mcp_get_debug_view') &&
               !r.messages?.some(m => m.role === 'tool' || m.role === 'toolResult');
      });
      if (!childReq1) throw new Error('child start req missing');
      if (!extractToolNames(childReq1.tools).includes('stealth-browser-mcp_get_debug_view')) throw new Error('MCP tool missing in tools');

      const childReq2 = t.provider.requests.find(r => {
        const names = extractToolNames(r.tools);
        return names.includes('stealth-browser-mcp_get_debug_view') &&
               r.messages?.some(m => m.role === 'tool' || m.role === 'toolResult');
      });
      if (!childReq2 || !JSON.stringify(childReq2).includes('e2e stealth mcp debug view')) throw new Error('MCP result missing from LLM round');

      const messages = (await t.client.messages(sid)).data || [];
      const part = findToolPart(messages, 'browser');
      if (!part || part.state?.status !== 'completed') throw new Error('status missing');
      const output = part.state?.output || '';
      if (!output.includes('Successfully checked debug view')) throw new Error('output missing debug view');
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-WEB-012 MCP process failure = child + resources cleaned',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({ id: 'parent-browser-fail', tool: 'browser', args: { intent: 'trigger failure' } });
      t.provider.expectToolCall({ id: 'child-mcp-fail-call', tool: 'stealth-browser-mcp_get_debug_view', args: {} });
      t.provider.expectText({ id: 'child-final-fail-exit', text: 'Browser subagent failed due to MCP launch error' });
      t.provider.expectText({ id: 'parent-final-fail-text', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'run browser with failed mcp');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const childReq2 = t.provider.requests.find(r => {
        const names = extractToolNames(r.tools);
        return names.includes('stealth-browser-mcp_get_debug_view') &&
               r.messages?.some(m => m.role === 'tool' || m.role === 'toolResult');
      });
      if (!childReq2) throw new Error('child req missing');
      const childReq2Str = JSON.stringify(childReq2);
      if (!childReq2Str.includes('spawn failed') && !childReq2Str.includes('failed') && !childReq2Str.includes('error')) throw new Error('MCP error missing');

      const messages = (await t.client.messages(sid)).data || [];
      const part = findToolPart(messages, 'browser');
      if (!part || part.state?.status !== 'completed') throw new Error('status missing');
      const output = part.state?.output || '';
      if (!output.includes('Browser subagent failed due to MCP launch error')) throw new Error('output missing error text');

      const ndjsonLinesBefore = readNdjsonLines(t.host.workDir);
      const subagentSpawnEvent = ndjsonLinesBefore.find(l => l.Kind === 'subagent_spawned' && l.Session === sid);
      const childId = subagentSpawnEvent?.Payload?.childId;
      if (!childId) throw new Error('childId missing');

      await t.client.request('DELETE', `/session/${childId}`);
      let closeEvents = [];
      const deadline = Date.now() + 3000;
      while (Date.now() < deadline) {
        const ndjsonLinesAfter = readNdjsonLines(t.host.workDir);
        closeEvents = ndjsonLinesAfter.filter(line => {
          if (line.Kind !== 'subsession_decision_committed') return false;
          try {
            const evts = JSON.parse(line.Payload?.events || '[]');
            return evts.some(e => e.Kind === 'subsession_physical_session_closed' && e.Payload?.sessionId === childId);
          } catch { return false; }
        });
        if (closeEvents.length > 0) break;
        await new Promise(r => setTimeout(r, 100));
      }
      if (closeEvents.length === 0) throw new Error('No close event found');
      await t.client.request('DELETE', `/session/${sid}`);
      expectNoSessionError(t, sid);
    },
  }
];

export default tests;
