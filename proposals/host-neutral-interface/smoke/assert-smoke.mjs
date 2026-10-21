#!/usr/bin/env node
// CUT-36 — smoke assertions over HOST-OBSERVED facts.
//
// Rules of this file:
//   * Every fact is HOST-OBSERVED or ON DISK. The product writes no smoke
//     report, and none is consulted: there is nothing self-reported to
//     disagree with the host.
//   * Only session files created by THIS run are considered (no scan of old
//     sessions, no fallback).
//   * The bilingual check uses the COMPILED PRODUCT RESOURCES' own text
//     (read from resources/provider/** at assert time), and it inspects the
//     system prompt / tool descriptions only — never the user prompt.
//   * fork / deliver / join are proven by a run-specific random NONCE that
//     appears in the child's delivered user message and in the parent's
//     observed join result.
//   * Work / Ownership are proven as ON-DISK artifacts under the product's
//     state root — the PRIMARY WORKTREE's `.Herdchestra`, exactly as
//     RuntimePath.stateRoot resolves it. Ownership is committed INTO the
//     Work journal (`<stateRoot>/wanxiangshu/work.sqlite`, stream
//     `ownership:<registryId>`), so the journal is READ, never name-scanned.
//   * The ownership/work evidence is scoped to THIS RUN: the ownership
//     record must open the scope `host-activation:<workspace>` (the omp
//     adapter's project id IS the cwd), and the run's own child-session
//     work stream (`host2-<sha256(childSessionId)[0:32]>`) must be NEWER
//     than a PRE-RUN baseline snapshot of the journal. With no baseline the
//     fact is undecidable and FAILS (fail closed).

// CLI (re-check evidence later, no model call):
//   node proposals/host-neutral-interface/smoke/assert-smoke.mjs \
//     --observer <observer.jsonl> --nonce <nonce> \
//     --parent-session <id> --workspace <dir> [--sessions <root>]

import fs from "node:fs"
import { execFileSync } from "node:child_process"
import { DatabaseSync } from "node:sqlite"
import crypto from "node:crypto"
import os from "node:os"
import path from "node:path"
import { fileURLToPath } from "node:url"

const HERE = path.dirname(fileURLToPath(import.meta.url))
const REPO = path.resolve(HERE, "../../..")

export function readJsonl(file) {
  if (!file || !fs.existsSync(file)) return []
  return fs
    .readFileSync(file, "utf8")
    .split("\n")
    .filter((l) => l.trim().length > 0)
    .map((l) => {
      try {
        return JSON.parse(l)
      } catch {
        return null
      }
    })
    .filter(Boolean)
}

export function readJson(file) {
  if (!file || !fs.existsSync(file)) return null
  try {
    return JSON.parse(fs.readFileSync(file, "utf8"))
  } catch {
    return null
  }
}

export function listSessionFiles(root) {
  const out = []
  if (!root || !fs.existsSync(root)) return out
  for (const entry of fs.readdirSync(root, { withFileTypes: true })) {
    const full = path.join(root, entry.name)
    if (entry.isDirectory()) out.push(...listSessionFiles(full))
    else if (entry.name.endsWith(".jsonl")) out.push(full)
  }
  return out
}

function collectText(node, acc = []) {
  if (typeof node === "string") acc.push(node)
  else if (Array.isArray(node)) for (const n of node) collectText(n, acc)
  else if (node && typeof node === "object") for (const n of Object.values(node)) collectText(n, acc)
  return acc
}

/**
 * Pull the system prompt strings and the tool descriptions out of a provider
 * payload, whatever shape the payload takes.
 *
 * `before_provider_request` hands the extension whatever the provider's
 * transport is about to send — which differs per provider:
 *
 *   - pi-ai `Context`      : { systemPrompt?: string[], messages, tools }
 *   - Anthropic messages   : { system: string | [{type:"text",text}], tools:
 *                            [{name, description, input_schema}] }
 *   - OpenAI Responses     : { instructions: string, input: [...], tools:
 *                            [{type:"function", name, description}] }
 *   - OpenAI chat completions: { messages: [{role:"system"|"developer",...}],
 *                            tools: [{type:"function", function:{name,
 *                            description}}] }
 *
 * Every one of those shapes is parsed, so the bilingual facts hold no matter
 * which provider the run went through.
 */
export function splitProviderPayload(payload) {
  const system = []
  const tools = []
  if (!payload || typeof payload !== "object") return { system: "", tools }

  const pushSystem = (value) => {
    const text = collectText(value).join("\n")
    if (text.trim().length > 0) system.push(text)
  }

  // 1. pi-ai Context shape: systemPrompt: string[]
  if (Array.isArray(payload.systemPrompt)) for (const s of payload.systemPrompt) pushSystem(s)
  else if (typeof payload.systemPrompt === "string") pushSystem(payload.systemPrompt)

  // 2. Anthropic wire shape: top-level `system` (string or [{type:"text"}])
  if (payload.system !== undefined) pushSystem(payload.system)

  // 3. OpenAI Responses wire shape: top-level `instructions`
  if (payload.instructions !== undefined) pushSystem(payload.instructions)

  // 4. OpenAI chat-completions wire shape: role system/developer messages
  const messages = payload.messages ?? payload.input ?? []
  for (const m of Array.isArray(messages) ? messages : []) {
    if (m?.role !== "system" && m?.role !== "developer") continue
    pushSystem(m.content ?? m.text ?? "")
  }

  // Tools: Anthropic {name, description}, Responses flat {type:"function",
  // name, description}, chat completions {type:"function", function:{...}},
  // and pi-ai Context tools [{name, description}].
  const toolList = payload.tools ?? payload.functions ?? []
  for (const t of Array.isArray(toolList) ? toolList : []) {
    if (!t || typeof t !== "object") continue
    const fn = t.function && typeof t.function === "object" ? t.function : null
    const name = t.name ?? fn?.name ?? ""
    if (!name) continue
    const desc = t.description ?? fn?.description ?? ""
    tools.push({ name, description: collectText(desc).join("\n") })
  }
  return { system: system.join("\n"), tools }
}

/** The product's own compiled resource text, used as the bilingual marker source. */
export function loadResourceMarkers(repo = REPO) {
  const dir = path.join(repo, "resources", "provider", "world", "common-law")
  const en = fs.existsSync(path.join(dir, "en.md")) ? fs.readFileSync(path.join(dir, "en.md"), "utf8") : ""
  const zh = fs.existsSync(path.join(dir, "zh-CN.md")) ? fs.readFileSync(path.join(dir, "zh-CN.md"), "utf8") : ""
  const sentence = (text) =>
    text
      .split("\n")
      .map((l) => l.trim())
      .find((l) => l.length > 30 && !l.startsWith("#")) ?? ""
  return {
    en: sentence(en),
    zh: sentence(zh),
    enTitle: (en.split("\n").find((l) => l.startsWith("#")) ?? "").replace(/^#\s*/, ""),
    zhTitle: (zh.split("\n").find((l) => l.startsWith("#")) ?? "").replace(/^#\s*/, ""),
  }
}

/**
 * The product's on-disk state root.
 *
 * Mirrors Wanxiangshu.Persistence.Journal.RuntimePath.stateRoot exactly: the
 * state lives in the PRIMARY WORKTREE's `.Herdchestra` (linked worktrees
 * share the primary's state), never inside a `.git` directory.
 *
 *   primary worktree = the first entry of `git worktree list --porcelain`
 *                      (RuntimePath.fs -> primaryWorktree)
 *   git common dir   = `git rev-parse --git-common-dir` (the topology check)
 *   state root       = <primary worktree>/.Herdchestra
 *
 * FAIL CLOSED: when git cannot resolve the topology, this THROWS. Guessing a
 * path would silently assert on the wrong directory, and a wrong-but-present
 * directory is exactly the kind of false PASS this smoke must never give.
 */
export function resolveProductStateRoot(workspace) {
  if (!workspace || !fs.existsSync(workspace)) {
    throw new Error(`cannot resolve state root: workspace does not exist: ${workspace}`)
  }
  const git = (args) => {
    try {
      return execFileSync("git", args, { cwd: workspace, encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] }).trim()
    } catch {
      return ""
    }
  }

  const commonDir = git(["rev-parse", "--path-format=absolute", "--git-common-dir"])
  const listing = git(["worktree", "list", "--porcelain"])
  if (!commonDir && !listing) {
    throw new Error(`cannot resolve state root: git could not resolve the repository at ${workspace}`)
  }
  const first = listing.split("\n").find((l) => l.startsWith("worktree "))
  let primary
  if (first) primary = first.slice("worktree ".length).trim()
  else if (commonDir) primary = path.dirname(commonDir)
  else throw new Error(`cannot resolve state root: git reported no worktrees and no common dir at ${workspace}`)
  if (!primary) {
    throw new Error(`cannot resolve state root: git reported an empty primary worktree at ${workspace}`)
  }

  return path.join(primary, ".Herdchestra")
}

/** Kept for callers that only want the directory name resolution. */
export function productStateRoot(workspace) {
  return resolveProductStateRoot(workspace)
}

/**
 * The product's durable Work journal, read the way the product writes it.
 *
 * `DurableWorkHost.journalPath stateRoot` is `<stateRoot>/wanxiangshu/work.sqlite`
 * and the records live in the `journal_entries` table of
 * `wanxiangli/eventstore/SqliteJournal`:
 *
 *   CREATE TABLE journal_entries (
 *     stream TEXT NOT NULL, revision INTEGER NOT NULL, request_id TEXT NOT NULL,
 *     body BLOB NOT NULL, checksum TEXT NOT NULL,
 *     PRIMARY KEY (stream, revision), UNIQUE (stream, request_id))
 *
 * Ownership is committed into the SAME journal: `executeOwnershipDurable`
 * appends to the stream `"ownership:" + registryId`
 * (DurableWorkHost.fs:675, :783). There is NO ownership FILE — the registry
 * id is not knowable in advance, so the stream is DISCOVERED, never assumed.
 *
 * Read-only: the smoke never writes to the product's state.
 */
export function readWorkJournal(stateRoot) {
  const journalPath = path.join(stateRoot, "wanxiangshu", "work.sqlite")
  if (!fs.existsSync(journalPath)) {
    return { journalPath, exists: false, error: "journal file does not exist", streams: [], ownershipStreams: [], bodies: [], records: [] }
  }
  let db
  try {
    db = new DatabaseSync(journalPath, { readOnly: true })
  } catch (err) {
    return {
      journalPath,
      exists: true,
      error: `journal could not be opened: ${err?.message ?? String(err)}`,
      streams: [],
      ownershipStreams: [],
      bodies: [],
      records: [],
    }
  }
  try {
    const rows = db
      .prepare("SELECT stream, revision, request_id, body FROM journal_entries ORDER BY stream, revision")
      .all()
    const streams = []
    const ownershipStreams = []
    const bodies = []
    const records = []
    for (const row of rows) {
      const stream = String(row.stream ?? "")
      const body =
        row.body instanceof Uint8Array
          ? Buffer.from(row.body).toString("utf8")
          : typeof row.body === "string"
            ? row.body
            : String(row.body ?? "")
      if (!streams.includes(stream)) streams.push(stream)
      const record = { stream, revision: Number(row.revision), requestId: String(row.request_id ?? ""), body }
      records.push(record)
      if (stream.startsWith("ownership:")) {
        ownershipStreams.push({ stream, revision: record.revision, requestId: record.requestId, body })
      }
      bodies.push(body)
    }
    return { journalPath, exists: true, error: null, streams, ownershipStreams, bodies, records }
  } catch (err) {
    return {
      journalPath,
      exists: true,
      error: `journal could not be read: ${err?.message ?? String(err)}`,
      streams: [],
      ownershipStreams: [],
      bodies: [],
      records: [],
    }
  } finally {
    try {
      db.close()
    } catch {
      /* closing a read-only handle must never break the smoke */
    }
  }
}

/**
 * The product's deterministic work id.
 *
 * `HostPlugin.workIdOf seed = "host2-" + sha256Hex(seed).Substring(0, 32)`
 * (src/Wanxiangshu/Host/SecondHost/HostPlugin.fs:88-89) and the durable
 * work of a session is committed on the stream named by that work id.
 * The smoke therefore knows, WITHOUT reading the product's source at
 * runtime, which journal stream this run's own child session must have
 * produced — a per-session id cannot be produced by an earlier run.
 */
export function productWorkIdOf(seed) {
  return "host2-" + crypto.createHash("sha256").update(String(seed), "utf8").digest("hex").slice(0, 32)
}

/**
 * The pre-run journal snapshot.
 *
 * Ownership is committed INTO the work journal and the product's activation
 * is IDEMPOTENT (a re-opening of the same scope is observed, not re-written),
 * so "the newest ownership record" alone cannot prove that THIS run wrote
 * anything. The baseline is what makes the scoping honest: it records, per
 * stream, the highest revision that existed BEFORE the host started.
 */
export function snapshotJournal(workspace) {
  let stateRoot = null
  try {
    stateRoot = resolveProductStateRoot(workspace)
  } catch {
    return { stateRoot: null, streams: {}, error: "state root could not be resolved" }
  }
  const journal = readWorkJournal(stateRoot)
  const streams = {}
  for (const record of journal.records ?? []) {
    const current = Number(streams[record.stream] ?? 0)
    if (Number(record.revision) > current) streams[record.stream] = Number(record.revision)
  }
  // An ABSENT journal is a valid baseline: nothing existed before this run.
  // Only an UNREADABLE one is an error, and an error fails the facts closed.
  const absent = journal.exists === false
  return {
    stateRoot,
    streams,
    error: absent ? null : journal.error ?? null,
    absent,
  }
}

/**
 * @param {object} input
 * @param {string} input.observerOut     observer JSONL of THIS run
 * @param {string} input.sessionsRoot    omp session store root
 * @param {string[]} input.newSessionFiles session files created during THIS run
 * @param {string} input.parentSessionId the host session id of THIS run's parent
 * @param {string} input.nonce           the run-specific random nonce
 * @param {string} input.workspace       workspace root
 * @param {object} [input.journalBaseline] pre-run journal snapshot (per-stream max revision)
 * @param {string} [input.childSessionId] the id of the child session THIS run created
 */
export function assertSmoke({
  observerOut,
  sessionsRoot,
  newSessionFiles,
  parentSessionId,
  nonce,
  workspace,
  repo = REPO,
  toolPattern = /^wxs_deliver$/,
  journalBaseline = null,
  childSessionId = null,
}) {
  const failures = []
  const notes = []
  const check = (name, condition, detail) => {
    if (condition) notes.push(`ok   ${name}${detail ? ` — ${detail}` : ""}`)
    else failures.push(`FAIL ${name}${detail ? ` — ${detail}` : ""}`)
    return condition
  }

  const observed = readJsonl(observerOut)
  const byKind = (kind) => observed.filter((o) => o.kind === kind)
  const providerPayloads = byKind("before_provider_request").map((o) => o.payload).filter(Boolean)
  const turnEnds = byKind("turn_end")
  const toolStarts = byKind("tool_execution_start")
  const toolEnds = byKind("tool_execution_end")
  const sessionStarts = byKind("session_start")

  // The provider payload is parsed BEFORE the facts, because FACT 1 needs a
  // PRODUCT artifact, not merely "an extension loaded".
  const split = providerPayloads.map((p) => splitProviderPayload(p))
  const systemText = split.map((s) => s.system).join("\n")
  const toolText = split.flatMap((s) => s.tools).map((t) => t.description).join("\n")
  const allToolNames = split.flatMap((s) => s.tools).map((t) => t.name)
  const productTools = allToolNames.filter((n) => typeof n === "string" && toolPattern.test(n))

  // ── FACT 1 — the PRODUCT bundle really loaded into a real second host ─────
  // A session_start event only proves that SOME extension loaded — the
  // observer extension is always loaded, so it can never satisfy FACT 1 on
  // its own. The product artifact is the PRODUCT TOOL, advertised by the
  // host to the provider: only the product bundle registers it. A bundle
  // that loads but is INERT (a default-exported function that does nothing)
  // registers nothing, and an unrelated built-in tool (`task`, …) is not the
  // product tool either.
  check("host session started", sessionStarts.length > 0, `${sessionStarts.length} session_start event(s)`)
  check(
    "host loaded the product bundle (product tool advertised to the provider)",
    productTools.length > 0,
    productTools.join(",") || `no tool matching ${toolPattern} was advertised by the host`,
  )
  if (workspace) {
    const hostCwd = sessionStarts[0]?.cwd
    check("host cwd is the requested workspace", hostCwd === path.resolve(workspace), `${hostCwd} vs ${path.resolve(workspace)}`)
  }
  if (parentSessionId) {
    const observedParent = sessionStarts.map((s) => s.sessionId).find(Boolean)
    check("host parent session id matches the run", observedParent === parentSessionId, `${observedParent} vs ${parentSessionId}`)
  }

  // ── FACT 2 — a real provider request really happened ────────────────────────
  check("real provider request observed", providerPayloads.length > 0, `${providerPayloads.length} before_provider_request payload(s)`)
  check(
    "real provider turn completed",
    turnEnds.some((t) => t.role === "assistant" && !!t.stopReason),
    turnEnds.map((t) => `${t.role}/${t.stopReason}`).join(",") || "none",
  )

  // ── FACT 3 — the product's paired-bilingual resources reached the provider ──
  // The system prompt and the tool descriptions are inspected; the user prompt
  // is NOT part of either, so a bilingual prompt cannot fake this.
  const markers = loadResourceMarkers(repo)
  check("product resource markers loaded", markers.en.length > 0 && markers.zh.length > 0, `en="${markers.en.slice(0, 40)}…" zh="${markers.zh.slice(0, 20)}…"`)

  check(
    "system prompt carries the product's paired-bilingual resource",
    systemText.includes(markers.en) && systemText.includes(markers.zh),
    `en=${systemText.includes(markers.en)} zh=${systemText.includes(markers.zh)}`,
  )
  check("provider request advertised a product tool", productTools.length > 0, productTools.join(",") || "none")
  const bilingualTool = split
    .flatMap((s) => s.tools)
    .find((t) => toolPattern.test(t.name) && /[\u4e00-\u9fff]/.test(t.description) && /[A-Za-z]{4,}/.test(t.description))
  check("product tool description is paired-bilingual", !!bilingualTool, bilingualTool ? bilingualTool.name : "no product tool with both CJK and Latin description")
  // the prompt must not be the source of the bilingual text
  const promptText = byKind("before_agent_start").map((o) => o.prompt ?? "").join("\n")
  if (promptText) {
    check(
      "bilingual text is not merely the user prompt",
      !(promptText.includes(markers.en) || promptText.includes(markers.zh)),
      "prompt does not carry the product resource markers",
    )
  }

  // ── FACT 4 — fork: a REAL child session of THIS run ────────────────────────
  const files = Array.isArray(newSessionFiles) ? newSessionFiles : []
  let child = null
  for (const file of files) {
    const entries = readJsonl(file)
    const header = entries.find((e) => e.type === "session")
    if (!header) continue
    if (parentSessionId && header.parentSession !== parentSessionId) continue
    if (!header.parentSession) continue
    child = { file, header, entries }
    break
  }
  check(
    "real child session forked from this run's parent",
    !!child,
    child ? `${child.file} parent=${child.header.parentSession}` : `${files.length} new session file(s), none parented to ${parentSessionId}`,
  )

  // ── FACT 4b — deliver: the child really received the delivered nonce ───────
  let childUserText = ""
  let childAssistantText = ""
  if (child) {
    for (const e of child.entries) {
      if (e.type !== "message") continue
      const m = e.message
      const text = Array.isArray(m?.content) ? m.content.filter((c) => c?.type === "text").map((c) => c.text).join("\n") : ""
      if (!text) continue
      if (m.role === "user") childUserText += `${text}\n`
      if (m.role === "assistant") childAssistantText += `${text}\n`
    }
  }
  check("delivered prompt reached the child session", childUserText.length > 0, childUserText.slice(0, 120))
  check(
    "child user message contains the delivered nonce",
    !!nonce && childUserText.includes(nonce),
    nonce ? `nonce=${nonce} in child user message=${childUserText.includes(nonce)}` : "no nonce supplied",
  )
  check("child produced a real reply", childAssistantText.length > 0, childAssistantText.slice(0, 120))

  // ── FACT 4c — join: the parent observed the child's completion ─────────────
  const productToolEnds = toolEnds.filter((t) => typeof t.toolName === "string" && toolPattern.test(t.toolName))
  check("product tool executed in the host", toolStarts.some((t) => typeof t.toolName === "string" && toolPattern.test(t.toolName)), toolStarts.map((t) => t.toolName).join(",") || "none")
  check("product tool execution completed", productToolEnds.length > 0, productToolEnds.map((t) => `${t.toolName}:${t.isError ? "error" : "ok"}`).join(",") || "none")
  check(
    "no product tool call errored",
    productToolEnds.every((t) => t.isError !== true),
    productToolEnds.map((t) => `${t.toolName}:${t.isError ? "error" : "ok"}`).join(",") || "none",
  )
  const joinedText = productToolEnds.map((t) => t.text ?? "").join("\n")
  check(
    "parent observed the join result carrying the nonce",
    !!nonce && joinedText.includes(nonce),
    nonce ? `nonce in join result=${joinedText.includes(nonce)}` : "no nonce supplied",
  )

  // ── FACT 2b — Work / Ownership are real on-disk artifacts ─────────────────
  // The state root is resolved the way the PRODUCT resolves it; when git
  // cannot resolve the topology the resolution FAILS CLOSED (no guessed path).
  let stateRoot = null
  let stateRootError = null
  try {
    stateRoot = productStateRoot(workspace)
  } catch (err) {
    stateRootError = err?.message ?? String(err)
  }
  check("product state root could be resolved", !!stateRoot, stateRoot ?? stateRootError ?? "unresolved")
  const stateRootExists = !!stateRoot && fs.existsSync(stateRoot) && fs.statSync(stateRoot).isDirectory()
  check("product state root exists on disk", stateRootExists, stateRoot ?? stateRootError ?? "unresolved")
  const workJournal = stateRoot ? path.join(stateRoot, "wanxiangshu", "work.sqlite") : ""
  check("durable Work journal exists on disk", !!workJournal && fs.existsSync(workJournal), workJournal || stateRootError || "unresolved")

  // The journal is READ, never scanned by name: Ownership is committed INTO
  // the same sqlite journal (stream "ownership:<registryId>"), and the
  // registry id is not knowable in advance — so the stream is DISCOVERED.
  const journal = stateRoot ? readWorkJournal(stateRoot) : { error: "no state root", ownershipStreams: [], bodies: [], streams: [], records: [] }
  check(
    "durable Work journal could be read",
    !!journal && !journal.error,
    journal?.error ?? `${journal?.ownershipStreams.length ?? 0} ownership record(s)`,
  )
  const ownershipStreams = journal?.ownershipStreams ?? []

  // The scope the product opens for THIS workspace: `host-activation:` +
  // the project id, and the omp adapter's project id IS the workspace
  // (HostPlugin.fs:159, Adapter.fs:838-840).
  const expectedScopeIds = new Set()
  if (workspace) expectedScopeIds.add(`host-activation:${workspace}`)
  const workspacePath = workspace ? path.resolve(workspace) : ""
  if (workspacePath) expectedScopeIds.add(`host-activation:${workspacePath}`)

  const scopeIdOf = (body) => {
    if (!body) return null
    try {
      const parsed = JSON.parse(body)
      const message = parsed?.message ?? parsed
      return typeof message?.scopeId === "string" ? message.scopeId : null
    } catch {
      const match = body.match(/"scopeId"\s*:\s*"([^"]+)"/)
      return match ? match[1] : null
    }
  }

  // An ownership stream for ANOTHER scope proves nothing about this run.
  const boundOwnership = ownershipStreams.filter((o) => expectedScopeIds.has(scopeIdOf(o.body)))
  check(
    "Ownership registry is committed in the Work journal",
    boundOwnership.length > 0,
    boundOwnership.length > 0
      ? boundOwnership.map((o) => `${o.stream}@${o.revision}`).join(",")
      : ownershipStreams.length > 0
        ? `no ownership record opens ${[...expectedScopeIds].join(" | ")} (found ${ownershipStreams.map((o) => o.stream).join(",")})`
        : `no ownership:* stream in ${journal?.journalPath ?? workJournal}`,
  )
  const ownershipBodies = boundOwnership.map((o) => o.body ?? "")
  check(
    "Ownership registry carries a committed record",
    ownershipBodies.some((b) => b.trim().length > 0),
    ownershipBodies.map((b) => b.slice(0, 80)).join(" | ") || "empty ownership records",
  )

  // ── run scoping ───────────────────────────────────────────────────────────
  // "an ownership:* stream exists" is NOT evidence: an earlier run of the
  // same product in the same workspace left one behind, and a workspace
  // that never ran the product at all can carry one from another project.
  //
  // Two facts scope the evidence to THIS run:
  //
  //  1. The ownership scope the product opens is
  //     `host-activation:<projectId>` (HostPlugin.fs:159), and the omp
  //     adapter's project id IS the workspace it was started in
  //     (Adapter.fs:838-840). So the scope id inside the ownership record
  //     names THIS run's workspace.
  //
  //  2. The product opens a durable work whose stream name is derived from
  //     the CHILD SESSION id (HostPlugin.fs:230,
  //     `workIdOf (Identity.SessionId.value childSession)`). The child
  //     session id is fresh for every run, so that stream — and the
  //     revision it carries — cannot have been written by an earlier run.
  //     A PRE-RUN baseline of the journal makes "written by this run" a
  //     fact rather than an assumption.
  const baseline = journalBaseline ?? null
  const hasBaseline = !!baseline && !baseline.error
  const baselineStreams = baseline?.streams ?? {}
  const records = journal?.records ?? []
  const isNew = (record) => Number(record.revision) > Number(baselineStreams[record.stream] ?? 0)

  check(
    "Ownership scope is bound to THIS run's workspace",
    boundOwnership.length > 0,
    boundOwnership.length > 0
      ? boundOwnership.map((o) => `${scopeIdOf(o.body)}@${o.revision}`).join(",")
      : `no ownership record opens ${[...expectedScopeIds].join(" | ")}`,
  )

  // THIS run's own work stream: derived from the child session id the HOST
  // created during this run, with the product's own derivation.
  const runWorkSeed = child?.header?.id ?? childSessionId ?? null
  const runWorkId = runWorkSeed ? productWorkIdOf(runWorkSeed) : null
  const runWorkRecords = runWorkId ? records.filter((r) => r.stream === runWorkId) : []
  // Without a pre-run baseline NOTHING can be proven to be this run's, so
  // freshness is undecidable — and an undecidable fact FAILS (fail closed).
  const runWorkFresh = hasBaseline && runWorkRecords.some(isNew)
  check(
    "durable Work stream for THIS run's child session exists",
    runWorkRecords.length > 0,
    runWorkId ? `${runWorkId}@${runWorkRecords.map((r) => r.revision).join(",") || "absent"}` : "no child session was identified",
  )
  check(
    "durable Work stream for THIS run's child session was written by THIS run",
    runWorkFresh,
    runWorkRecords.length === 0
      ? "no such stream in the journal"
      : runWorkFresh
        ? `${runWorkId}@${runWorkRecords.map((r) => r.revision).join(",")} is newer than the pre-run snapshot`
        : `${runWorkId} predates this run (pre-run revision ${baselineStreams[runWorkId] ?? 0})`,
  )

  // The ownership record itself is idempotent: re-opening an already open
  // scope is OBSERVED, not re-written. So a fresh ownership revision is
  // strong evidence, but its ABSENCE is only weak evidence — the run is
  // proven by its own (never repeatable) child work stream.
  const freshOwnership = hasBaseline ? ownershipStreams.filter(isNew) : []
  const freshBoundOwnership = freshOwnership.filter((o) => expectedScopeIds.has(scopeIdOf(o.body)))
  check(
    "Ownership registry was committed by THIS run",
    hasBaseline && boundOwnership.length > 0 && (freshBoundOwnership.length > 0 || runWorkFresh),
    !hasBaseline
      ? "no pre-run journal baseline — ownership cannot be scoped to this run"
      : freshBoundOwnership.length > 0
        ? freshBoundOwnership.map((o) => `${o.stream}@${o.revision}`).join(",")
        : runWorkFresh
          ? `ownership scope re-opened idempotently; this run's own work stream ${runWorkId} is new`
          : boundOwnership.length > 0
            ? `ownership records exist but predate this run (${boundOwnership.map((o) => `${o.stream}@${o.revision}`).join(",")})`
            : "no ownership record for this run's scope",
  )

  if (nonce) {
    // The run's identifier in the journal. The product's durable work
    // records carry the work id (and the workspace root), not the
    // delivered prompt, so the identifier the journal can prove is the
    // run's OWN work stream. A nonce left in the journal by an earlier
    // run proves nothing about this one and must not satisfy the check.
    const freshWithNonce = hasBaseline ? records.filter((r) => r.body.includes(nonce) && isNew(r)) : []
    const journalHasNonce = records.some((r) => r.body.includes(nonce))
    check(
      "Work journal carries this run's identifier",
      runWorkFresh || freshWithNonce.length > 0,
      runWorkFresh
        ? `run work stream ${runWorkId} carries this run's identity`
        : freshWithNonce.length > 0
          ? `nonce ${nonce} found in ${freshWithNonce.map((r) => `${r.stream}@${r.revision}`).join(",")}`
          : !hasBaseline
            ? `nonce ${nonce} cannot be scoped to this run: no pre-run journal baseline`
            : journalHasNonce
              ? `nonce ${nonce} is present but only in records that predate this run`
              : `nonce ${nonce} not found in ${journal?.journalPath} and this run wrote no work stream`,
    )
  }


  return {
    result: failures.length === 0 ? "PASS" : "FAIL",
    failures,
    notes,
    observations: {
      sessionStarts: sessionStarts.length,
      providerPayloads: providerPayloads.length,
      turnEnds: turnEnds.length,
      toolStarts: toolStarts.length,
      toolEnds: toolEnds.length,
      productTools,
      journalStreams: journal?.streams ?? [],
      ownershipStreams: (journal?.ownershipStreams ?? []).map((o) => `${o.stream}@${o.revision}`),
    },
    childSession: child ? { file: child.file, id: child.header.id, parentSession: child.header.parentSession } : null,
    childUserText: childUserText.slice(0, 500),
    childAssistantText: childAssistantText.slice(0, 500),
    joinText: joinedText.slice(0, 500),
    stateRoot,
    workJournal,
    journal: {
      path: journal?.journalPath ?? null,
      error: journal?.error ?? null,
      streams: journal?.streams ?? [],
      ownershipStreams: (journal?.ownershipStreams ?? []).map((o) => ({
        stream: o.stream,
        revision: o.revision,
        requestId: o.requestId,
        body: (o.body ?? "").slice(0, 500),
      })),
    },
  }
}

// ── CLI mode ─────────────────────────────────────────────────────────────────
if (process.argv[1] && process.argv[1].endsWith("assert-smoke.mjs")) {
  const args = process.argv.slice(2)
  const arg = (name, fallback) => {
    const i = args.indexOf(name)
    return i >= 0 ? args[i + 1] : fallback
  }
  const observerOut = arg("--observer", path.join(os.tmpdir(), "cut36-omp-observer.jsonl"))
  const sessionsRoot = arg("--sessions", path.join(os.homedir(), ".omp", "agent", "sessions"))
  const workspace = arg("--workspace", undefined)
  const summaryPath = arg("--summary", path.join(os.tmpdir(), "cut36-omp-smoke-summary.json"))
  const summary = readJson(summaryPath)
  const newSessionFiles = arg("--new-sessions") ? arg("--new-sessions").split(",").filter(Boolean) : (summary?.newSessionFiles ?? [])
  const parentSessionId = arg("--parent-session", summary?.parentSessionId)
  const nonce = arg("--nonce", summary?.nonce)
  // The pre-run baseline comes from the summary the runner wrote; without it
  // the run-scoped facts are undecidable and FAIL closed.
  const journalBaseline = summary?.journalBaseline ?? null
  const verdict = assertSmoke({ observerOut, sessionsRoot, workspace, nonce, parentSessionId, newSessionFiles, journalBaseline })
  for (const n of verdict.notes) console.log(n)
  for (const f of verdict.failures) console.log(f)
  console.log(`CUT-36 real smoke (re-check): ${verdict.result}`)
  process.exit(verdict.result === "PASS" ? 0 : 1)
}
