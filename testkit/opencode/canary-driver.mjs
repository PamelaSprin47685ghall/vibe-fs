/**
 * canary-driver.mjs — one driver, one static TOML scenario per canary.
 *
 *   node canary-driver.mjs process-stress        (or `runCanary('process-stress')`)
 *
 * The scenario file says what the model replies and what order things must happen in; this
 * file turns the flow verbs into real Host calls. A request the scenario does not declare
 * fail-stops the run immediately (StrictMock `onFatal`).
 *
 * ── what K9 removed here ────────────────────────────────────────────────────
 *
 * `loadScripts` — swapping in more edges mid-run. §8 retires it: a scenario is one static
 * file and a restart does not change what the model would say. Measured while converting:
 * one of the two recovery files it loaded was byte-identical to edges the main file already
 * declared, and the other was `scripts: []` — an empty swap indistinguishable at runtime
 * from a meaningful one.
 *
 * `readScript` + `loadScripts` reading the same file twice. The scenario is compiled once
 * and the harness keys ride on the compiled object, so there is no second reader to
 * disagree with the first.
 */

import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import {
  runStaticGate,
  setupScenario,
  teardownScenario,
  getSessionId,
} from './index.js';
import { WATCHDOG_TIMEOUT_MS, WAIT_FACT_WINDOW_MS } from './time-budget.js';
import { bindLaneSession } from './tests/lane.mjs';
import { requestKindOf } from './strict-mock-matches.js';
import { compileScenario } from './scenario-schema.js';
import { ScenarioRuntime } from './scenario-runtime.js';

const SCENARIO_DIR = path.join(path.dirname(fileURLToPath(import.meta.url)), 'scripts');

/**
 * Compile the named scenario, or fail with every problem at once.
 *
 * A scenario that half-loads is a scenario whose author believes something is covered and
 * it is not — so there is no partial mode, and the problems are thrown rather than logged.
 */
function loadScenario(name) {
  const file = path.isAbsolute(name)
    ? name
    : path.join(SCENARIO_DIR, name.endsWith('.toml') ? name : `${name}.toml`);

  const result = compileScenario(fs.readFileSync(file, 'utf8'), { name: path.basename(file) });
  if (!result.ok) throw new Error(`scenario did not compile:\n  ${result.problems.join('\n  ')}`);
  return result.scenario;
}

function countFact(workDir, factName) {
  const common = execFileSync('git', ['-C', workDir, 'rev-parse', '--git-common-dir'], { encoding: 'utf8' }).trim();
  const runtimeDir = path.join(
    path.isAbsolute(common) ? common : path.resolve(workDir, common),
    'wanxiangshu-next',
    'runtimes',
  );
  if (!fs.existsSync(runtimeDir)) return 0;
  let count = 0;
  for (const file of fs.readdirSync(runtimeDir)) {
    if (!file.endsWith('.ndjson')) continue;
    for (const line of fs.readFileSync(path.join(runtimeDir, file), 'utf8').split('\n')) {
      if (line.includes(factName)) count += 1;
    }
  }
  return count;
}

async function sendPrompt(scenario, sessionId, prompt) {
  // Omit model when prompt.model is null/undefined so Host + plugin continue
  // from durable LastAuthority / Fallback Side (session does not own a model).
  // Explicit prompt.model always wins.
  const body = {
    agent: prompt.agent,
    parts: [{ type: 'text', text: prompt.text }],
  };
  if (prompt.model !== undefined && prompt.model !== null) {
    body.model = prompt.model;
  } else if (prompt.model === undefined && prompt.omitModel !== true) {
    // Back-compat: most scripts still expect a default model on the wire.
    body.model = { providerID: 'test', modelID: 'test-model' };
  }
  const response = await scenario.client.request('POST', `/session/${sessionId}/prompt_async`, { body });
  assert.ok(response.ok, `prompt failed: ${JSON.stringify(response.data)}`);
  return response;
}

async function runFlow(scenario, doc, ctx) {
  const flow = doc.flow || [];
  for (const step of flow) {
    if (step.wait) {
      await scenario.provider.waitForExpectation(step.wait, step.timeoutMs || WATCHDOG_TIMEOUT_MS);
      if (step.watchdog !== false) {
        scenario.watchdog?.advance({
          reason: step.wait,
          lane: step.lane || step.wait,
          blocking: step.blocking !== false,
        });
      }
      continue;
    }
    if (step.waitFact) {
      // Intermediate causal barrier on durable journal facts (e.g. OrchestratorPublished)
      // between LLM turns. Do NOT raise the single wait timeout: re-arm the 2s
      // scenario-local watchdog on every host event / poll slice so a multi-second
      // publish/ff chain is covered by intermediate progress, not a larger timeoutMs.
      const name = step.waitFact.name;
      const need = step.waitFact.eq !== undefined ? step.waitFact.eq : step.waitFact.gte !== undefined ? step.waitFact.gte : 1;
      const cmp = step.waitFact.eq !== undefined
        ? (n) => n === need
        : (n) => n >= need;
      // Overall span may exceed WATCHDOG_TIMEOUT_MS under parallel host load;
      // silence budget stays 2s via advance (not a raised expectation timeout).
      const overallMs = step.timeoutMs || WAIT_FACT_WINDOW_MS;
      const deadline = Date.now() + overallMs;
      let ok = cmp(countFact(scenario.host.workDir, name));
      while (!ok && Date.now() < deadline) {
        scenario.watchdog?.advance({
          reason: `wait-fact:${name}`,
          lane: step.lane || `fact:${name}`,
          blocking: true,
        });
        const remaining = Math.max(1, deadline - Date.now());
        // Slice well under the 2s silence budget so advance never races the watchdog.
        const slice = Math.min(remaining, 500);
        try {
          // Any host event re-enters the loop; fact may appear without a matching
          // provider request (ff / join completion is host-side).
          await scenario.events.awaitEvent(() => true, slice);
        } catch {
          // slice elapsed without events — re-check fact below
        }
        ok = cmp(countFact(scenario.host.workDir, name));
      }
      assert.ok(ok, `waitFact ${name} not satisfied (need ${step.waitFact.eq !== undefined ? 'eq' : 'gte'} ${need}, got ${countFact(scenario.host.workDir, name)})`);
      scenario.watchdog?.advance({
        reason: `fact-ready:${name}`,
        lane: step.lane || `fact:${name}`,
        blocking: true,
      });
      continue;
    }
    if (step.prompt) {
      const sid = step.session === 'child' ? ctx.childId
        : step.session === 'guard' ? (ctx.guardId || ctx.sessions?.guard)
        : step.session === 'nudge' ? (ctx.nudgeId || ctx.sessions?.nudge)
        : (step.session && ctx.sessions?.[step.session]) || ctx.sessionId;
      assert.ok(sid, 'prompt requires session');
      if (step.startTurn !== false) {
        ctx.turn = scenario.turn.start(sid, step.afterSeq ? { afterSeq: step.afterSeq } : undefined);
      }
      await sendPrompt(scenario, sid, step.prompt);
      continue;
    }
    if (step.restart) {
      await scenario.restart();
      continue;
    }
    if (step.awaitTerminal) {
      const sid = step.session === 'child' ? ctx.childId
        : step.session === 'guard' ? (ctx.guardId || ctx.sessions?.guard)
        : step.session === 'nudge' ? (ctx.nudgeId || ctx.sessions?.nudge)
        : (step.session && ctx.sessions?.[step.session]) || ctx.sessionId;
      const turn = ctx.turn || scenario.turn.start(sid);
      await turn.awaitTerminal({
        timeoutMs: step.timeoutMs || WATCHDOG_TIMEOUT_MS,
        requireActivity: step.requireActivity !== false,
        requireAssistantTerminal: step.requireAssistantTerminal === true,
        requireIdleAfterActivity: step.requireIdleAfterActivity !== false,
      });
      continue;
    }
    if (step.expectSatisfied) {
      scenario.provider.expectSatisfied();
      continue;
    }
    if (step.abort) {
      const sid = step.session === 'child' ? ctx.childId
        : step.session === 'guard' ? (ctx.guardId || ctx.sessions?.guard)
        : step.session === 'nudge' ? (ctx.nudgeId || ctx.sessions?.nudge)
        : (step.session && ctx.sessions?.[step.session]) || ctx.sessionId;
      await scenario.client.request('POST', `/session/${sid}/abort`, { body: {} }).catch(() => {});
      continue;
    }
    if (step.bindChild) {
      const parentId = ctx.sessionId;
      const event = await scenario.events.awaitEvent(
        (e) => e.type === 'session.created'
          && e.parentSessionID === parentId
          && (!step.bindChild.agent || e.sessionAgent === step.bindChild.agent),
        step.timeoutMs || WATCHDOG_TIMEOUT_MS,
      );
      ctx.childId = event.sessionID;
      assert.ok(ctx.childId, 'bindChild: missing child session');
      scenario.sessionIds.push(ctx.childId);
      // `bind` in TOML, `aliases` in the JSON scenarios K8 has not reached yet. The
      // fallback goes away with the JSON path in K9; `aliases` is already rejected by
      // `legacy-fields.js` at the TOML layer, so only the old files can reach it.
      const bound = step.bindChild.bind || step.bindChild.aliases || [step.bindChild.agent || 'child'];
      bindLaneSession(scenario.provider, ctx.childId, ...bound);
      scenario.watchdog?.advance({
        reason: 'child-created',
        lane: `session:${ctx.childId}`,
        blocking: true,
      });
      continue;
    }
    if (step.createSession) {
      const created = await scenario.client.createSession({ agent: step.createSession.agent });
      const sid = getSessionId(created);
      assert.ok(sid, `createSession failed: ${JSON.stringify(created)}`);
      scenario.sessionIds.push(sid);
      const aliases = step.createSession.bind || step.createSession.aliases || [step.createSession.agent];
      bindLaneSession(scenario.provider, sid, ...aliases);
      const key = step.createSession.as || step.createSession.agent || 'session';
      ctx.sessions = ctx.sessions || {};
      ctx.sessions[key] = sid;
      if (key === 'guard') ctx.guardId = sid;
      if (key === 'nudge') ctx.nudgeId = sid;
      scenario.watchdog?.advance({ reason: `create-session-${key}`, lane: `session:${sid}`, blocking: true });
      continue;
    }
    if (step.createChild) {
      const parentId = ctx.sessionId;
      assert.ok(parentId, 'createChild requires parent session');
      const child = await scenario.client.request('POST', '/api/session', {
        body: {
          parentID: parentId,
          title: step.createChild.title || 'canary child',
          agent: step.createChild.agent,
          model: step.createChild.model || { providerID: 'test', id: 'test-model' },
        },
      });
      ctx.childId = getSessionId(child);
      assert.ok(ctx.childId, `createChild failed: ${JSON.stringify(child)}`);
      scenario.sessionIds.push(ctx.childId);
      const aliases = step.createChild.bind || step.createChild.aliases || [step.createChild.agent];
      bindLaneSession(scenario.provider, ctx.childId, ...aliases);
      scenario.watchdog?.advance({
        reason: 'create-child',
        lane: `session:${ctx.childId}`,
        blocking: true,
      });
      continue;
    }
    if (step.afterExpectation) {
      scenario.provider.afterExpectation(step.afterExpectation.id, () => {
        if (step.afterExpectation.gitConflictProof) {
          const workDir = scenario.host.workDir;
          const proof = step.afterExpectation.file || 'publish_proof.txt';
          fs.writeFileSync(path.join(workDir, proof), 'Conflicting target edit\n');
          execFileSync('git', ['-C', workDir, 'add', proof], { encoding: 'utf8' });
          execFileSync('git', ['-C', workDir, 'commit', '-m', 'target: conflicting edit to proof file'], {
            encoding: 'utf8',
          });
        }
        if (step.afterExpectation.restart) {
          ctx.restartPromise = scenario.restart();
        }
      });
      continue;
    }
    if (step.awaitRestart) {
      assert.ok(ctx.restartPromise, 'awaitRestart without afterExpectation restart');
      await ctx.restartPromise;
      continue;
    }
    if (step.assertFacts) {
      const n = countFact(scenario.host.workDir, step.assertFacts.name);
      if (step.assertFacts.eq !== undefined) {
        assert.equal(n, step.assertFacts.eq, `${step.assertFacts.name} count`);
      }
      if (step.assertFacts.gte !== undefined) {
        assert.ok(n >= step.assertFacts.gte, `${step.assertFacts.name} expected >= ${step.assertFacts.gte}, got ${n}`);
      }
      continue;
    }
    if (step.assertActiveRequests) {
      const n = scenario.provider.activeRequestCount;
      if (step.assertActiveRequests.gte !== undefined) {
        assert.ok(n >= step.assertActiveRequests.gte, `activeRequestCount ${n}`);
      }
      continue;
    }
    if (step.awaitEvent) {
      const afterSeq = scenario.events.lastSeq;
      await scenario.events.awaitEvent((e) => {
        if (e.seq <= afterSeq) return false;
        if (step.awaitEvent.type && e.type !== step.awaitEvent.type) return false;
        if (step.awaitEvent.session === 'self' && e.sessionID !== ctx.sessionId) return false;
        if (step.awaitEvent.statusType) {
          return e.properties?.status?.type === step.awaitEvent.statusType;
        }
        return true;
      }, step.timeoutMs || WATCHDOG_TIMEOUT_MS);
      scenario.watchdog?.advance({
        reason: step.awaitEvent.reason || step.awaitEvent.type || 'event',
        lane: `session:${ctx.sessionId}`,
        blocking: true,
      });
      continue;
    }
    if (step.assertFile) {
      const full = path.join(scenario.host.workDir, step.assertFile.path);
      assert.ok(fs.existsSync(full), `missing file ${step.assertFile.path}`);
      const body = fs.readFileSync(full, 'utf8');
      if (step.assertFile.equals !== undefined) {
        assert.equal(body, step.assertFile.equals, `file content ${step.assertFile.path}`);
      }
      if (step.assertFile.includes !== undefined) {
        assert.ok(body.includes(step.assertFile.includes), `file ${step.assertFile.path} missing ${step.assertFile.includes}`);
      }
      continue;
    }
    if (step.assertGitLogContains) {
      const log = execFileSync('git', ['log', '--format=%s', 'HEAD'], {
        cwd: scenario.host.workDir,
        encoding: 'utf8',
      });
      assert.ok(log.includes(step.assertGitLogContains), `git log missing ${step.assertGitLogContains}: ${log}`);
      continue;
    }
    if (step.assertWorktreeClean) {
      const status = execFileSync('git', ['status', '--porcelain'], {
        cwd: scenario.host.workDir,
        encoding: 'utf8',
      }).trim();
      assert.equal(status, '', `worktree not clean: ${status}`);
      const worktrees = execFileSync('git', ['worktree', 'list', '--porcelain'], {
        cwd: scenario.host.workDir,
        encoding: 'utf8',
      });
      const extra = worktrees.split('\n').filter((line) =>
        line.startsWith('worktree ') && !line.includes(scenario.host.workDir));
      assert.equal(extra.length, 0, `extra worktrees remain: ${worktrees}`);
      continue;
    }
    if (step.assertModelTrajectory) {
      // FALLBACK-002's provider-visible A/A/B/B evidence, as an assertion rather than a
      // matching input. PROMPT-008 makes `AttemptExecutionProfile` the only source of the
      // effective model, so the model on the wire is a CONCLUSION of the run — a scenario
      // that matched on it would silently agree with whatever the cursor did.
      //
      // The lane is resolved through the session binding, so this counts only requests that
      // belong to the Logical Run under test. The old canary filtered by two hard-coded
      // prompt substrings instead, which also swept in any other session that happened to
      // send the same text.
      const claim = step.assertModelTrajectory;
      const sessionId = scenario.provider.sessionFor(claim.lane);
      assert.ok(sessionId, `assertModelTrajectory lane '${claim.lane}' is not bound`);

      const models = (scenario.provider.requests || [])
        .filter((request) => (request?.sessionID ?? request?.sessionId) === sessionId)
        .filter((request) => requestKindOf(request) === 'chat')
        .map((request) => {
          const model = request?.model;
          return typeof model === 'string' ? model : (model?.modelID ?? model?.id ?? null);
        });

      // Exact, and deliberately so. The old canary carried a
      // `rawModels.length === 5 → slice(1)` normalization to tolerate a duplicated first
      // attempt — assertion weakening of the kind VERIFY-002 forbids. If the Host ever does
      // deliver that duplicate, this must fail and be explained.
      assert.deepEqual(models, claim.models, `model trajectory for lane ${claim.lane}`);
      ctx.modelTrajectory = models;
      continue;
    }
    if (step.assertPtyEcho) {
      const results = [];
      for (const request of scenario.provider.requests || []) {
        for (const message of request.messages || []) {
          if (message.role === 'tool' || message.role === 'toolResult') {
            const content = typeof message.content === 'string' ? message.content : JSON.stringify(message.content || '');
            try { results.push(JSON.parse(content)); } catch { results.push({ raw: content }); }
          }
        }
      }
      const readResult = results.find((r) => typeof r.output === 'string' && r.output.includes('ECHO_TEST'));
      assert.ok(readResult, `read must return ECHO_TEST: ${JSON.stringify(results)}`);
      assert.ok(readResult.output.includes('CWD='), `read must surface cwd: ${readResult.output}`);
      const isClosed = (outcome) =>
        outcome === 'closed'
        || (typeof outcome === 'string' && outcome.includes('closed'))
        || (Array.isArray(outcome) && outcome.includes('closed'));
      assert.ok(results.some((r) => isClosed(r.outcome)), `join must deliver closed: ${JSON.stringify(results)}`);
      const listResult = results.find((r) => Array.isArray(r));
      assert.ok(listResult, `list must return array: ${JSON.stringify(results)}`);
      assert.ok(!listResult.some((e) => e && e.kind === 'pty'), `leaked pty: ${JSON.stringify(listResult)}`);
      continue;
    }
    if (step.custom) {
      const fn = ctx.customs?.[step.custom];
      assert.ok(typeof fn === 'function', `unknown custom step ${step.custom}`);
      await fn(scenario, ctx, step);
      continue;
    }
    throw new Error(`unknown flow step: ${JSON.stringify(step)}`);
  }
}

export async function runCanary(scriptName, { customs } = {}) {
  const doc = loadScenario(scriptName);
  let scenario;
  const ctx = { customs: customs || {} };
  try {
    scenario = await setupScenario({
      project: doc.setup?.project || { files: {} },
      strict: doc.setup?.strict !== false,
      extraEnv: doc.setup?.env || {},
      watchdogLabel: doc.setup?.watchdogLabel || doc.name,
    });

    scenario.provider.attachScenario(new ScenarioRuntime(doc));

    const agent = doc.session?.agent || doc.prompt?.agent;
    if (agent || doc.session) {
      const created = await scenario.client.createSession({ agent: doc.session?.agent });
      ctx.sessionId = getSessionId(created);
      assert.ok(ctx.sessionId, `session creation failed: ${JSON.stringify(created)}`);
      scenario.sessionIds.push(ctx.sessionId);
      const bind = doc.session?.bind || [agent];
      if (bind?.length) bindLaneSession(scenario.provider, ctx.sessionId, ...bind);
    }

    if (doc.prompt) {
      ctx.turn = scenario.turn.start(ctx.sessionId);
      await sendPrompt(scenario, ctx.sessionId, doc.prompt);
    }

    await runFlow(scenario, doc, ctx);

    console.log(doc.pass ?? `${doc.name} canary passed.`);

    await teardownScenario(scenario);
    return 0;
  } catch (error) {
    console.error(`${doc.name} canary failed: ${error.stack || error}`);
    if (scenario?.provider?.unexpectedRequests?.length) {
      console.error(JSON.stringify(scenario.provider.unexpectedRequests.slice(0, 3)));
    }
    if (process.env.CANARY_DEBUG_FACTS === '1' && scenario?.host?.workDir) {
      const common = execFileSync('git', ['-C', scenario.host.workDir, 'rev-parse', '--git-common-dir'], { encoding: 'utf8' }).trim();
      const runtimeDir = path.join(path.isAbsolute(common) ? common : path.resolve(scenario.host.workDir, common), 'wanxiangshu-next', 'runtimes');
      if (fs.existsSync(runtimeDir)) {
        for (const file of fs.readdirSync(runtimeDir).filter(name => name.endsWith('.ndjson'))) {
          console.error(`[canary-facts] ${file}`);
          console.error(fs.readFileSync(path.join(runtimeDir, file), 'utf8'));
        }
      }
    }
    if (scenario) {
      try { await teardownScenario(scenario, { keepOnFailure: true }); } catch {}
    }
    return 1;
  }
}

// CLI: node canary-driver.mjs process-stress
const isMain = process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url);
if (isMain) {
  const script = process.argv[2];
  if (!script) {
    console.error('usage: node canary-driver.mjs <scenario>');
    process.exit(2);
  }
  process.exit(await runCanary(script));
}
