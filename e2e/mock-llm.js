import http from 'node:http';

function sendSSE(res, chunks) {
  res.writeHead(200, {
    'Content-Type': 'text/event-stream',
    'Cache-Control': 'no-cache',
    'Connection': 'keep-alive',
    'X-Accel-Buffering': 'no',
  });
  for (const chunk of chunks) {
    res.write(`data: ${JSON.stringify(chunk)}\n\n`);
  }
  res.write('data: [DONE]\n\n');
  res.end();
}

function toolCallChunks(id, name, argsStr, promptTokens) {
  return [
    { id, object: 'chat.completion.chunk', created: 1, model: 'mock', choices: [{ index: 0, delta: { role: 'assistant', content: null }, finish_reason: null }] },
    { id, object: 'chat.completion.chunk', created: 1, model: 'mock', choices: [{ index: 0, delta: { tool_calls: [{ index: 0, id, type: 'function', function: { name, arguments: argsStr } }] }, finish_reason: null }] },
    { id, object: 'chat.completion.chunk', created: 1, model: 'mock', choices: [{ index: 0, delta: {}, finish_reason: 'tool_calls' }], usage: { prompt_tokens: promptTokens, completion_tokens: 100, total_tokens: promptTokens + 100 } },
  ];
}

function textChunks(id, text, promptTokens) {
  return [
    { id, object: 'chat.completion.chunk', created: 1, model: 'mock', choices: [{ index: 0, delta: { role: 'assistant', content: text }, finish_reason: null }] },
    { id, object: 'chat.completion.chunk', created: 1, model: 'mock', choices: [{ index: 0, delta: {}, finish_reason: 'stop' }], usage: { prompt_tokens: promptTokens, completion_tokens: 100, total_tokens: promptTokens + 100 } },
  ];
}

function estimatePromptTokens(body) {
  const json = JSON.stringify(body?.messages ?? []);
  return Math.max(1, Math.ceil(json.length / 2));
}

function toolNames(body) {
  const tools = body?.tools;
  if (Array.isArray(tools)) {
    return tools.flatMap((tool) => {
      const name = tool?.function?.name ?? tool?.name;
      return typeof name === 'string' ? [name] : [];
    });
  }
  if (tools && typeof tools === 'object') return Object.keys(tools);
  return [];
}

function hasToolResult(body) {
  return (body?.messages ?? []).some((message) => {
    if (message?.role === 'tool') return true;
    if (!Array.isArray(message?.content)) return false;
    return message.content.some((part) => part?.type === 'tool-result' || part?.type === 'tool');
  });
}

// opencode 内部在会话首条真实用户消息后，会异步向同一 provider/model 发起一次
// 「生成会话标题」的旁路 LLM 调用（见 packages/opencode/src/session/prompt.ts
// SessionPrompt.ensureTitle：messages 首项 content 固定前缀
// "Generate a title for this conversation:"）。该调用与测试用例的
// expectTool/expectText 队列无关，但会异步落在 mock 服务器上，若不识别会
// 窃取下一个测试回合 reset() 后的队列项，破坏 FIFO 语义（BUGS.md 未涉及此项，
// 纯 e2e mock 边界问题，不改动 Nudge/Fallback 内核）。
const TITLE_GENERATION_MARKER = 'Generate a title for this conversation:';

function isTitleGenerationRequest(body) {
  const messages = body?.messages ?? [];
  return messages.some((message) => {
    const content = message?.content;
    if (typeof content === 'string') return content.includes(TITLE_GENERATION_MARKER);
    if (Array.isArray(content)) {
      return content.some((part) => typeof part?.text === 'string' && part.text.includes(TITLE_GENERATION_MARKER));
    }
    return false;
  });
}

function handleTitleGeneration(res, url) {
  if (process.env.WANXIANG_E2E_DEBUG) {
    console.error('[MOCKLLM_DEBUG]', Date.now(), 'title-generation side-call bypassed, path=', url.pathname);
  }
  const id = `title_${Date.now()}_${Math.random().toString(36).slice(2, 8)}`;
  sendSSE(res, textChunks(id, 'E2E Test Session', 1));
}

function nextItemFor(body, queue) {
  const names = new Set(toolNames(body));
  if (names.has('return_reviewer') && queue.length === 0) {
    return {
      tool: 'return_reviewer',
      args: { verdict: 'accepted', feedback: 'everything looks perfectly clean' }
    };
  }
  if (names.size === 0 && !hasToolResult(body) && queue.length === 0) return undefined;
  if (queue[0]?.tool && names.size > 0 && !names.has(queue[0].tool)) {
    const toolIndex = queue.findIndex((item) => item?.tool && names.has(item.tool));
    if (toolIndex >= 0) return queue.splice(toolIndex, 1)[0];
  }
  if (queue.length > 0) return queue.shift();
  return undefined;
}

function handleExpect(req, res, queue) {
  let body = '';
  req.on('data', chunk => body += chunk);
  req.on('end', () => {
    try {
      queue.push(JSON.parse(body));
      res.writeHead(200, { 'Content-Type': 'application/json' });
      res.end('{"ok":true}');
    } catch {
      res.writeHead(400);
      res.end('{"error":"invalid json"}');
    }
  });
}

function handleWebSearch(req, res) {
  req.on('data', () => {});
  req.on('end', () => {
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({
      results: [
        { title: 'Test Search Title', url: 'http://example.com', content: 'Test search content for E2E.' },
      ],
    }));
  });
}

function handleWebFetch(req, res) {
  req.on('data', () => {});
  req.on('end', () => {
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({
      title: 'Example Domain',
      byline: 'IANA',
      length: 500,
      content: 'Example Domain\n\nThis domain is for use in documentation examples.',
    }));
  });
}

function handleChat(req, res, queue, calls, url) {
  if (process.env.WANXIANG_E2E_DEBUG) {
    console.error('[MOCKLLM_DEBUG]', Date.now(), 'chat endpoint hit, path=', url.pathname);
  }
  let rawBody = '';
  req.on('data', chunk => rawBody += chunk);
  req.on('end', () => {
    let parsed = {};
    try { parsed = JSON.parse(rawBody); } catch { /* keep {} */ }
    if (isTitleGenerationRequest(parsed)) {
      return handleTitleGeneration(res, url);
    }
    const messages = parsed.messages || [];
    const lastUser = [...messages].reverse().find(m => m.role === 'user');

    const call = {
      path: url.pathname,
      body: parsed,
      lastUserMessage: lastUser?.content ?? null,
    };

    const item = nextItemFor(parsed, queue);
    const id = `call_${Date.now()}_${Math.random().toString(36).slice(2, 8)}`;
    const promptTokens = estimatePromptTokens(parsed);

    if (item?.tool) {
      const args = {
        'follow-tdd-and-kolmogorov-principles': 'i-am-sure-i-have-followed-tdd-and-kolmogorov-principles-and-kept-todo-updated',
        'not-suitable-via-continue-tool': 'this-task-is-not-suitable-to-be-completed-via-continue-tool',
        'impossible-via-other-tools': 'it-is-not-possible-to-do-it-using-other-tools-and-only-run-tests-when-static-analysis-cannot-handle-it',
        ...(item.args ?? {})
      };
      if (item.args && item.args['follow-tdd-and-kolmogorov-principles'] === null) {
        delete args['follow-tdd-and-kolmogorov-principles'];
      }
      if (item.args && item.args['impossible-via-other-tools'] === null) {
        delete args['impossible-via-other-tools'];
      }
      if (item.args && item.args['not-suitable-via-continue-tool'] === null) {
        delete args['not-suitable-via-continue-tool'];
      }
      call.response = { type: 'tool_call', name: item.tool, args };
      sendSSE(res, toolCallChunks(id, item.tool, JSON.stringify(args), promptTokens));
    } else {
      const text = item?.text ?? 'ok';
      call.response = { type: 'text', text };
      sendSSE(res, textChunks(id, text, promptTokens));
    }
    calls.push(call);
    if (process.env.WANXIANG_E2E_DEBUG) {
      console.error('[MOCKLLM_DEBUG]', Date.now(), 'calls.push done, total calls=', calls.length);
    }
  });
}

function createMockLLM() {
  const _queue = [];
  const _calls = [];
  let _server = null;

  const handler = (req, res) => {
    const url = new URL(req.url, `http://${req.headers.host}`);
    if (url.pathname === '/_expect' && req.method === 'POST') {
      return handleExpect(req, res, _queue);
    }
    if ((url.pathname === '/v1/chat/completions' || url.pathname === '/v1/responses') && req.method === 'POST') {
      return handleChat(req, res, _queue, _calls, url);
    }
    if (url.pathname === '/api/web_search' && req.method === 'POST') {
      return handleWebSearch(req, res);
    }
    if (url.pathname === '/api/web_fetch' && req.method === 'POST') {
      return handleWebFetch(req, res);
    }
    res.writeHead(404);
    res.end('{"error":"not found"}');
  };

  const api = {
    start() {
      _server = http.createServer(handler);
      return new Promise(resolve => {
        _server.listen(0, '127.0.0.1', () => {
          const { port } = _server.address();
          resolve({
            url: `http://127.0.0.1:${port}`,
            expectTool: (tool, args) => _queue.push({ tool, args: args ?? {} }),
            expectText: (text) => _queue.push({ text }),
            reset: () => { _queue.length = 0; _calls.length = 0; },
            getRemainingExpectations: () => _queue.length,
            calls: _calls,
            stop: () => new Promise(fulfil => {
              if (!_server) return fulfil();
              _server.close(fulfil);
              _server.closeIdleConnections?.();
              _server.closeAllConnections?.();
            }),
          });
        });
      });
    },
  };

  return api;
}

export { createMockLLM };
