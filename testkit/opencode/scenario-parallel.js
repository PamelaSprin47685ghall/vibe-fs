import path from 'node:path';
import fs from 'node:fs';
import { ProcessHost } from './process-host.js';
import { EventProbe } from './event-probe.js';
import { FsOracle, HttpClient } from './scenario-http.js';
import { initGitWorkspace } from './process-host-utils.js';
import { resolvePluginPath } from './scenario-paths.js';
import { StrictMockProvider } from './strict-mock-provider.js';
import { createScenarioTurn } from './scenario-turn.js';
import { Watchdog } from './watchdog.js';

export class Scenario {
  constructor(ctx) {
    this.host = ctx.host;
    this.provider = ctx.provider;
    this.events = ctx.events;
    this.client = ctx.client;
    this.fs = ctx.fs;
    this.scenarioDir = ctx.scenarioDir;
    this.sessionIds = [];
    this.turn = createScenarioTurn(this);
    this.watchdog = null;
    this._tornDown = false;
  }

  async restart() {
    this.watchdog?.pet("restart-stop-host");
    await this.host.stop({ assert: true });
    this.watchdog?.pet("restart-close-events");
    await this.events.close();
    this.watchdog?.pet("restart-start-host");
    await this.host.start(this.host._startOpts);
    this.client._baseUrl = this.host.baseUrl;
    this.events._baseUrl = this.host.baseUrl;
    this.watchdog?.pet("restart-connect-events");
    await this.events.connect();
    this.watchdog?.pet("restart-complete");
  }
}

function configureProvider(provider, opts) {
  if (opts.strict !== undefined) provider.strict = !!opts.strict;
  return provider;
}

function ensureWorkspace(scenarioDir) {
  const workDir = path.join(scenarioDir, 'workspace');
  fs.mkdirSync(workDir, { recursive: true });
  return workDir;
}

async function prepareWorkspace(workDir, project) {
  if (project) {
    for (const [relPath, content] of Object.entries(project.files || {})) {
      const absPath = path.join(workDir, relPath);
      fs.mkdirSync(path.dirname(absPath), { recursive: true });
      fs.writeFileSync(absPath, content);
    }
  }
  await initGitWorkspace(workDir);
}

export async function setupScenarioParallel(opts, tmpDir) {
  const scenarioDir = tmpDir();
  const workDir = path.join(scenarioDir, 'workspace');
  fs.mkdirSync(workDir, { recursive: true });

  const provider = configureProvider(new StrictMockProvider(), opts);
  const host = new ProcessHost();
  const pluginPaths = opts.plugin !== false ? [resolvePluginPath(opts.variant || 'opencode')] : [];

  try {
    const t0 = Date.now();
    const providerUrl = await provider.start();
    const t1 = Date.now(); console.log(`[setupScenario] provider.start took ${t1 - t0}ms`);
    // Prepare workspace before starting opencode; it reads AGENTS.md at startup.
    await prepareWorkspace(workDir, opts.project);
    const t2 = Date.now(); console.log(`[setupScenario] prepareWorkspace took ${t2 - t1}ms`);
    await host.start({
      scenarioDir,
      providerUrl: `${providerUrl}/v1`,
      pluginPaths,
      contextLimit: opts.contextLimit,
      extraEnv: opts.extraEnv,
    });
    const t3 = Date.now(); console.log(`[setupScenario] host.start took ${t3 - t2}ms`);

    const client = new HttpClient(host.baseUrl, host.workDir);
    const events = new EventProbe(host.baseUrl, host.workDir);
    await events.connect();
    const t4 = Date.now(); console.log(`[setupScenario] events.connect took ${t4 - t3}ms`);

    const scenario = new Scenario({
      host,
      provider,
      events,
      client,
      fs: new FsOracle(host.workDir),
      scenarioDir,
    });
    client.onSessionCreated = (sid) => {
      if (!scenario.sessionIds.includes(sid)) scenario.sessionIds.push(sid);
    };

    const watchdogTimeout = opts.watchdogMs || 1000;
    const watchdog = new Watchdog({
      timeoutMs: watchdogTimeout,
      label: opts.watchdogLabel || "canary",
      onTimeout: async () => {
        console.error(`── watchdog event tail ──\n${events.dump(20)}`);
        try {
          await Promise.race([
            host.stop({ assert: false }),
            new Promise((r) => setTimeout(r, 3000)),
          ]);
        } catch (e) {
          console.error(`[Watchdog Cleanup Error] host.stop failed: ${e.message}`);
        }
        try { await provider.stop(); } catch {}
      },
    });
    scenario.watchdog = watchdog;
    events.onEvent((e) => watchdog.pet(`sse:${e.type}`));
    provider.onRequest = () => watchdog.pet("provider-request");
    client.onRequest = () => watchdog.pet("client-request");
    return scenario;
  } catch (err) {
    if (host.stdoutLog || host.stderrLog) {
      console.error('--- Setup Scenario Host Logs ---');
      if (host.stdoutLog) console.error(`stdout:\n${host.stdoutLog}`);
      if (host.stderrLog) console.error(`stderr:\n${host.stderrLog}`);
    }
    try { await host.stop({ assert: false }); } catch {}
    try { await provider.stop(); } catch {}
    throw err;
  }
}
