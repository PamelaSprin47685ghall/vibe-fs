namespace Wanxiangshu.Infrastructure

open System
open System.Threading.Tasks
open Fable.Core

/// AGENT-027: one-shot stdio MCP tools/call. No SDK.
module SembleMcpStdio =

    let private driverSource =
        """
(async function(command, args, toolName, toolArgs, timeoutMs) {
  const { spawn } = await import('node:child_process');
  return await new Promise((resolve, reject) => {
    const child = spawn(command, args, { stdio: ['pipe', 'pipe', 'pipe'] });
    let buf = '';
    let nextId = 1;
    const pending = new Map();
    let settled = false;
    const fail = (err) => {
      if (settled) return;
      settled = true;
      try { child.kill(); } catch (_) {}
      reject(err instanceof Error ? err : new Error(String(err)));
    };
    const timer = setTimeout(() => fail(new Error('semble mcp timeout')), timeoutMs || 15000);
    const finish = (value) => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      try { child.kill(); } catch (_) {}
      resolve(value);
    };
    child.on('error', fail);
    child.on('exit', (code) => { if (!settled) fail(new Error('semble mcp exited ' + String(code))); });
    child.stderr.on('data', () => {});
    child.stdout.setEncoding('utf8');
    child.stdout.on('data', (chunk) => {
      buf += chunk;
      let idx;
      while ((idx = buf.indexOf('\n')) !== -1) {
        const line = buf.slice(0, idx).trim();
        buf = buf.slice(idx + 1);
        if (!line) continue;
        let msg;
        try { msg = JSON.parse(line); } catch (_) { continue; }
        if (msg.id != null && pending.has(msg.id)) {
          const done = pending.get(msg.id);
          pending.delete(msg.id);
          done(msg);
        }
      }
    });
    const rpc = (method, params) => new Promise((res, rej) => {
      const id = nextId++;
      pending.set(id, (msg) => {
        if (msg.error) rej(new Error(msg.error.message || JSON.stringify(msg.error)));
        else res(msg.result);
      });
      try {
        child.stdin.write(JSON.stringify({ jsonrpc: '2.0', id: id, method: method, params: params }) + '\n');
      } catch (err) {
        pending.delete(id);
        rej(err);
      }
    });
    rpc('initialize', {
      protocolVersion: '2024-11-05',
      capabilities: {},
      clientInfo: { name: 'wanxiangshu-semble', version: '0.1.0' }
    }).then(() => {
      try {
        child.stdin.write(JSON.stringify({ jsonrpc: '2.0', method: 'notifications/initialized' }) + '\n');
      } catch (err) { return fail(err); }
      return rpc('tools/call', { name: toolName, arguments: toolArgs }).then(finish, fail);
    }).catch(fail);
  });
})
"""

    [<Emit("(0, eval)($0)($1, $2, $3, $4, $5)")>]
    let private callRaw
        (source: string)
        (command: string)
        (args: string array)
        (toolName: string)
        (toolArgs: obj)
        (timeoutMs: int)
        : JS.Promise<obj> =
        jsNative

    [<Emit("$0.then((value) => $1(value)).catch((err) => $2(String(err && err.message || err)))")>]
    let private runPromise (p: JS.Promise<obj>) (onOk: obj -> unit) (onErr: string -> unit) : unit = jsNative

    let callTool
        (command: string)
        (args: string array)
        (toolName: string)
        (toolArgs: obj)
        (timeoutMs: int)
        : Task<obj option> =
        let tcs = TaskCompletionSource<obj option>()

        runPromise
            (callRaw driverSource command args toolName toolArgs timeoutMs)
            (fun value -> tcs.SetResult(if isNull value then None else Some value))
            (fun _ -> tcs.SetResult None)

        tcs.Task
