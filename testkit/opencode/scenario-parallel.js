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
import { WATCHDOG_TIMEOUT_MS } from './watchdog-constants.js';

export class Scenario {
  constructor(ctx) {
    this.host = ctx.host;
    this.provider = ctx.provider;
    this.events = ctx.events;
    this.client = ctx.client;
    this.fs = ctx.fs;
    this.scenarioDir = ctx.scenarioDir;
    this.sessionIds = [];
    this.sessionCreatedDiagnostics = [];
    this.turn = createScenarioTurn(this);
    this.watchdog = null;
    this._tornDown = false;
  }

  async restart() {
    this.watchdog?.advance({ reason: 'restart-stop-host', lane: 'runtime', blocking: true });
    await this.host.stop({ assert: true });
    this.watchdog?.advance({ reason: 'restart-close-events', lane: 'runtime', blocking: true });
    await this.events.close();
    this.watchdog?.advance({ reason: 'restart-start-host', lane: 'runtime', blocking: true });
    await this.host.start({
      ...this.host._startOpts,
      onProgress: (stage) => this.watchdog?.advance({
        reason: `restart-host-${stage}`,
        lane: 'runtime',
        blocking: true,
      }),
    });
    this.client._baseUrl = this.host.baseUrl;
    this.events._baseUrl = this.host.baseUrl;
    // New host process: prior provider-visible seals are not comparable across restart.
    this.provider?.clearPrefixSeals?.();
    this.watchdog?.advance({ reason: 'restart-connect-events', lane: 'runtime', blocking: true });
    await this.events.connect();
    this.watchdog?.advance({ reason: 'restart-complete', lane: 'runtime', blocking: true });
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
    const t1 = Date.now();
    if (process.env.CANARY_VERBOSE || process.env.DEBUG) {
      console.log(`[setupScenario] provider.start took ${t1 - t0}ms`);
    }
    // Prepare workspace before starting opencode; it reads AGENTS.md at startup.
    await prepareWorkspace(workDir, opts.project);
    const t2 = Date.now();
    if (process.env.CANARY_VERBOSE || process.env.DEBUG) {
      console.log(`[setupScenario] prepareWorkspace took ${t2 - t1}ms`);
    }
    await host.start({
      scenarioDir,
      providerUrl: `${providerUrl}/v1`,
      pluginPaths,
      contextLimit: opts.contextLimit,
      extraEnv: {
        // Prefer the scenario's explicit contextLimit so canaries that shrink
        // the model budget also shrink Y's self-rebase threshold. A global
        // process env must not keep the production 32k default and prevent
        // threshold-driven self-rebase in small fixtures.
        WANXIANGSHU_BLOGGER_CONTEXT_LIMIT:
          opts.contextLimit != null
            ? String(opts.contextLimit)
            : (process.env.WANXIANGSHU_BLOGGER_CONTEXT_LIMIT || '32000'),
        // Every companion-enabled canary needs a concrete cheap-model identity;
        // production still fails closed when operators omit this setting.
        WANXIANGSHU_BLOGGER_MODEL: process.env.WANXIANGSHU_BLOGGER_MODEL || 'test/test-model',
        ...(opts.extraEnv || {}),
      },
    });
    const t3 = Date.now();
    if (process.env.CANARY_VERBOSE || process.env.DEBUG) {
      console.log(`[setupScenario] host.start took ${t3 - t2}ms`);
    }

    const client = new HttpClient(host.baseUrl, host.workDir);
    const events = new EventProbe(host.baseUrl, host.workDir);
    await events.connect();
    const t4 = Date.now();
    if (process.env.CANARY_VERBOSE || process.env.DEBUG) {
      console.log(`[setupScenario] events.connect took ${t4 - t3}ms`);
    }
    console.log('[setupScenario] ready');

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
      scenario.sessionCreatedDiagnostics.push({
        sessionID: sid,
        observedAt: Date.now(),
        causal: false,
      });
    };

    const watchdogTimeout = opts.watchdogMs || WATCHDOG_TIMEOUT_MS;
    const watchdog = new Watchdog({
      timeoutMs: watchdogTimeout,
      label: opts.watchdogLabel || "canary",
      onTimeout: async () => {
        console.error(`── watchdog event tail ──\n${events.dump(20)}`);
        console.error('── watchdog blocked expectations ──');
        for (const expectation of provider.blockedExpectations) {
          console.error(`  ${expectation.blocking ? 'blocking' : 'background'} ${expectation.id} ${expectation.lane}`);
        }
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
    // First mock script mismatch: stop the entire canary process (not just 500).
    provider.onFatal = (fatal) => {
      try {
        console.error(`── fatal script mismatch: ${fatal.reason} ──`);
        console.error(`session=${fatal.sessionId} lastUser=${JSON.stringify(fatal.lastUser)}`);
        console.error(`candidates=${JSON.stringify(fatal.candidates)}`);
        console.error(`── event tail ──\n${events.dump(20)}`);
        for (const expectation of provider.blockedExpectations) {
          console.error(`  pending ${expectation.blocking ? 'blocking' : 'background'} ${expectation.id} ${expectation.lane}`);
        }
      } catch {}
      try { watchdog.stop(); } catch {}
      process.exit(1);
    };
    provider.onExpectationConsumed = ({ id, lane, blocking }) => {
      watchdog.advance({
        reason: `expectation:${id}`,
        lane: `${lane.scenario}/${lane.session}/${lane.role}/turn-${lane.turn}`,
        expectationId: id,
        blocking,
      });
    };
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
