import http from 'node:http';

function createMockLLM() {
  const _queue = [];
  const _calls = [];
  let _server = null;

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

  function toolCallChunks(id, name, argsStr) {
    return [
      { id, object: 'chat.completion.chunk', created: 1, model: 'mock', choices: [{ index: 0, delta: { role: 'assistant', content: null }, finish_reason: null }] },
      { id, object: 'chat.completion.chunk', created: 1, model: 'mock', choices: [{ index: 0, delta: { tool_calls: [{ index: 0, id, type: 'function', function: { name, arguments: argsStr } }] }, finish_reason: null }] },
      { id, object: 'chat.completion.chunk', created: 1, model: 'mock', choices: [{ index: 0, delta: {}, finish_reason: 'tool_calls' }] },
    ];
  }

  function textChunks(id, text) {
    return [
      { id, object: 'chat.completion.chunk', created: 1, model: 'mock', choices: [{ index: 0, delta: { role: 'assistant', content: text }, finish_reason: null }] },
      { id, object: 'chat.completion.chunk', created: 1, model: 'mock', choices: [{ index: 0, delta: {}, finish_reason: 'stop' }] },
    ];
  }

  const handler = (req, res) => {
    const url = new URL(req.url, `http://${req.headers.host}`);

    if (url.pathname === '/_expect' && req.method === 'POST') {
      let body = '';
      req.on('data', chunk => body += chunk);
      req.on('end', () => {
        try {
          _queue.push(JSON.parse(body));
          res.writeHead(200, { 'Content-Type': 'application/json' });
          res.end('{"ok":true}');
        } catch {
          res.writeHead(400);
          res.end('{"error":"invalid json"}');
        }
      });
      return;
    }

    if (url.pathname === '/v1/chat/completions' && req.method === 'POST') {
      let body = '';
      req.on('data', chunk => body += chunk);
      req.on('end', () => {
        let messages = [];
        try { messages = JSON.parse(body).messages || []; } catch { /* keep [] */ }
        const lastUser = [...messages].reverse().find(m => m.role === 'user');

        const call = {
          path: url.pathname,
          body: JSON.parse(body),
          lastUserMessage: lastUser?.content ?? null,
        };

        const item = _queue.shift();
        const id = `call_${Date.now()}_${Math.random().toString(36).slice(2, 8)}`;

        if (item?.tool) {
          call.response = { type: 'tool_call', name: item.tool, args: item.args ?? {} };
          sendSSE(res, toolCallChunks(id, item.tool, JSON.stringify(item.args ?? {})));
        } else {
          const text = item?.text ?? 'ok';
          call.response = { type: 'text', text };
          sendSSE(res, textChunks(id, text));
        }
        _calls.push(call);
      });
      return;
    }

    res.writeHead(404);
    res.end('{"error":"not found"}');
  };

  const api = {
    start() {
      _server = http.createServer(handler);
      return new Promise(resolve => {
        _server.listen(0, () => {
          const { port } = _server.address();
          resolve({
            url: `http://127.0.0.1:${port}`,
            expectTool: (tool, args) => _queue.push({ tool, args: args ?? {} }),
            expectText: (text) => _queue.push({ text }),
            reset: () => { _queue.length = 0; _calls.length = 0; },
            calls: _calls,
            stop: () => new Promise(fulfil => _server.close(fulfil)),
          });
        });
      });
    },
  };

  return api;
}

export { createMockLLM };
