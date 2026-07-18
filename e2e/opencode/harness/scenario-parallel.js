/**
 * scenario-parallel.js — Parallel setup helpers.
 *
 * Setup has two independent branches that can run concurrently:
 *   1. Mock provider start (HTTP server, ~10ms).
 *   2. Scenario dir + project files + git init + AGENTS.md.
 *
 * The host branch (3) must start after the workspace is fully prepared,
 * because `opencode serve` reads AGENTS.md/git state at startup and
 * concurrent `git init`/`git commit` calls from the old parallel layout
 * caused intermittent `fetch failed` setup errors.
 *
 * Kept in its own module so scenario.js stays under the 200-line budget.
 */

import fs from 'node:fs';
import path from 'node:path';
import { StrictMockProvider } from './strict-mock-provider.js';
import { ProcessHost } from './process-host.js';
import { EventProbe } from './event-probe.js';
import { FsOracle, HttpClient } from './scenario-http.js';
import { initGitWorkspace } from './process-host-utils.js';
import { resolvePluginPath } from './scenario-paths.js';
import { createScenarioTurn } from './scenario-turn.js';

export class Scenario {
  constructor(ctx) {
    this.host = ctx.host;
    this.provider = ctx.provider;
    this.events = ctx.events;
    this.client = ctx.client;
    this.fs = ctx.fs;
    this.scenarioDir = ctx.scenarioDir;
    this.sessionIds = [];
    this._tornDown = false;
    this.turn = createScenarioTurn(this);
  }
}

export function configureProvider(provider, opts) {
  if (opts.strict) provider.strict = true;
  if (opts.allowSynthetic) provider.allowSyntheticContinuations();
  if (opts.allowTitleGen) provider.allowTitleGeneration();
  return provider;
}

async function writeProjectFiles(workDir, project) {
  for (const [file, content] of Object.entries(project)) {
    const abs = path.join(workDir, file);
    fs.mkdirSync(path.dirname(abs), { recursive: true });
    fs.writeFileSync(abs, content);
  }
}

async function prepareWorkspace(workDir, project) {
  if (project) {
    await writeProjectFiles(workDir, project);
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
    const providerUrl = await provider.start();
    // Prepare workspace before starting opencode; it reads AGENTS.md at startup.
    await prepareWorkspace(workDir, opts.project);
    await host.start({
      scenarioDir,
      providerUrl: `${providerUrl}/v1`,
      pluginPaths,
      contextLimit: opts.contextLimit,
    });

    const client = new HttpClient(host.baseUrl, host.workDir);
    const events = new EventProbe(host.baseUrl, host.workDir);
    await events.connect();

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
    return scenario;
  } catch (err) {
    if (host.stdoutLog || host.stderrLog) {
      console.error(`\n── setup failed host logs ──`);
      if (host.stdoutLog) console.error(host.stdoutLog.slice(-2000));
      if (host.stderrLog) console.error(host.stderrLog.slice(-3000));
    }
    try { await host.stop({ assert: false }); } catch {}
    try { await provider.stop(); } catch {}
    throw err;
  }
}
