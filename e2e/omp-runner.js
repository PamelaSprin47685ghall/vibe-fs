import { spawn, spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import readline from 'node:readline';
import { createOmpHarness } from './omp-harness.js';

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
    process.once('exit', releaseLock);

    const ompRepo = process.env.WANXIANGSHU_OMP_REPO || path.resolve(__dirname, '..', '..', 'oh-my-pi');
    const driverPath = process.env.WANXIANGSHU_OMP_DRIVER || path.resolve(__dirname, 'omp-driver.ts');
    const pluginPath = path.resolve(__dirname, '..', 'build', 'src', 'Omp', 'Plugin.js');

    const child = spawn(resolveBun(), ['run', driverPath], {
        cwd: ompRepo,
        env: { ...process.env, WANXIANGSHU_PLUGIN_PATH: pluginPath },
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
        throw new Error('Expected ready signal from driver, got: ' + readyLine);
    }

    const harness = createOmpHarness({ sendCommand, child, releaseLock });

    const handlersRes = await sendCommand({ type: 'getHandlers' });
    if (handlersRes.ok) {
        for (const key of handlersRes.data.handlerKeys) harness.handlers[key] = true;
    }

    return harness;
}

export default { start };
