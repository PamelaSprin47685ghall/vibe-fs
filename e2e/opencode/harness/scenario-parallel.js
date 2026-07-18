/**
 * scenario-parallel.js — Parallel setup helpers.
 *
 * Setup has three independent branches that can run concurrently:
 *   1. Mock provider start (HTTP server, ~10ms).
 *   2. Scenario dir + git init + project files (sync, <50ms).
 *   3. ProcessHost start (opencode spawn + wait-for-listening + health).
 *
 * Branches (1) and (2)/(3) can fully overlap. The host branch (3)
 * internally needs the provider URL — we therefore pre-resolve the
 * provider URL first (it does not depend on the host), then start the
 * host in parallel with the remaining project work.
 *
 * Wall-clock saving: provider + project overlap with the host spawn
 * (~900ms+ cold start). Total per-test setup drops from ~1100ms to
 * ~900ms.
 *
 * Kept in its own module so scenario.js stays under the 200-line
 * Kolmogorov line budget.
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

export async function setupScenarioParallel(opts, tmpDir) {
  const scenarioDir = tmpDir();
  const workDir = path.join(scenarioDir, 'workspace');
  fs.mkdirSync(workDir, { recursive: true });

  const provider = configureProvider(new StrictMockProvider(), opts);
  const host = new ProcessHost();
  const pluginPaths = opts.plugin !== false ? [resolvePluginPath(opts.variant || 'opencode')] : [];

  const providerUrl = await provider.start();
  const projectP = opts.project
    ? writeProjectFiles(workDir, opts.project).then(() => initGitWorkspace(workDir))
    : initGitWorkspace(workDir);
  const hostP = host.start({
    scenarioDir,
    providerUrl: `${providerUrl}/v1`,
    pluginPaths,
    contextLimit: opts.contextLimit,
  });
  await Promise.all([projectP, hostP]);

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
}
