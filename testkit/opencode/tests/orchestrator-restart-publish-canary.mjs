/**
 * orchestrator-restart-publish-canary.mjs
 *
 * Crash-recovery E2E for the real Orchestrator ManagerJob publish chain
 * (fork manager -> coder writes proof file -> double-PERFECT review ->
 * candidate commit -> rebase -> ff-only publish -> worktree+branch cleanup)
 * driven entirely on the StrictMockProvider + real git, with two deterministic
 * crash points, each recovered by `scenario.restart()` re-running the idempotent
 * chain (A7 durable barriers: PreRebaseReviewConfirmed / CandidateRegistered /
 * Rebased / ConflictDetected / PostRebaseReviewConfirmed / PublishClaimed /
 * Published).
 *
 * Both crash points share ONE scenario shape (mirrors orchestrator-publish-canary.mjs)
 * so lanes stay deterministic; recovery is triggered by `scenario.restart()` which
 * re-runs the chain from the durable journal — completed stages self-skip.
 *
 * Crash point A (after-candidate): restart after the candidate checkpoint (candidate
 *   commit already exists) but before the chain finishes. Recovery must re-run,
 *   self-skip CandidateRegistered, rebase, post-rebase review, publish exactly once.
 *
 * Crash point B (rebase-conflict): engineer a target-branch conflict before rebase;
 *   the first rebase hits conflicts and resumes the manager with [CONFLICT RESUMPTION].
 *   Restart mid-resolution (REBASE_HEAD still present); recovery re-delivers
 *   [CONFLICT RESUMPTION], the manager resumes, the rebase converges, publish once.
 *
 * Lane discipline (StrictMockProvider): per (scenario,session,role,requestKind) key,
 * expectations form a FIFO queue consumed in turn order (turn must be sequential, no
 * gaps). Child lanes bind via parentSession alias (orchestrator -> manager -> coder ->
 * reviewer), cascading session identity on first consumption. `afterExpectation` fires
 * the causal restart the instant the checkpoint lane is consumed (watchdog-only, no
 * fixed sleeps).
 */

import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { runStaticGate, setupScenario, teardownScenario, getSessionId } from '../index.js';
import { WATCHDOG_TIMEOUT_MS } from '../watchdog-constants.js';
import { bindLaneSession, expectationLane } from './lane.mjs';
import { requestSessionOf, requestRoleOf, requestParentSessionOf } from '../strict-mock-matches.js';

const __filename = fileURLToPath(import.meta.url);
const SCENARIO = 'orch-restart-publish';
const ORCH_PROMPT = 'Ship publish_proof.txt to the target branch.';
const CODER_PROMPT = 'Write publish_proof.txt.';
const PROOF_CONTENT = 'Published by orchestrator canary\n';
const PROOF_FILE = 'publish_proof.txt';

// Count durable facts of a given tag in the runtime journal (git-common-dir based).
// Fable encodes DU cases as ["Tag", payload] where payload is often an object, so a
// string-only recursive search misses the tag. Substring match on the serialized
// line is the same contract fallback-canary uses for FallbackFailureRecorded.
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
    const fullPath = path.join(runtimeDir, file);
    if (!fs.statSync(fullPath).isFile()) continue;
    for (const line of fs.readFileSync(fullPath, 'utf8').split('\n')) {
      if (!line.trim()) continue;
      if (line.includes(factName)) count += 1;
    }
  }
  return count;
}

/**
 * Register the full lane set for one crash-point scenario.
 *
 * Turn layout (per lane key, sequential):
 *  - orchestrator (alias 'orchestrator'): 1 fork, 2 join(initial), 3 join(recovery), 4 final(recovery)
 *  - manager (alias 'manager'):           1 fork-coder, 2 join-coder, 3 terminal;
 *                                         + 4 conflict-resume-1, 5 conflict-resume-2  (only when conflict)
 *  - coder (alias 'coder'):               1 write, 2 terminal
 *  - reviewer (alias 'reviewer'):         1..8 = perfect-1..4 / terminal-1..4
 *                                         (initial run consumes 1..4, recovery 5..8)
 *  - bloggers (initial + post-restart, separate aliases so turns reset to 1):
 *      orchestrator-blogger / -final, manager-blogger, coder-blogger  (initial)
 *      orchestrator-blogger-restarted / -final-restarted, manager-blogger-restarted,
 *      coder-blogger-restarted  (post-restart re-anchor)
 */
function registerLanes(provider, scenario, { conflict }) {
  const L = (session, role, turn, parent, requestKind = 'chat') =>
    expectationLane(scenario, session, role, turn, requestKind, parent);

  // --- orchestrator (root session, 'orchestrator' alias bound to its session id) ---
  provider.expectTitle({
    id: 'orch-title',
    lane: expectationLane(scenario, 'title', 'title', 1, 'title'),
  });
  provider.expectToolCall({
    id: 'orch-fork-manager',
    lane: L('orchestrator', 'orchestrator', 1),
    tool: 'fork',
    args: { agent: 'manager', prompt: ORCH_PROMPT },
    match: { requiredTools: ['fork', 'join'], forbiddenTools: ['list'] },
  });
  provider.expectToolCall({
    id: 'orch-join',
    lane: L('orchestrator', 'orchestrator', 2),
    tool: 'join',
    args: {},
    match: { requiredTools: ['fork', 'join'], forbiddenTools: ['list'] },
  });
  // Recovery re-issues join (turn 3) after restart; the durable completion drains.
  provider.expectToolCall({
    id: 'orch-join-2',
    lane: L('orchestrator', 'orchestrator', 3),
    tool: 'join',
    args: {},
    match: { requiredTools: ['fork', 'join'], forbiddenTools: ['list'] },
  });
  provider.expectText({
    id: 'orch-final',
    lane: L('orchestrator', 'orchestrator', 4),
    text: 'Publish completed.',
    match: { requiredTools: ['fork', 'join'], forbiddenTools: ['list'] },
  });

  // --- manager (child of orchestrator) ---
  provider.expectToolCall({
    id: 'manager-fork-coder',
    lane: L('manager', 'manager', 1, 'orchestrator'),
    tool: 'fork',
    args: { agent: 'coder', prompt: CODER_PROMPT },
    match: { requiredTools: ['fork', 'join', 'list'] },
  });
  provider.expectToolCall({
    id: 'manager-join-coder',
    lane: L('manager', 'manager', 2, 'orchestrator'),
    tool: 'join',
    args: {},
    match: { requiredTools: ['fork', 'join', 'list'] },
  });
  provider.expectText({
    id: 'manager-terminal',
    lane: L('manager', 'manager', 3, 'orchestrator'),
    text: 'Manager finished.',
    match: { requiredTools: ['fork', 'join', 'list'] },
  });
  if (conflict) {
    // Both deliveries of [CONFLICT RESUMPTION] target the SAME (resumed) manager
    // session, so they queue on the manager lane as turns 4 then 5.
    // contract: mid-conflict recovery re-delivers [CONFLICT RESUMPTION] while REBASE_HEAD present.
    provider.expectText({
      id: 'manager-conflict-resume',
      lane: L('manager', 'manager', 4, 'orchestrator'),
      text: 'Resuming rebase resolution.',
      match: { containsText: ['[CONFLICT RESUMPTION]'] },
    });
    provider.expectText({
      id: 'manager-conflict-resume-2',
      lane: L('manager', 'manager', 5, 'orchestrator'),
      text: 'Resolved rebase resolution.',
      match: { containsText: ['[CONFLICT RESUMPTION]'] },
    });
  }

  // --- coder (child of manager) ---
  provider.expectToolCall({
    id: 'coder-write',
    lane: L('coder', 'coder', 1, 'manager'),
    tool: 'write',
    args: { filePath: PROOF_FILE, content: PROOF_CONTENT },
    match: { requiredTools: ['write'] },
  });
  provider.expectText({
    id: 'coder-terminal',
    lane: L('coder', 'coder', 2, 'manager'),
    text: 'Coder finished.',
  });

  // --- reviewer (child of orchestrator): 4 PERFECT verdicts + 4 terminals ---
  // Verdict math: reverifyTwice is called once per review phase. Pre-rebase
  // phase emits ReviewBarrierStarted("pre-rebase"), then the double-PERFECT
  // check needs 2 distinct PERFECTs (perfect-1 + perfect-2). Post-rebase
  // phase emits ReviewBarrierStarted("post-rebase") (resetting the guard so
  // pre-rebase confirmation does not carry over), then 2 more fresh PERFECTs
  // (perfect-3 + perfect-4). Initial run confirms pre-rebase (1..2) and
  // recovery confirms post-rebase (3..4). No 3rd redundant reviewer call.
  for (const [n, label] of [[1, 'one'], [2, 'two'], [3, 'three'], [4, 'four']]) {
    // Initial run confirms pre-rebase (turns 1..2, alias 'reviewer'); recovery
    // re-runs the chain with a FRESH reviewer session (the old one is unlinked
    // on restart), so its post-rebase verdicts bind to a distinct
    // 'reviewer-recovery' alias. This mirrors the blogger re-anchor lanes and
    // avoids the strict-mock alias mismatch that otherwise left reviewer-perfect-3
    // unmatched (no-lane-head-matched on the new reviewer session id). Each lane
    // key requires turns to start at 1, so the recovery alias renumbers 1..4.
    const reviewerAlias = n <= 2 ? 'reviewer' : 'reviewer-recovery';
    const t = n <= 2 ? n : n - 2;
    provider.expectToolCall({
      id: `reviewer-perfect-${n}`,
      lane: L(reviewerAlias, 'reviewer', t * 2 - 1, 'orchestrator'),
      tool: 'verdict',
      args: { verdict: 'PERFECT' },
      match: { requiredTools: ['verdict'] },
    });
    provider.expectText({
      id: `reviewer-terminal-${n}`,
      lane: L(reviewerAlias, 'reviewer', t * 2, 'orchestrator'),
      text: `Review round ${label} done.`,
    });
  }

  // --- blogger sidecars (initial) ---
  provider.expectText({
    id: 'orch-blogger',
    lane: L('orchestrator-blogger', 'blogger', 1, 'orchestrator'),
    blocking: false,
    text: 'Orchestrator background.',
  });
  provider.expectText({
    id: 'orch-blogger-final',
    lane: L('orchestrator-blogger', 'blogger', 2, 'orchestrator'),
    neverEnd: true,
    text: 'Orchestrator final background.',
  });
  provider.expectText({
    id: 'manager-blogger',
    lane: L('manager-blogger', 'blogger', 1, 'manager'),
    neverEnd: true,
    text: 'Manager job background.',
  });
  provider.expectText({
    id: 'coder-blogger',
    lane: L('coder-blogger', 'blogger', 1, 'coder'),
    neverEnd: true,
    text: 'Coder background.',
  });

  // --- blogger sidecars (post-restart re-anchor; distinct aliases so turns reset) ---
  provider.expectText({
    id: 'orch-blogger-restarted',
    lane: L('orchestrator-blogger-restarted', 'blogger', 1, 'orchestrator'),
    blocking: false,
    text: 'Orchestrator restarted background.',
    match: { containsText: ['You are the blogger of a coding agent session.'] },
  });
  provider.expectText({
    id: 'orch-blogger-final-restarted',
    lane: L('orchestrator-blogger-restarted', 'blogger', 2, 'orchestrator'),
    neverEnd: true,
    text: 'Orchestrator restarted final background.',
    match: { containsText: ['You are the blogger of a coding agent session.'] },
  });
  provider.expectText({
    id: 'manager-blogger-restarted',
    lane: L('manager-blogger-restarted', 'blogger', 1, 'manager'),
    neverEnd: true,
    text: 'Manager restarted background.',
    match: { containsText: ['You are the blogger of a coding agent session.', '"agent":"manager"'] },
  });
}

function registerPreCandidateRecoveryLanes(provider, scenario) {
  const L = (session, role, turn, parent, requestKind = 'chat') =>
    expectationLane(scenario, session, role, turn, requestKind, parent);

  provider.expectToolCall({
    id: 'manager-recovery-fork-coder',
    lane: L('manager-recovery', 'manager', 1, 'orchestrator'),
    tool: 'fork',
    args: { agent: 'coder', prompt: CODER_PROMPT },
    match: { requiredTools: ['fork', 'join', 'list'] },
  });
  provider.expectToolCall({
    id: 'manager-recovery-join-coder',
    lane: L('manager-recovery', 'manager', 2, 'orchestrator'),
    tool: 'join',
    args: {},
    match: { requiredTools: ['fork', 'join', 'list'] },
  });
  provider.expectText({
    id: 'manager-recovery-terminal',
    lane: L('manager-recovery', 'manager', 3, 'orchestrator'),
    text: 'Manager recovered.',
    match: { requiredTools: ['fork', 'join', 'list'] },
  });
  provider.expectToolCall({
    id: 'coder-recovery-write',
    lane: L('coder-recovery', 'coder', 1, 'manager-recovery'),
    tool: 'write',
    args: { filePath: PROOF_FILE, content: PROOF_CONTENT },
    match: { requiredTools: ['write'] },
  });
  provider.expectText({
    id: 'coder-recovery-terminal',
    lane: L('coder-recovery', 'coder', 2, 'manager-recovery'),
    text: 'Coder recovered.',
  });
  provider.expectText({
    id: 'coder-blogger-restarted',
    lane: L('coder-recovery-blogger', 'blogger', 1, 'coder-recovery'),
    neverEnd: true,
    text: 'Coder restarted background.',
    match: { containsText: ['You are the blogger of a coding agent session.', '"agent":"coder"'] },
  });
}

async function runCrash({ conflict, restartAfterId, label, proofExpected }) {
  let scenario;
  let restartPromise = null;
  try {
    scenario = await setupScenario({
      project: {
        files: {
          'AGENTS.md': `- orchestrator restart publish canary (${label})\n`,
          'README.md': `# orchestrator-restart-publish project (${label})\n`,
        },
      },
      strict: true,
    });

    registerLanes(scenario.provider, SCENARIO, { conflict });

    // `restarted` flips true after the host restart resolves; recovery
    // manager/reviewer requests are routed to their distinct recovery aliases.
    let restarted = false;
    // parent session id -> the role that owns it, learned from that session's own
    // (non-blogger) requests. A blogger sidecar is then attributed by parentage
    // instead of by scanning its prompt, which embeds the parent transcript.
    const bloggerParents = new Map();
    const bloggerRestarted = new Map();
    // parent session id -> blogger sessions that spoke before the parent did.
    const pendingBloggers = new Map();
    // Alias binding is per SESSION and never crosses roles. Only the
    // orchestrator emits the "Re-anchor" prompt after a restart; manager/coder
    // sidecars re-anchor silently, so a SECOND session of the same role must
    // take that role's `-restarted` alias instead of colliding with the initial
    // binding (the collision threw inside this hook and surfaced as an opaque
    // 400 that stalled both blogger lanes).
    const bindRoleAlias = (base, sessionID, preferRestarted) => {
      const restartedAlias = `${base}-restarted`;
      if (scenario.provider.sessionFor(base) === sessionID) return;
      if (scenario.provider.sessionFor(restartedAlias) === sessionID) return;
      const first = preferRestarted ? restartedAlias : base;
      const second = preferRestarted ? base : restartedAlias;
      if (!scenario.provider.sessionFor(first)) scenario.provider.bindSession(first, sessionID);
      else if (!scenario.provider.sessionFor(second)) scenario.provider.bindSession(second, sessionID);
    };
    scenario.provider.onRequest = (body) => {
      const sessionID = requestSessionOf(body);
      const text = JSON.stringify(body?.messages || []);
      if (!sessionID) return;
      const role = requestRoleOf(body);
      if (role && role !== 'blogger') {
        bloggerParents.set(sessionID, role === 'orchestrator' ? 'orchestrator' : role);
        if (restarted && !bloggerRestarted.has(sessionID)) bloggerRestarted.set(sessionID, true);
        // A sidecar can speak BEFORE its parent's first model call, so replay any
        // blogger session that arrived while its parent was still unknown.
        const waiting = pendingBloggers.get(sessionID);
        if (waiting) {
          pendingBloggers.delete(sessionID);
          for (const bloggerSessionID of waiting) {
            bindRoleAlias(`${bloggerParents.get(sessionID)}-blogger`, bloggerSessionID, Boolean(bloggerRestarted.get(sessionID)));
          }
        }
      }
      if (text.includes('You are the blogger of a coding agent session.')) {
        // Discriminate by the sidecar's PARENT session, not by scanning the
        // prompt: the companion projection embeds the parent's transcript, so
        // an orchestrator sidecar can also contain `"agent":"manager"` text.
        // Parent identity is exact and already carried by x-parent-session-id.
        const parentID = requestParentSessionOf(body);
        const bloggerRole = parentID && bloggerParents.get(parentID);
        if (bloggerRole) {
          bindRoleAlias(`${bloggerRole}-blogger`, sessionID, Boolean(bloggerRestarted.get(parentID)));
        } else if (parentID) {
          const waiting = pendingBloggers.get(parentID) || new Set();
          waiting.add(sessionID);
          pendingBloggers.set(parentID, waiting);
        }
      }
      // Recovery (post-restart) forks FRESH manager/reviewer/coder sessions, so
      // bind them to distinct recovery aliases; the initial run binds the base
      // aliases. Without this, the initial and recovery lanes (identical
      // role/parent/requestKind) are both unbound at the new session's first
      // request and the strict selector sees ambiguous heads. Coder recovery
      // also parents the post-restart coder blogger lane.
      if (role === 'manager' || role === 'reviewer' || role === 'coder') {
        const recoveryAlias = `${role}-recovery`;
        if (scenario.provider.sessionFor(role) !== sessionID && scenario.provider.sessionFor(recoveryAlias) !== sessionID) {
          const target = restarted ? recoveryAlias : role;
          if (!scenario.provider.sessionFor(target)) scenario.provider.bindSession(target, sessionID);
          else if (!scenario.provider.sessionFor(restarted ? role : recoveryAlias)) {
            scenario.provider.bindSession(restarted ? role : recoveryAlias, sessionID);
          }
        }
      }
    };

    // Crash point B: commit a conflicting change to the SAME proof file directly on
    // the target ref AFTER the manager worktree has branched off master.
    // When coder later writes the file on the manager branch -> "both added" rebase conflict.
    if (conflict) {
      scenario.provider.afterExpectation('manager-fork-coder', () => {
        const workDir = scenario.host.workDir;
        fs.writeFileSync(path.join(workDir, PROOF_FILE), 'Conflicting target edit\n');
        execFileSync('git', ['-C', workDir, 'add', PROOF_FILE], { encoding: 'utf8' });
        execFileSync('git', ['-C', workDir, 'commit', '-m', 'target: conflicting edit to proof file'], {
          encoding: 'utf8',
        });
      });
    }

    // Causal restart: the instant the checkpoint lane is consumed, restart the host.
    // Fire-and-forget (not awaited) so it races ahead of any further production side
    // effects; the recovery waits below implicitly serialize on restart completion.
    scenario.provider.afterExpectation(restartAfterId, () => {
      restartPromise = scenario.restart();
    });

    const orchestrator = await scenario.client.createSession();
    const orchestratorId = getSessionId(orchestrator);
    assert.ok(orchestratorId, `orchestrator session creation failed: ${JSON.stringify(orchestrator)}`);
    scenario.sessionIds.push(orchestratorId);
    bindLaneSession(scenario.provider, orchestratorId, 'title', 'orchestrator');

    const prompt = await scenario.client.request('POST', `/session/${orchestratorId}/prompt_async`, {
      body: {
        agent: 'orchestrator',
        parts: [{ type: 'text', text: ORCH_PROMPT }],
        model: { providerID: 'test', modelID: 'test-model' },
      },
    });
    assert.ok(prompt.ok, `orchestrator prompt failed: ${JSON.stringify(prompt.data)}`);

    // ---- Phase 1: initial run up to the checkpoint that triggers the restart ----
    await scenario.provider.waitForExpectation('orch-fork-manager', WATCHDOG_TIMEOUT_MS);
    await scenario.provider.waitForExpectation('manager-fork-coder', WATCHDOG_TIMEOUT_MS);
    await scenario.provider.waitForExpectation('coder-write', WATCHDOG_TIMEOUT_MS); // proof file created in manager worktree
    await scenario.provider.waitForExpectation('manager-terminal', WATCHDOG_TIMEOUT_MS); // finalizeWorktree -> candidate commit
    for (const n of [1, 2]) {
      await scenario.provider.waitForExpectation(`reviewer-perfect-${n}`, WATCHDOG_TIMEOUT_MS);
      await scenario.provider.waitForExpectation(`reviewer-terminal-${n}`, WATCHDOG_TIMEOUT_MS);
    }
    // crash point A restart trigger: PreRebaseReview confirmed + candidate checkpoint.
    if (conflict) {
      // crash point B: rebase hit the conflict and resumed the manager with [CONFLICT RESUMPTION].
      await scenario.provider.waitForExpectation('manager-conflict-resume', WATCHDOG_TIMEOUT_MS);
    }

    // ---- Phase 2: recovery (after restart) re-runs the idempotent chain ----
    // Host restart deliberately does not resume an in-flight model Run. A fresh
    // user turn is the structured entry point that lazy-loads the Orchestrator,
    // reconciles durable ManagerJobs, and then joins the recovered publish.
    assert.ok(restartPromise, 'checkpoint must schedule a host restart');
    await restartPromise;
    restarted = true;
    if (!conflict) registerPreCandidateRecoveryLanes(scenario.provider, SCENARIO);
    const recoveryTurn = scenario.turn.start(orchestratorId);
    const recoveryPrompt = await scenario.client.request('POST', `/session/${orchestratorId}/prompt_async`, {
      body: {
        agent: 'orchestrator',
        parts: [{ type: 'text', text: 'Continue the publish job and join its result.' }],
        model: { providerID: 'test', modelID: 'test-model' },
      },
    });
    assert.ok(recoveryPrompt.ok, `recovery prompt failed: ${JSON.stringify(recoveryPrompt.data)}`);

    if (conflict) {
      // contract: recovery re-delivers [CONFLICT RESUMPTION] (REBASE_HEAD still present).
      await scenario.provider.waitForExpectation('manager-conflict-resume-2', WATCHDOG_TIMEOUT_MS);
    } else {
      await scenario.provider.waitForExpectation('manager-recovery-fork-coder', WATCHDOG_TIMEOUT_MS);
      await scenario.provider.waitForExpectation('coder-recovery-write', WATCHDOG_TIMEOUT_MS);
      await scenario.provider.waitForExpectation('coder-recovery-terminal', WATCHDOG_TIMEOUT_MS);
      await scenario.provider.waitForExpectation('manager-recovery-join-coder', WATCHDOG_TIMEOUT_MS);
      await scenario.provider.waitForExpectation('manager-recovery-terminal', WATCHDOG_TIMEOUT_MS);
    }
    for (const n of [3, 4]) {
      await scenario.provider.waitForExpectation(`reviewer-perfect-${n}`, WATCHDOG_TIMEOUT_MS);
      await scenario.provider.waitForExpectation(`reviewer-terminal-${n}`, WATCHDOG_TIMEOUT_MS);
    }
    await scenario.provider.waitForExpectation('orch-join-2', WATCHDOG_TIMEOUT_MS); // recovery re-issues join
    await scenario.provider.waitForExpectation('orch-final', WATCHDOG_TIMEOUT_MS); // Published returned

    // post-restart blogger sidecars re-anchor after the host restart
    await scenario.provider.waitForExpectation('orch-blogger-restarted', WATCHDOG_TIMEOUT_MS);
    await scenario.provider.waitForExpectation('manager-blogger-restarted', WATCHDOG_TIMEOUT_MS);
    if (!conflict) await scenario.provider.waitForExpectation('coder-blogger-restarted', WATCHDOG_TIMEOUT_MS);

    await recoveryTurn.awaitTerminal({
      timeoutMs: WATCHDOG_TIMEOUT_MS,
      requireActivity: true,
      requireAssistantTerminal: false,
      requireIdleAfterActivity: true,
    });
    scenario.provider.expectSatisfied();

    // ---- Git + journal assertions ----
    const workDir = scenario.host.workDir;
    const git = (args) => execFileSync('git', args, { cwd: workDir, encoding: 'utf8' });

    // contract: exactly one candidate commit in the target (no duplicate despite restart).
    const log = git(['log', '--format=%s', 'HEAD']);
    const candidateCommits = log.split('\n').filter((l) => l.startsWith('candidate:')).length;
    assert.equal(candidateCommits, 1, `exactly one candidate commit, got:\n${log}`);

    // contract: proof file present in the main worktree.
    const proofPath = path.join(workDir, PROOF_FILE);
    assert.ok(fs.existsSync(proofPath), `proof file ${PROOF_FILE} must exist after publish`);
    const proof = fs.readFileSync(proofPath, 'utf8');
    if (proofExpected) {
      assert.equal(proof, proofExpected, `proof content mismatch: ${JSON.stringify(proof)}`);
    } else {
      // crash point B: resolved via production finalizeWorktree (git add -A +
      // rebase --continue commits the conflicted worktree content), so the coder's
      // content is present even if conflict markers remain.
      assert.ok(proof.includes('Published by orchestrator canary'), 'provenance lost in conflict resolution');
    }

    // contract: no residual git worktree + no manager/<id> branch after cleanup.
    const worktrees = git(['worktree', 'list', '--porcelain']);
    const extra = worktrees
      .split('\n')
      .filter((line) => line.startsWith('worktree ') && !line.includes(workDir));
    assert.equal(extra.length, 0, `worktree must be cleaned up after publish, got:\n${worktrees}`);
    const branches = git(['branch', '--list', 'manager/*']);
    assert.equal(branches.trim(), '', `manager branch must be deleted after publish, got:\n${branches}`);

    const candidateRegisteredFacts = countFact(workDir, 'OrchestratorCandidateRegistered');
    const publishedFacts = countFact(workDir, 'OrchestratorPublished');
    const conflictDetectedFacts = conflict ? countFact(workDir, 'OrchestratorConflictDetected') : 0;
    const rebasedFacts = conflict ? countFact(workDir, 'OrchestratorRebased') : 0;

    // contract: durable barriers recorded exactly once — exactly one CandidateRegistered
    // and exactly one Published for the manager; no duplicate publish.
    assert.equal(
      candidateRegisteredFacts,
      1,
      'exactly one OrchestratorCandidateRegistered fact',
    );
    assert.equal(
      publishedFacts,
      1,
      'exactly one OrchestratorPublished fact',
    );

    if (conflict) {
      // contract: conflict was detected (>=1) and the rebase converged (>=1 Rebased barrier).
      assert.ok(conflictDetectedFacts >= 1, 'expected a ConflictDetected fact');
      assert.ok(rebasedFacts >= 1, 'expected a Rebased barrier fact');
    }

    await teardownScenario(scenario);
    console.log(
      `  [${label}] passed: candidate=${candidateCommits} Published=${publishedFacts} ` +
        `worktree-residue=${extra.length} manager-branch=${branches.trim() === '' ? 'none' : 'LEAK'}`,
    );
  } catch (error) {
    if (restartPromise) {
      try {
        await restartPromise;
      } catch {}
    }
    if (scenario?.provider?.unexpectedRequests) {
      console.error(JSON.stringify(scenario.provider.unexpectedRequests));
    }
    if (scenario?.host?.workDir) {
      console.error(`workDir: ${scenario.host.workDir}`);
      console.error(`pending: ${JSON.stringify(scenario.provider.blockedExpectations)}`);
    }
    if (scenario?.host?.stdoutLog) console.error(`host stdout: ${scenario.host.stdoutLog.slice(-4000)}`);
    if (scenario?.host?.stderrLog) console.error(`host stderr: ${scenario.host.stderrLog.slice(-4000)}`);
    if (scenario) {
      try {
        await teardownScenario(scenario, { keepOnFailure: true });
      } catch {}
    }
    throw error;
  }
}

try {
  if (!runStaticGate([__filename]).passed) {
    throw new Error('orchestrator restart publish canary contains prohibited fixed sleep or polling loop');
  }

  // Crash point A: restart at the candidate checkpoint (PreRebaseReview confirmed,
  // candidate commit exists), before the chain publishes.
  await runCrash({
    conflict: false,
    restartAfterId: 'reviewer-terminal-2',
    label: 'after-candidate',
    proofExpected: PROOF_CONTENT,
  });

  // Crash point B: restart mid rebase-conflict resolution (REBASE_HEAD still present).
  await runCrash({
    conflict: true,
    restartAfterId: 'manager-conflict-resume',
    label: 'rebase-conflict',
    proofExpected: null,
  });

  console.log('Orchestrator restart publish canary passed: crash-after-candidate + crash-during-rebase-conflict both recover to exactly-once publish.');
} catch (error) {
  console.error(`Orchestrator restart publish canary failed: ${error.stack || error}`);
  process.exit(1);
}
