import { spawn, spawnSync } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import readline from 'node:readline';
import { createOmpHarness } from './omp-harness.js';
import { createMockLLM } from './mock-llm.js';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const OMP_E2E_LOCK = '/tmp/wanxiang-omp-e2e.lock';

function releaseLock() { try { fs.unlinkSync(OMP_E2E_LOCK); } catch {} }

function resolveBun() {
    return process.env.BUN ?? (() => {
        const r = spawnSync(process.platform === 'win32' ? 'where' : 'which', ['bun'], { encoding: 'utf8' });
        if (r.status === 0 && r.stdout) {
            const first = r.stdout.trim().split('\n')[0];
            if (first) return first;
        }
        throw new Error('bun not found. Install bun: https://bun.sh/docs/installation (or set BUN env var)');
    })();
}

export async function start(opts = {}) {
    try { fs.openSync(OMP_E2E_LOCK, 'wx'); }
    catch (e) {
        if (e.code === 'EEXIST') throw new Error('omp e2e already running (lock exists at ' + OMP_E2E_LOCK + ')');
        throw e;
    }
    const mockLlmInstance = createMockLLM();
    const mockLlm = await mockLlmInstance.start();
    const tempHome = fs.mkdtempSync(path.join(os.tmpdir(), 'omp-runner-home-'));
    const agentDir = path.join(tempHome, '.omp', 'agent');
    fs.mkdirSync(agentDir, { recursive: true });
    const configContent = `
model: openai/gpt-4o
`;
    const modelsContent = `
providers:
  openai:
    baseUrl: "${mockLlm.url}/v1"
    apiKey: "test-key"
    api: "openai-completions"
    models:
      - id: "gpt-4o"
        name: "GPT-4o"
        api: "openai-completions"
        contextWindow: 128000
`;
    fs.writeFileSync(path.join(agentDir, 'config.yml'), configContent, 'utf8');
    fs.writeFileSync(path.join(agentDir, 'models.yml'), modelsContent, 'utf8');

    function cleanupAll() {
        releaseLock();
        if (mockLlm) { mockLlm.stop().catch(() => {}); }
        try { fs.rmSync(tempHome, { recursive: true, force: true }); } catch {}
    }
    process.once('exit', cleanupAll);

    const ompRepo = process.env.WANXIANGSHU_OMP_REPO || path.resolve(__dirname, '..', '..', 'oh-my-pi');
    const driverPath = process.env.WANXIANGSHU_OMP_DRIVER || path.resolve(__dirname, 'omp-driver.ts');
    const pluginPath = path.resolve(__dirname, '..', 'build', 'src', 'Omp', 'Plugin.js');

    const child = spawn(resolveBun(), ['run', driverPath], {
        cwd: ompRepo,
        env: {
            ...process.env,
            WANXIANGSHU_PLUGIN_PATH: pluginPath,
            MOCK_LLM_URL: mockLlm.url,
            PI_CODING_AGENT_DIR: agentDir,
            OPENAI_API_KEY: 'test-key',
            OLLAMA_API_KEY: 'test-key',
            OMP_REVIEW_GRACE_INITIAL_MS: '600000',
            OMP_REVIEW_GRACE_SUBSEQUENT_MS: '600000',
            OMP_EXECUTOR_MIN_WAIT: '5'
        },
        stdio: ['pipe', 'pipe', 'pipe'],
        windowsHide: true,
    });
    child.stderr.on('data', (chunk) => process.stderr.write(`[omp-driver] ${chunk}`));

    // Single-line response dispatch: each driver line goes to the first waiting
    // resolver, or is queued if no command has been sent yet.
    const responseQueue = [];
    const waitingResolvers = [];
    const rl = readline.createInterface({ input: child.stdout, terminal: false });
    rl.on('line', (line) => {
        if (waitingResolvers.length > 0) waitingResolvers.shift()(line);
        else responseQueue.push(line);
    });

    function nextResponse() {
        if (responseQueue.length > 0) return Promise.resolve(responseQueue.shift());
        return new Promise((resolve) => waitingResolvers.push(resolve));
    }

    async function sendCommand(cmd) {
        child.stdin.write(JSON.stringify(cmd) + '\n');
        const line = await nextResponse();
        try { return JSON.parse(line); }
        catch (e) { throw new Error(`Failed to parse driver response: ${line}`); }
    }

    const readyLine = await nextResponse();
    if (readyLine.trim() !== 'ready') {
        child.kill('SIGKILL');
        cleanupAll();
        throw new Error('Expected ready signal from driver, got: ' + readyLine);
    }

    const harness = createOmpHarness({ sendCommand, child, releaseLock: cleanupAll, mockLlm });

    const handlersRes = await sendCommand({ type: 'getHandlers' });
    if (handlersRes.ok) {
        for (const key of handlersRes.data.handlerKeys) harness.handlers[key] = true;
    }

    return harness;
}

export default { start };
