// CUT-36 — OFFLINE tests for the smoke assertions.
//
// These never start omp and never spend quota: they feed the assertions
// synthetic host evidence and check that each fact is enforced.
//
// Run (offline, no model call):
//   node --test proposals/host-neutral-interface/smoke/assert-smoke.test.mjs
//
// This file is intentionally NOT wired into scripts/verify.mjs / npm test:
// the default gates stay offline and the smoke remains an explicit opt-in.

import assert from "node:assert/strict"
import { execFileSync } from "node:child_process"
import { DatabaseSync } from "node:sqlite"
import fs from "node:fs"
import os from "node:os"
import path from "node:path"
import { test } from "node:test"
import {
  assertSmoke,
  loadResourceMarkers,
  productWorkIdOf,
  readWorkJournal,
  resolveProductStateRoot,
  splitProviderPayload,
} from "./assert-smoke.mjs"

const REPO = path.resolve(import.meta.dirname, "../../..")
const NONCE = "cut36-test-nonce-0001"

const git = (cwd, args) =>
  execFileSync("git", args, { cwd, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] })

/**
 * A real git repository, so the state root is resolved by git exactly as the
 * product does (RuntimePath.stateRoot -> primary worktree + /.Herdchestra).
 *
 * `linked` adds a linked worktree: the product's state then lives in the
 * PRIMARY worktree, not in the linked one.
 */
function makeWorkspace({ linked = false } = {}) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "cut36-evidence-"))
  const primary = path.join(root, "primary")
  fs.mkdirSync(primary)
  git(primary, ["init", "-q"])
  git(primary, ["config", "user.email", "cut36@example.invalid"])
  git(primary, ["config", "user.name", "cut36"])
  git(primary, ["add", "-A"])
  git(primary, ["commit", "-q", "--allow-empty", "-m", "cut36"])
  let workspace = primary
  if (linked) {
    const linkedDir = path.join(root, "linked")
    git(primary, ["worktree", "add", "-q", "-b", "cut36-linked", linkedDir])
    workspace = linkedDir
  }
  return { root, primary, workspace }
}

/**
 * A REAL sqlite journal, shaped exactly like the product's:
 * `wanxiangli/eventstore/SqliteJournal` keeps one `journal_entries` table
 * (stream, revision, request_id, body, checksum). Ownership is committed
 * INTO that same journal under the stream `ownership:<registryId>`.
 */
function writeJournal(
  stateRoot,
  {
    nonce,
    ownership = true,
    registryId = "cut36-registry-1",
    corrupt = false,
    workspace = null,
    childSessionId = null,
    runWork = true,
    scopeId = null,
  } = {},
) {
  const dir = path.join(stateRoot, "wanxiangshu")
  fs.mkdirSync(dir, { recursive: true })
  const journalPath = path.join(dir, "work.sqlite")
  if (corrupt) {
    fs.writeFileSync(journalPath, "this is not a sqlite database")
    return journalPath
  }
  fs.rmSync(journalPath, { force: true })
  fs.rmSync(`${journalPath}-wal`, { force: true })
  fs.rmSync(`${journalPath}-shm`, { force: true })
  const db = new DatabaseSync(journalPath)
  db.exec(`CREATE TABLE journal_entries (
  stream TEXT NOT NULL,
  revision INTEGER NOT NULL CHECK (revision > 0),
  request_id TEXT NOT NULL,
  body BLOB NOT NULL,
  checksum TEXT NOT NULL,
  PRIMARY KEY (stream, revision),
  UNIQUE (stream, request_id)
) STRICT;`)
  const insert = db.prepare(
    "INSERT INTO journal_entries (stream, revision, request_id, body, checksum) VALUES (?, ?, ?, ?, ?)",
  )
  // A work record carrying the delivered token.
  insert.run("work:cut36", 1, "req-work-1", Buffer.from(JSON.stringify({ prompt: `deliver ${nonce}` }), "utf8"), "sum")
  if (ownership) {
    // The product's Ownership record: an OpenScope message for the activation
    // scope `host-activation:<projectId>` (HostPlugin.fs:159-168). The
    // registry id is never assumed by the smoke.
    const scope = scopeId ?? (workspace ? `host-activation:${workspace}` : "host-activation:unknown")
    insert.run(
      `ownership:${registryId}`,
      1,
      "req-own-1",
      Buffer.from(
        JSON.stringify({
          registryId,
          revision: 1,
          previous: null,
          requestId: "req-own-1",
          message: { type: "OpenScope", scopeId: scope, parentId: null },
          digest: "sum",
        }),
        "utf8",
      ),
      "sum",
    )
  }
  if (runWork && childSessionId) {
    // THIS run's own durable work stream: the product derives it from the
    // child session id (HostPlugin.fs:230).
    const workId = productWorkIdOf(childSessionId)
    insert.run(
      workId,
      1,
      "req-work-open",
      Buffer.from(
        JSON.stringify({
          requestId: "req-work-open",
          requestHash: "sum",
          events: [
            {
              type: "WorkOpened",
              workId,
              root: workspace ?? "",
              sequence: 0,
              authorityInitial: "authority-initial-v1",
            },
          ],
        }),
        "utf8",
      ),
      "sum",
    )
  }
  db.close()
  return journalPath
}

function writeStateArtifacts(stateRoot, nonce, options = {}) {
  fs.mkdirSync(path.join(stateRoot, "wanxiangshu"), { recursive: true })
  writeJournal(stateRoot, { nonce, ...options })
}

const CHILD_SESSION_ID = "child-1"

function buildEvidence({
  bilingual = true,
  nonceInJoin = true,
  toolError = false,
  inPromptOnly = false,
  linked = false,
  payloadShape = "chat",
  journal = {},
  productTool = "wxs_deliver",
  childSessionId = CHILD_SESSION_ID,
} = {}) {
  const { root, primary, workspace } = makeWorkspace({ linked })
  const sessionDir = path.join(root, "sessions", "-ws")
  fs.mkdirSync(sessionDir, { recursive: true })

  // The product's state root: the PRIMARY worktree's .Herdchestra.
  const stateRoot = path.join(primary, ".Herdchestra")
  writeStateArtifacts(stateRoot, NONCE, { workspace, childSessionId, ...journal })

  const markers = loadResourceMarkers(REPO)
  const prompt = `deliver ${NONCE}`
  const system = inPromptOnly ? "中文 english" : `${markers.en}\n${markers.zh}`
  const toolDescription = inPromptOnly ? "执行 / run" : "执行 canary 检查 / run the canary check"

  // The provider payload shape differs per provider; the assertions must hold
  // for all of them.
  const payloads = {
    // pi-ai Context: { systemPrompt: string[], messages, tools }
    context: () => ({
      systemPrompt: [system],
      messages: [{ role: "user", content: prompt }],
      tools: [{ name: productTool, description: toolDescription, parameters: {} }],
    }),
    // Anthropic messages wire body: top-level `system` (text blocks)
    anthropic: () => ({
      system: [{ type: "text", text: system }],
      messages: [{ role: "user", content: prompt }],
      tools: [{ name: productTool, description: toolDescription, input_schema: { type: "object" } }],
    }),
    // OpenAI Responses wire body: top-level `instructions`, flat function tools
    responses: () => ({
      instructions: system,
      input: [{ role: "user", content: prompt }],
      tools: [{ type: "function", name: productTool, description: toolDescription, parameters: {} }],
    }),
    // OpenAI chat completions wire body: system/developer messages, nested fn
    chat: () => ({
      messages: [
        { role: "system", content: system },
        { role: "user", content: prompt },
      ],
      tools: [{ type: "function", function: { name: productTool, description: toolDescription } }],
    }),
  }
  const payload = payloads[payloadShape]()

  const observer = [
    { kind: "session_start", sessionId: "parent-1", cwd: workspace },
    { kind: "before_agent_start", prompt },
    { kind: "before_provider_request", payload },
    { kind: "turn_end", role: "assistant", stopReason: "stop" },
    { kind: "tool_execution_start", toolName: productTool },
    {
      kind: "tool_execution_end",
      toolName: productTool,
      isError: toolError,
      text: nonceInJoin ? `joined child ${NONCE}` : "joined child other-token",
    },
  ]
  const observerOut = path.join(root, "observer.jsonl")
  fs.writeFileSync(observerOut, observer.map((o) => JSON.stringify(o)).join("\n"))

  const childFile = path.join(sessionDir, "child.jsonl")
  fs.writeFileSync(
    childFile,
    [
      JSON.stringify({ type: "session", id: childSessionId, parentSession: "parent-1", cwd: workspace }),
      JSON.stringify({ type: "message", message: { role: "user", content: [{ type: "text", text: prompt }] } }),
      JSON.stringify({ type: "message", message: { role: "assistant", content: [{ type: "text", text: "done 完成" }] } }),
    ].join("\n"),
  )

  return { root, primary, workspace, stateRoot, observerOut, childFile, sessionsRoot: path.join(root, "sessions") }
}

function verdictFor(evidence, overrides = {}) {
  return assertSmoke({
    observerOut: evidence.observerOut,
    sessionsRoot: evidence.sessionsRoot,
    newSessionFiles: [evidence.childFile],
    parentSessionId: "parent-1",
    nonce: NONCE,
    workspace: evidence.workspace,
    repo: REPO,
    // Nothing existed before this run: the fixture's journal IS this run's.
    journalBaseline: { streams: {} },
    ...overrides,
  })
}

test("a complete, honest run passes every fact", () => {
  const evidence = buildEvidence()
  const verdict = verdictFor(evidence)
  assert.equal(verdict.result, "PASS", verdict.failures.join("\n"))
  assert.equal(verdict.childSession.id, "child-1")
})

test("bilingual content only in the user prompt does not satisfy the bilingual fact", () => {
  const evidence = buildEvidence({ inPromptOnly: true })
  const verdict = verdictFor(evidence)
  assert.equal(verdict.result, "FAIL")
  assert.ok(verdict.failures.some((f) => f.includes("paired-bilingual resource")))
})

test("a join result without the nonce is not a join", () => {
  const evidence = buildEvidence({ nonceInJoin: false })
  const verdict = verdictFor(evidence)
  assert.equal(verdict.result, "FAIL")
  assert.ok(verdict.failures.some((f) => f.includes("join result carrying the nonce")))
})

test("an errored product tool call fails the run", () => {
  const evidence = buildEvidence({ toolError: true })
  const verdict = verdictFor(evidence)
  assert.equal(verdict.result, "FAIL")
  assert.ok(verdict.failures.some((f) => f.includes("no product tool call errored")))
})

test("only session files created by this run are considered", () => {
  const evidence = buildEvidence()
  const verdict = verdictFor(evidence, { newSessionFiles: [] })
  assert.equal(verdict.result, "FAIL")
  assert.ok(verdict.failures.some((f) => f.includes("real child session forked from this run's parent")))
})

test("a child parented to another session is not this run's child", () => {
  const evidence = buildEvidence()
  const verdict = verdictFor(evidence, { parentSessionId: "some-other-parent" })
  assert.equal(verdict.result, "FAIL")
  assert.ok(verdict.failures.some((f) => f.includes("real child session forked from this run's parent")))
})

test("the state root is the primary worktree's .Herdchestra, not a .git directory", () => {
  const evidence = buildEvidence()
  assert.equal(evidence.stateRoot, path.join(evidence.primary, ".Herdchestra"))
  assert.equal(resolveProductStateRoot(evidence.workspace), evidence.stateRoot)
  assert.ok(!evidence.stateRoot.includes(`${path.sep}.git${path.sep}`))
})

test("a linked worktree resolves to the primary worktree's state root", () => {
  const evidence = buildEvidence({ linked: true })
  // the workspace is the LINKED worktree; the state lives in the primary one
  assert.notEqual(evidence.workspace, evidence.primary)
  assert.equal(resolveProductStateRoot(evidence.workspace), evidence.stateRoot)
  const verdict = verdictFor(evidence)
  assert.equal(verdict.result, "PASS", verdict.failures.join("\n"))
})

test("state written only into the linked worktree is not the product's state", () => {
  const evidence = buildEvidence({ linked: true })
  // move the artifacts into the linked worktree, away from the primary root
  fs.rmSync(evidence.stateRoot, { recursive: true, force: true })
  writeStateArtifacts(path.join(evidence.workspace, ".Herdchestra"), NONCE)
  const verdict = verdictFor(evidence)
  assert.equal(verdict.result, "FAIL")
  assert.ok(verdict.failures.some((f) => f.includes("product state root exists on disk")))
})

test("missing on-disk Work/Ownership artifacts fail the run", () => {
  const evidence = buildEvidence()
  fs.rmSync(evidence.stateRoot, { recursive: true, force: true })
  const verdict = verdictFor(evidence)
  assert.equal(verdict.result, "FAIL")
  assert.ok(verdict.failures.some((f) => f.includes("product state root exists on disk")))
})

test("state artifacts without this run's identifier fail the run", () => {
  const evidence = buildEvidence()
  // a real journal, but it journals a DIFFERENT run's identifier
  writeJournal(evidence.stateRoot, { nonce: "some-other-run" })
  const verdict = verdictFor(evidence)
  assert.equal(verdict.result, "FAIL")
  assert.ok(verdict.failures.some((f) => f.includes("this run's identifier")))
})

test("an Anthropic-style payload (top-level system) satisfies the bilingual facts", () => {
  const evidence = buildEvidence({ payloadShape: "anthropic" })
  const verdict = verdictFor(evidence)
  assert.equal(verdict.result, "PASS", verdict.failures.join("\n"))
})

test("a pi-ai Context payload (systemPrompt[]) satisfies the bilingual facts", () => {
  const evidence = buildEvidence({ payloadShape: "context" })
  const verdict = verdictFor(evidence)
  assert.equal(verdict.result, "PASS", verdict.failures.join("\n"))
})

test("an OpenAI Responses payload (instructions) satisfies the bilingual facts", () => {
  const evidence = buildEvidence({ payloadShape: "responses" })
  const verdict = verdictFor(evidence)
  assert.equal(verdict.result, "PASS", verdict.failures.join("\n"))
})

test("the payload parser reads every provider system-prompt shape", () => {
  const en = "A".repeat(40)
  const zh = "文".repeat(40)
  const desc = "执行 / run"
  const shapes = {
    context: { systemPrompt: [en, zh], tools: [{ name: "t", description: desc }] },
    anthropicString: { system: `${en}\n${zh}`, tools: [{ name: "t", description: desc }] },
    anthropicBlocks: { system: [{ type: "text", text: en }, { type: "text", text: zh }], tools: [{ name: "t", description: desc }] },
    instructions: { instructions: `${en}\n${zh}`, tools: [{ type: "function", name: "t", description: desc }] },
    chatSystem: { messages: [{ role: "system", content: en }, { role: "developer", content: zh }], tools: [{ type: "function", function: { name: "t", description: desc } }] },
    responsesInput: { input: [{ role: "developer", content: `${en}\n${zh}` }], tools: [{ type: "function", name: "t", description: desc }] },
  }
  for (const [name, payload] of Object.entries(shapes)) {
    const split = splitProviderPayload(payload)
    assert.ok(split.system.includes(en), `${name}: english system text missing`)
    assert.ok(split.system.includes(zh), `${name}: chinese system text missing`)
    assert.equal(split.tools.length, 1, `${name}: tool not found`)
    assert.equal(split.tools[0].name, "t", `${name}: tool name`)
    assert.equal(split.tools[0].description, desc, `${name}: tool description`)
  }
})

test("an unresolvable state root fails closed instead of guessing a path", () => {
  const evidence = buildEvidence()
  const notARepo = fs.mkdtempSync(path.join(os.tmpdir(), "cut36-not-a-repo-"))
  assert.throws(() => resolveProductStateRoot(notARepo), /cannot resolve state root/)
  assert.throws(() => resolveProductStateRoot(path.join(notARepo, "nope")), /cannot resolve state root/)
  // and the run itself FAILs, it does not pass on a guessed path
  const verdict = verdictFor(evidence, { workspace: notARepo })
  assert.equal(verdict.result, "FAIL")
  assert.ok(verdict.failures.some((f) => f.includes("state root could be resolved")))
})

test("a host that never started a session fails the run", () => {
  // The observer extension is always loaded, so a session_start event proves
  // only that SOME extension loaded. When the host produced no session at
  // all, the run must FAIL — never vacuously pass on the remaining facts.
  const evidence = buildEvidence()
  const lines = fs
    .readFileSync(evidence.observerOut, "utf8")
    .split("\n")
    .filter((l) => l.trim() !== "")
    .map((l) => JSON.parse(l))
    .filter((o) => o.kind !== "session_start")
  fs.writeFileSync(evidence.observerOut, lines.map((o) => JSON.stringify(o)).join("\n"))
  const verdict = verdictFor(evidence)
  assert.equal(verdict.result, "FAIL")
  assert.ok(verdict.failures.some((f) => f.includes("host session started")))
})

test("an INERT product bundle fails the run even with a stale journal present", () => {
  // The bundle loads but does nothing: it registers NO tool. The observer
  // still fires session_start (the observer extension loaded) and the host
  // still offers its own built-in `task` tool — neither is the product.
  // A STALE journal from an earlier run is present as well, so the only
  // thing that could save the run is the product artifact — and there is
  // none.
  const evidence = buildEvidence({ productTool: "task" })
  writeJournal(evidence.stateRoot, {
    nonce: NONCE,
    registryId: "stale-registry",
    scopeId: "host-activation:/some/other/workspace",
    childSessionId: null,
    runWork: false,
  })
  const verdict = verdictFor(evidence, {
    journalBaseline: { streams: { "ownership:stale-registry": 1, "work:cut36": 1 } },
  })
  assert.equal(verdict.result, "FAIL")
  assert.ok(
    verdict.failures.some((f) => f.includes("host loaded the product bundle")),
    verdict.failures.join("\n"),
  )
  // the built-in tool must satisfy nothing: the product facts all fail
  assert.ok(verdict.failures.some((f) => f.includes("product tool executed in the host")))
  assert.ok(verdict.failures.some((f) => f.includes("product tool execution completed")))
  assert.ok(verdict.failures.some((f) => f.includes("join result carrying the nonce")))
})

test("STALE Ownership from an earlier run does not satisfy the ownership facts", () => {
  // An ownership stream committed by an EARLIER run, for a DIFFERENT scope,
  // plus this run's nonce only inside that old record. The pre-run baseline
  // says it all existed before this run started.
  const evidence = buildEvidence({ journal: { registryId: "stale-registry" } })
  // rewrite the journal so the ownership record belongs to another workspace
  writeJournal(evidence.stateRoot, {
    nonce: NONCE,
    registryId: "stale-registry",
    scopeId: "host-activation:/some/other/workspace",
    childSessionId: null,
    runWork: false,
  })
  const verdict = verdictFor(evidence, {
    journalBaseline: {
      streams: { "ownership:stale-registry": 1, "work:cut36": 1 },
    },
  })
  assert.equal(verdict.result, "FAIL")
  assert.ok(
    verdict.failures.some((f) => f.includes("Ownership scope is bound to THIS run's workspace")),
    verdict.failures.join("\n"),
  )
  assert.ok(verdict.failures.some((f) => f.includes("durable Work stream for THIS run's child session exists")))
  assert.ok(
    verdict.failures.some((f) => f.includes("this run's identifier")),
    verdict.failures.join("\n"),
  )
})

test("Ownership committed in the Work journal satisfies the ownership fact", () => {
  // The product commits Ownership INTO the journal, on the stream
  // `ownership:<registryId>`. The registry id is not knowable in advance,
  // so the smoke DISCOVERS the stream — it never assumes one.
  const evidence = buildEvidence({ journal: { registryId: "some-unknown-registry" } })
  const verdict = verdictFor(evidence)
  assert.equal(verdict.result, "PASS", verdict.failures.join("\n"))
  assert.ok(
    verdict.journal.ownershipStreams.some((o) => o.stream === "ownership:some-unknown-registry"),
    JSON.stringify(verdict.journal),
  )
})

test("a Work journal with no ownership stream fails the ownership fact", () => {
  // No `ownership:*` stream — the registry was never committed. This must
  // FAIL, not skip: the file exists, so the fact is decidable.
  const evidence = buildEvidence({ journal: { ownership: false } })
  const verdict = verdictFor(evidence)
  assert.equal(verdict.result, "FAIL")
  assert.ok(verdict.failures.some((f) => f.includes("Ownership registry is committed in the Work journal")))
})

test("a corrupt Work journal fails the run instead of skipping the ownership fact", () => {
  // A file that is not a database: the read must FAIL CLOSED.
  const evidence = buildEvidence({ journal: { corrupt: true } })
  const verdict = verdictFor(evidence)
  assert.equal(verdict.result, "FAIL")
  assert.ok(verdict.failures.some((f) => f.includes("Work journal could be read")))
  assert.ok(verdict.failures.some((f) => f.includes("Ownership registry is committed in the Work journal")))
})

test("an absent Work journal fails the run", () => {
  const evidence = buildEvidence()
  fs.rmSync(path.join(evidence.stateRoot, "wanxiangshu", "work.sqlite"), { force: true })
  const verdict = verdictFor(evidence)
  assert.equal(verdict.result, "FAIL")
  assert.ok(verdict.failures.some((f) => f.includes("durable Work journal exists on disk")))
})

test("the journal reader discovers the ownership stream and the nonce", () => {
  const evidence = buildEvidence()
  const journal = readWorkJournal(evidence.stateRoot)
  assert.equal(journal.error, null)
  assert.equal(journal.ownershipStreams.length, 1)
  assert.ok(journal.ownershipStreams[0].stream.startsWith("ownership:"))
  assert.ok(journal.bodies.some((b) => b.includes(NONCE)))
})
