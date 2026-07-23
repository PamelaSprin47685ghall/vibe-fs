/**
 * p0-canary-tests-fuzzy-executor.js — E2E tests for fuzzy search and executor gap.
 * Covers OC-FUZZY-002/004/005/006/007/008 and OC-EXEC-003/004/005/006.
 */

import { getSessionId } from '../../../testkit/opencode/scenario.js';
import { content, writeWorkFile, findToolPart, TIMEOUTS } from './p0-canary-utils.js';
import YAML from 'yaml';

export function parseToolOutput(text) {
  if (!text || !text.startsWith('---\n')) {
    return { metadata: {}, body: text || '' };
  }
  let endIdx = text.indexOf('\n---\n', 4);
  let fenceLength = 5;
  if (endIdx === -1) {
    if (text.endsWith('\n---')) {
      endIdx = text.length - 4;
      fenceLength = 4;
    }
  }
  if (endIdx === -1) {
    return { metadata: {}, body: text };
  }
  const yamlText = text.slice(4, endIdx);
  const body = text.slice(endIdx + fenceLength);
  try {
    const metadata = YAML.parse(yamlText) || {};
    return { metadata, body };
  } catch (e) {
    return { metadata: {}, body };
  }
}

function expectNoSessionError(t, sid) {
  t.events.expectCount({ type: 'session.error', sessionID: sid, count: 0 });
}

const tests = [
  {
    name: 'OC-FUZZY-002 No match returns explicit empty result',
    fn: async (t) => {
      // Given: A session is created, and we setup expectations on the provider
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({
        id: 'fuzzy-find-no-match',
        tool: 'fuzzy_find',
        args: { pattern: ['nonexistent_file_abc_123'] }
      });
      t.provider.expectToolCall({
        id: 'fuzzy-grep-no-match',
        tool: 'fuzzy_grep',
        args: { pattern: ['nonexistent_pattern_abc_123'] }
      });
      t.provider.expectText({ id: 'no-match-done', text: 'done' });

      // When: The agent prompts to search for nonexistent file and pattern
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'find files named nonexistent_file_abc_123 and search for nonexistent_pattern_abc_123');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      // Then: The tool results must contain the explicit empty result text
      const messages = (await t.client.messages(sid)).data || [];
      
      const findPart = findToolPart(messages, 'fuzzy_find');
      if (!findPart) throw new Error('fuzzy_find tool part not found');
      if (findPart.state?.status !== 'completed') throw new Error(`fuzzy_find state: ${findPart.state?.status}`);
      const findParsed = parseToolOutput(findPart.state.output);
      if (!findParsed.body.includes('No matching files found')) {
        throw new Error(`Expected output to contain "No matching files found", got: ${findPart.state.output}`);
      }

      const grepPart = findToolPart(messages, 'fuzzy_grep');
      if (!grepPart) throw new Error('fuzzy_grep tool part not found');
      if (grepPart.state?.status !== 'completed') throw new Error(`fuzzy_grep state: ${grepPart.state?.status}`);
      const grepParsed = parseToolOutput(grepPart.state.output);
      if (!grepParsed.body.includes('No matches found')) {
        throw new Error(`Expected output to contain "No matches found", got: ${grepPart.state.output}`);
      }

      // Cleanup: Verify no session errors
      expectNoSessionError(t, sid);
    }
  },

  {
    name: 'OC-FUZZY-004 Multi-pattern returns chunked results',
    fn: async (t) => {
      // Given: Create files with distinct patterns (pre-created)
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({
        id: 'fuzzy-grep-multi',
        tool: 'fuzzy_grep',
        args: { pattern: ['content_pat_x', 'content_pat_y'] }
      });
      t.provider.expectToolCall({
        id: 'fuzzy-find-multi',
        tool: 'fuzzy_find',
        args: { pattern: ['multi_pat_file_a', 'multi_pat_file_b'] }
      });
      t.provider.expectText({ id: 'multi-pat-done', text: 'done' });

      // When: The agent prompts to search and find multiple patterns
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'grep for content_pat_x and content_pat_y, and find files named multi_pat_file_a and multi_pat_file_b');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      // Then: Results must contain chunked outputs separated by pattern headers
      const messages = (await t.client.messages(sid)).data || [];

      const grepPart = findToolPart(messages, 'fuzzy_grep');
      if (!grepPart) throw new Error('fuzzy_grep tool part not found');
      if (grepPart.state?.status !== 'completed') throw new Error(`fuzzy_grep state: ${grepPart.state?.status}`);
      const grepOutput = grepPart.state.output;
      if (!grepOutput.includes('## pattern: "content_pat_x"')) {
        throw new Error(`fuzzy_grep output missing header for content_pat_x: ${grepOutput}`);
      }
      if (!grepOutput.includes('## pattern: "content_pat_y"')) {
        throw new Error(`fuzzy_grep output missing header for content_pat_y: ${grepOutput}`);
      }

      const findPart = findToolPart(messages, 'fuzzy_find');
      if (!findPart) throw new Error('fuzzy_find tool part not found');
      if (findPart.state?.status !== 'completed') throw new Error(`fuzzy_find state: ${findPart.state?.status}`);
      const findOutput = findPart.state.output;
      if (!findOutput.includes('## pattern: "multi_pat_file_a"')) {
        throw new Error(`fuzzy_find output missing header for multi_pat_file_a: ${findOutput}`);
      }
      if (!findOutput.includes('## pattern: "multi_pat_file_b"')) {
        throw new Error(`fuzzy_find output missing header for multi_pat_file_b: ${findOutput}`);
      }

      // Cleanup: Verify no session errors
      expectNoSessionError(t, sid);
    }
  },

  {
    name: 'OC-FUZZY-005 fuzzy_continue gets next page, no duplicates',
    fn: async (t) => {
      // Given: Create 4 files to trigger paging (pre-created)
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);

      let myIterator = null;

      t.provider.expectToolCall({
        id: 'find-page-1',
        tool: 'fuzzy_find',
        args: { pattern: ['page_file_'], limit: 1 }
      });
      t.provider.expectText({ id: 'page-1-done', text: 'continue' });

      // When: Query page 1
      const turn1 = await t.turn.start(sid);
      await t.client.prompt(sid, 'find files named page_file_ with limit 1');
      await turn1.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      // Retrieve iterator and verify page 1 results
      const messages1 = (await t.client.messages(sid)).data || [];
      const part1 = findToolPart(messages1, 'fuzzy_find');
      if (!part1) throw new Error('fuzzy_find part 1 missing');
      const parsed1 = parseToolOutput(part1.state.output);
      myIterator = parsed1.metadata.iterator;
      if (!myIterator) throw new Error(`Page 1 did not return iterator: ${part1.state.output}`);

      const page1Files = parsed1.body.split('\n').filter(line => line.includes('page_file_'));
      if (page1Files.length !== 1) {
        throw new Error(`Expected exactly 1 file on page 1, got: ${JSON.stringify(page1Files)} | body was: ${JSON.stringify(parsed1.body)}`);
      }

      // Now set expectation for page 2 using the iterator
      t.provider.expectToolCall({
        id: 'find-page-2',
        tool: 'fuzzy_continue',
        args: () => ({ iterator: myIterator })
      });
      t.provider.expectText({ id: 'page-2-done', text: 'done' });

      // When: Query page 2
      const turn2 = await t.turn.start(sid);
      await t.client.prompt(sid, 'continue finding next page');
      await turn2.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      // Then: Verify page 2 has the remaining files and no duplicates
      const messages2 = (await t.client.messages(sid)).data || [];
      const part2 = findToolPart(messages2, 'fuzzy_continue');
      if (!part2) throw new Error('fuzzy_continue part 2 missing');
      const parsed2 = parseToolOutput(part2.state.output);

      const page2Files = parsed2.body.split('\n').filter(line => line.includes('page_file_'));
      if (page2Files.length !== 1) {
        throw new Error(`Expected exactly 1 file on page 2, got: ${JSON.stringify(page2Files)} | body was: ${JSON.stringify(parsed2.body)}`);
      }

      // Verify no duplicates
      const page1Set = new Set(page1Files.map(f => f.trim()));
      for (const f of page2Files) {
        if (page1Set.has(f.trim())) {
          throw new Error(`Duplicate file found across pages: ${f} | page1: ${JSON.stringify(page1Files)} | page2: ${JSON.stringify(page2Files)}`);
        }
      }

      // Cleanup: Verify no session errors
      expectNoSessionError(t, sid);
    }
  },

  {
    name: 'OC-FUZZY-006 Iterator exhausted returns completion state',
    fn: async (t) => {
      // Given: Create 3 files to trigger pagination (pre-created)
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);

      let myIterator = null;

      t.provider.expectToolCall({
        id: 'find-exhaust-1',
        tool: 'fuzzy_find',
        args: { pattern: ['exhaust_file_'], limit: 2 }
      });
      t.provider.expectText({ id: 'exhaust-1-done', text: 'continue' });

      // When: Query page 1
      const turn1 = await t.turn.start(sid);
      await t.client.prompt(sid, 'find files named exhaust_file_ with limit 2');
      await turn1.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const messages1 = (await t.client.messages(sid)).data || [];
      const part1 = findToolPart(messages1, 'fuzzy_find');
      const parsed1 = parseToolOutput(part1.state.output);
      myIterator = parsed1.metadata.iterator;
      if (!myIterator) throw new Error('Expected iterator on page 1');

      // Setup expectation for page 2
      t.provider.expectToolCall({
        id: 'find-exhaust-2',
        tool: 'fuzzy_continue',
        args: () => ({ iterator: myIterator })
      });
      t.provider.expectText({ id: 'exhaust-2-done', text: 'done' });

      // When: Query page 2
      const turn2 = await t.turn.start(sid);
      await t.client.prompt(sid, 'continue finding next page');
      await turn2.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      // Then: Page 2 should have the final file and NO iterator
      const messages2 = (await t.client.messages(sid)).data || [];
      const part2 = findToolPart(messages2, 'fuzzy_continue');
      if (!part2) throw new Error('fuzzy_continue part 2 missing');
      const parsed2 = parseToolOutput(part2.state.output);

      if (parsed2.metadata.iterator) {
        throw new Error(`Expected iterator to be exhausted (undefined), but got: ${parsed2.metadata.iterator}`);
      }

      // Cleanup: Verify no session errors
      expectNoSessionError(t, sid);
    }
  },

  {
    name: 'OC-FUZZY-007 Wrong iterator ID does not read other session data',
    fn: async (t) => {
      // Given: Create Session B
      const sessB = await t.client.createSession();
      const sidB = getSessionId(sessB);

      t.provider.expectToolCall({
        id: 'find-wrong-iterator',
        tool: 'fuzzy_continue',
        args: { iterator: 'wrong_session:ffi_f:999' }
      });
      t.provider.expectText({ id: 'wrong-iterator-done', text: 'done' });

      // When: Call fuzzy_continue with a fake iterator ID
      const turn = await t.turn.start(sidB);
      await t.client.prompt(sidB, 'continue finding with iterator wrong_session:ffi_f:999');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      // Then: The response must return a clear error, not file contents
      const messages = (await t.client.messages(sidB)).data || [];
      const part = findToolPart(messages, 'fuzzy_continue');
      if (!part) throw new Error('fuzzy_continue tool part not found');
      
      const parsed = parseToolOutput(part.state.output);
      if (!parsed.body.includes('iterator error: unknown, expired, or already consumed iterator')) {
        throw new Error(`Expected output to contain unknown/expired iterator error, got: ${JSON.stringify(part.state.output)}`);
      }

      // Cleanup: Verify no session errors
      expectNoSessionError(t, sidB);
    }
  },

  {
    name: 'OC-FUZZY-008 Session deleted cleans up iterator',
    fn: async (t) => {
      // Given: Create Session A, get an iterator (pre-created files)
      const sessA = await t.client.createSession();
      const sidA = getSessionId(sessA);

      let myIterator = null;

      t.provider.expectToolCall({
        id: 'find-cleanup-1',
        tool: 'fuzzy_find',
        args: { pattern: ['cleanup_file_'], limit: 2 }
      });
      t.provider.expectText({ id: 'cleanup-1-done', text: 'continue' });

      const turn1 = await t.turn.start(sidA);
      await t.client.prompt(sidA, 'find files named cleanup_file_ with limit 2');
      await turn1.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const messages1 = (await t.client.messages(sidA)).data || [];
      const part1 = findToolPart(messages1, 'fuzzy_find');
      const parsed1 = parseToolOutput(part1.state.output);
      myIterator = parsed1.metadata.iterator;
      if (!myIterator) throw new Error('Expected iterator on first search');

      // When: Delete Session A
      const deleteRes = await t.client.request('DELETE', `/session/${sidA}`);
      if (!deleteRes.ok) throw new Error(`DELETE /session/${sidA} failed: ${deleteRes.status}`);

      // Create Session B
      const sessB = await t.client.createSession();
      const sidB = getSessionId(sessB);

      // Setup expectation to try continuing with deleted Session A's iterator
      t.provider.expectToolCall({
        id: 'find-cleanup-2',
        tool: 'fuzzy_continue',
        args: () => ({ iterator: myIterator })
      });
      t.provider.expectText({ id: 'cleanup-2-done', text: 'done' });

      // When: Attempt to use the deleted session's iterator
      const turn2 = await t.turn.start(sidB);
      await t.client.prompt(sidB, 'continue finding with the iterator from the deleted session');
      await turn2.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      // Then: The call must fail with unknown iterator error
      const messages2 = (await t.client.messages(sidB)).data || [];
      const part2 = findToolPart(messages2, 'fuzzy_continue');
      if (!part2) throw new Error('fuzzy_continue tool part not found');
      
      const parsed2 = parseToolOutput(part2.state.output);
      if (!parsed2.body.includes('iterator error: unknown, expired, or already consumed iterator')) {
        throw new Error(`Expected output to indicate unknown/expired iterator, got: ${parsed2.state.output}`);
      }

      // Cleanup: Verify no session errors
      expectNoSessionError(t, sidB);
    }
  },

  {
    name: 'OC-EXEC-003 Non-zero exit code structured return',
    fn: async (t) => {
      // Given: Create session and set expectations
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({
        id: 'exec-exit-code',
        tool: 'executor',
        args: {
          language: 'shell',
          command: 'exit 42',
          timeout_type: 'short',
          mode: 'ro',
          what_to_summarize: 'exit command',
          max_bytes: 1000
        }
      });
      t.provider.expectText({ id: 'exit-code-done', text: 'done' });

      // When: Run command
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'run exit 42');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      // Then: Result must be structured with status and exit_code
      const messages = (await t.client.messages(sid)).data || [];
      const part = findToolPart(messages, 'executor');
      if (!part) throw new Error('executor tool part not found');
      
      const parsed = parseToolOutput(part.state.output);
      if (parsed.metadata.status !== 'exit_error') {
        throw new Error(`Expected status to be "exit_error", got: ${parsed.metadata.status} inside metadata: ${JSON.stringify(parsed.metadata)} | raw output: ${JSON.stringify(part.state.output)}`);
      }
      if (parsed.metadata.exit_code !== 42) {
        throw new Error(`Expected exit_code to be 42, got: ${parsed.metadata.exit_code}`);
      }

      // Cleanup: Verify no session errors
      expectNoSessionError(t, sid);
    }
  },

  {
    name: 'OC-EXEC-004 stderr visible, not conflated with success',
    fn: async (t) => {
      // Given: Create session and set expectations
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({
        id: 'exec-stderr',
        tool: 'executor',
        args: {
          language: 'shell',
          command: 'echo "my stderr msg" >&2; exit 3',
          timeout_type: 'short',
          mode: 'ro',
          what_to_summarize: 'stderr command',
          max_bytes: 1000
        }
      });
      t.provider.expectText({ id: 'stderr-done', text: 'done' });

      // When: Run command
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'run stderr command');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      // Then: Stderr must be in output body, and exit status must indicate error
      const messages = (await t.client.messages(sid)).data || [];
      const part = findToolPart(messages, 'executor');
      if (!part) throw new Error('executor tool part not found');

      const parsed = parseToolOutput(part.state.output);
      if (!parsed.body.includes('my stderr msg')) {
        throw new Error(`Expected body to contain "my stderr msg", got: ${parsed.body}`);
      }
      if (parsed.metadata.status !== 'exit_error') {
        throw new Error(`Expected status to be "exit_error", got: ${parsed.metadata.status}`);
      }
      if (parsed.metadata.exit_code !== 3) {
        throw new Error(`Expected exit_code to be 3, got: ${parsed.metadata.exit_code}`);
      }

      // Cleanup: Verify no session errors
      expectNoSessionError(t, sid);
    }
  },

  {
    name: 'OC-EXEC-005 cwd points to target workspace',
    fn: async (t) => {
      // Given: Write marker file in workspace (pre-created)
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({
        id: 'exec-cwd',
        tool: 'executor',
        args: {
          language: 'shell',
          command: 'cat work_cwd_marker.txt',
          timeout_type: 'short',
          mode: 'ro',
          what_to_summarize: 'cwd command',
          max_bytes: 1000
        }
      });
      t.provider.expectText({ id: 'cwd-done', text: 'done' });

      // When: Run command
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'run cat work_cwd_marker.txt');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      // Then: Output must contain the file contents from the workspace
      const messages = (await t.client.messages(sid)).data || [];
      const part = findToolPart(messages, 'executor');
      if (!part) throw new Error('executor tool part not found');

      const parsed = parseToolOutput(part.state.output);
      if (!parsed.body.includes('cwd-correct')) {
        throw new Error(`Expected body to contain "cwd-correct", got: ${parsed.body}`);
      }

      // Cleanup: Verify no session errors
      expectNoSessionError(t, sid);
    }
  },

  {
    name: 'OC-EXEC-006 max_bytes truncation works',
    fn: async (t) => {
      // Given: Create session and set expectations
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({
        id: 'exec-max-bytes',
        tool: 'executor',
        args: {
          language: 'shell',
          command: 'echo "hello-world-truncation"',
          timeout_type: 'short',
          mode: 'ro',
          what_to_summarize: 'long output',
          max_bytes: 10
        }
      });
      // The summarizer subagent request expectations
      t.provider.expectText({
        id: 'exec-summary-response',
        text: 'summary: hello'
      });
      t.provider.expectText({ id: 'max-bytes-done', text: 'done' });

      // When: Run command
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'run long output command');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      // Then: The subagent request must have received the full original output
      const subagentReq = t.provider.requests[1];
      if (!subagentReq) throw new Error('Summarizer subagent request missing');
      const subMsg = subagentReq.messages?.[subagentReq.messages.length - 1];
      const subText = typeof subMsg?.content === 'string' ? subMsg.content : JSON.stringify(subMsg?.content);
      if (!subText.includes('hello-world-truncation')) {
        throw new Error(`Subagent request did not receive original output: ${subText}`);
      }

      // Verify the final tool output body is the summary returned by the subagent
      const messages = (await t.client.messages(sid)).data || [];
      const part = findToolPart(messages, 'executor');
      if (!part) throw new Error('executor tool part not found');

      const parsed = parseToolOutput(part.state.output);
      if (!parsed.body.includes('summary: hello')) {
        throw new Error(`Expected tool output to contain subagent summary, got: ${parsed.body}`);
      }

      // Cleanup: Verify no session errors
      expectNoSessionError(t, sid);
    }
  }
];

export default tests;
