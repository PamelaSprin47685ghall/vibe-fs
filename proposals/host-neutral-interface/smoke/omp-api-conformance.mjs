#!/usr/bin/env node
// CUT-36 — omp 18.12.4 API CONFORMANCE PROBE.
//
// harness conformance probe — NOT the CUT-36 canary; it does not load the
// product and proves nothing about the product.
//
// It is loaded INTO a real `omp` process as an extension (`omp -e <this file>`)
// and answers one question: does every omp API member the F# adapter
// (src/Wanxiangshu/Omp/Adapter.fs) calls actually EXIST on this omp build, and
// does it behave the way the adapter assumes?
//
// It is OPT-IN and UNWIRED: it is not imported by npm test, by check.mjs, or by
// any gate. It writes one JSONL event stream ($OMP_CONFORMANCE_OUT.jsonl) and
// one JSON summary ($OMP_CONFORMANCE_OUT), and prints one PASS/FAIL line per
// check to stderr.
//
// Usage:
//   OMP_CONFORMANCE_OUT=/tmp/out.json \
//   omp -p "Call the wxs_conformance_probe tool exactly once with {} and then reply DONE." \
//        -e proposals/host-neutral-interface/smoke/omp-api-conformance.mjs \
//        --cwd /tmp/probe-ws --mode json
//
// It is a harness probe: a FAIL here means the ADAPTER's assumptions are
// wrong for this omp version, not that the product is broken.

import { appendFileSync, writeFileSync } from "node:fs"

const OUT = process.env.OMP_CONFORMANCE_OUT ?? ""
const TOOL_NAME = "wxs_conformance_probe"

const checks = []
const facts = {}

function appendLine(obj) {
  if (!OUT) return
  try {
    appendFileSync(`${OUT}.jsonl`, JSON.stringify(obj) + "\n")
  } catch {
    /* a probe must never break the host */
  }
}

function record(name, pass, detail) {
  const line = `${pass ? "PASS" : "FAIL"} ${name}${detail ? ` — ${detail}` : ""}`
  checks.push({ name, pass: !!pass, detail: detail ?? "" })
  process.stderr.write(`${line}\n`)
  appendLine({ kind: "check", name, pass: !!pass, detail: detail ?? "" })
}

function note(key, value) {
  facts[key] = value
  appendLine({ kind: "fact", key, value })
}

function arity(fn) {
  return typeof fn === "function" ? fn.length : null
}

function describe(v) {
  if (v === null) return "null"
  if (v === undefined) return "undefined"
  return typeof v
}

// The probe must never hang the host: every awaited SDK call is bounded.
async function withTimeout(promise, ms, label) {
  let timer
  try {
    return await Promise.race([
      promise,
      new Promise((_resolve, reject) => {
        timer = setTimeout(() => reject(new Error(`${label} timed out after ${ms}ms`)), ms)
      }),
    ])
  } finally {
    clearTimeout(timer)
  }
}

let finalized = false
function finalize() {
  if (finalized) return
  finalized = true
  const summary = {
    kind: "summary",
    probe: "omp-api-conformance",
    note: "harness conformance probe — NOT the CUT-36 canary; it does not load the product and proves nothing about the product",
    omp_pid: process.pid,
    checks,
    facts,
    failed: checks.filter((c) => !c.pass).map((c) => c.name),
  }
  const failed = summary.failed.length
  process.stderr.write(
    `${failed === 0 ? "ALL CHECKS PASSED" : "SOME CHECKS FAILED"} (${checks.length} checks, ${failed} failed)\n`,
  )
  if (OUT) {
    try {
      writeFileSync(OUT, JSON.stringify(summary, null, 2) + "\n")
    } catch {
      /* ignore */
    }
  }
}

// The default export IS the factory omp's getExtensionFactory accepts.
function conformanceFactory(pi) {
  note("factory_invoked", { pid: process.pid, at: Date.now() })

  const sdk = pi?.pi
  const observed = { cwd: null }
  const toolResult = { executed: false, toolCallId: null, params: null }
  let advertised = null
  let entriesDone = false

  // ── CHECK 2 — pi.pi SDK access and the members the adapter uses ──────────
  record("pi.pi is an object", typeof sdk === "object" && sdk !== null, `typeof ${describe(sdk)}`)
  const members = {}
  for (const name of ["SessionManager", "createAgentSession", "listSessions", "readSessionInfo", "FileSessionStorage"]) {
    const value = sdk?.[name]
    members[name] = { present: value !== undefined, typeof: describe(value), arity: arity(value) }
  }
  note("sdk_members", members)
  const statics = {}
  for (const name of ["create", "open", "list"]) {
    const value = sdk?.SessionManager?.[name]
    statics[name] = { present: value !== undefined, typeof: describe(value), arity: arity(value) }
  }
  note("session_manager_statics", statics)
  const missing = [
    ...["SessionManager", "createAgentSession", "listSessions", "readSessionInfo", "FileSessionStorage"]
      .filter((n) => !members[n].present),
    ...["create", "open", "list"].filter((n) => !statics[n].present).map((n) => `SessionManager.${n}`),
  ]
  record(
    "every adapter-used SDK member exists",
    missing.length === 0,
    missing.length === 0
      ? Object.entries(members)
          .map(([n, m]) => `${n}:${m.typeof}/${m.arity ?? "-"}`)
          .join(" ")
      : `MISSING ${missing.join(", ")}`,
  )
  record("SessionManager statics create/open/list exist", missing.length === 0, JSON.stringify(statics))

  // ── CHECK 5a — register a tool ──────────────────────────────────────────
  try {
    pi.registerTool({
      name: TOOL_NAME,
      label: TOOL_NAME,
      description: "Conformance probe tool. Takes no arguments. Returns CONFORMANCE_PROBE_OK.",
      parameters: { type: "object", properties: {}, additionalProperties: false },
      loadMode: "essential",
      async execute(toolCallId, params, signal, onUpdate, ctx) {
        toolResult.executed = true
        toolResult.toolCallId = toolCallId
        toolResult.params = params
        note("tool_execute_ctx_keys", ctx ? Object.keys(ctx).sort() : null)
        // CHECK 7 — sendUserMessage is fire-and-forget (returns undefined).
        try {
          const returned = pi.sendUserMessage("conformance probe follow-up; reply OK", { deliverAs: "followUp" })
          note("send_user_message_return", returned === undefined ? "undefined" : describe(returned))
          record("pi.sendUserMessage returns undefined (fire-and-forget)", returned === undefined, `returned ${describe(returned)}`)
        } catch (err) {
          note("send_user_message_error", String(err?.message ?? err))
          record("pi.sendUserMessage returns undefined (fire-and-forget)", false, `threw ${String(err?.message ?? err)}`)
        }
        return { content: [{ type: "text", text: "CONFORMANCE_PROBE_OK" }], details: { conformance: true } }
      },
    })
    record("pi.registerTool accepted the tool definition", true, `name=${TOOL_NAME}`)
  } catch (err) {
    record("pi.registerTool accepted the tool definition", false, String(err?.message ?? err))
  }

  // ── CHECK 7 (static half) — sendUserMessage exists on the API ────────────
  record(
    "pi.sendUserMessage is a function",
    typeof pi?.sendUserMessage === "function",
    `typeof ${describe(pi?.sendUserMessage)} arity ${arity(pi?.sendUserMessage)}`,
  )
  note("send_user_message_typeof", describe(pi?.sendUserMessage))
  note("send_user_message_arity", arity(pi?.sendUserMessage))

  // ── CHECK 6 — transcript entries carry a real id ─────────────────────────
  function checkEntries(ctx, at) {
    try {
      const entries = ctx?.sessionManager?.getEntries?.()
      const list = Array.isArray(entries) ? entries : []
      const firstMessage = list.find((e) => e?.type === "message")
      note(`entries_at_${at}`, list.length)
      note(`entry_types_at_${at}`, list.map((e) => e?.type))
      if (firstMessage) {
        note("first_message_entry_keys", Object.keys(firstMessage).sort())
        note("first_message_entry_id", firstMessage.id ?? null)
        note("first_message_entry_id_length", firstMessage.id ? String(firstMessage.id).length : null)
      }
      note(
        "ctx_session_manager_methods",
        Object.getOwnPropertyNames(Object.getPrototypeOf(ctx?.sessionManager ?? {})).sort(),
      )
      if (!entriesDone && firstMessage) {
        entriesDone = true
        record(
          "transcript entries carry a real id",
          typeof firstMessage.id === "string" && firstMessage.id.length > 0,
          `id=${firstMessage.id}`,
        )
      }
    } catch (err) {
      note("entries_error", String(err?.message ?? err))
    }
  }

  // ── CHECK 5b — the host advertises the tool to the provider ──────────────
  pi.on("before_provider_request", async (event) => {
    const payload = event?.payload
    const list = Array.isArray(payload?.tools) ? payload.tools : []
    const names = list.map((t) => t?.function?.name ?? t?.name).filter(Boolean)
    const entry = list.find((t) => (t?.function?.name ?? t?.name) === TOOL_NAME)
    if (entry) advertised = { entry, names }
    note("provider_payload_top_keys", payload ? Object.keys(payload) : null)
    note("advertised_tool_names", names)
    note(
      "advertised_probe_tool",
      entry ? { keys: Object.keys(entry), innerKeys: entry.function ? Object.keys(entry.function) : null } : null,
    )
    note("provider_request_count", (facts.provider_request_count ?? 0) + 1)
  })

  pi.on("tool_execution_start", async (event) => {
    note("tool_execution_start", { toolName: event?.toolName, toolCallId: event?.toolCallId })
  })

  pi.on("tool_execution_end", async (event) => {
    note("tool_execution_end", { toolName: event?.toolName, isError: event?.isError })
  })

  pi.on("turn_end", async (event, ctx) => {
    checkEntries(ctx, "turn_end")
  })

  // ── CHECK 3 + CHECK 4 — session manager + a real child session ───────────
  async function checkSessions() {
    const cwd = observed.cwd ?? process.cwd()
    note("session_probe_cwd", cwd)

    // CHECK 3a: SessionManager.create
    let parentId = null
    try {
      const manager = sdk.SessionManager.create(cwd)
      parentId = manager.getSessionId()
      note("session_manager_create_id", parentId)
      note("session_manager_create_header", manager.getHeader?.() ?? null)
      note("session_manager_create_session_file", manager.getSessionFile?.() ?? null)
      record(
        "SessionManager.create yields a persisted session id",
        typeof parentId === "string" && parentId.length > 0,
        `id=${parentId}`,
      )
    } catch (err) {
      note("session_manager_create_error", String(err?.message ?? err))
      record("SessionManager.create yields a persisted session id", false, String(err?.message ?? err))
    }

    // CHECK 3b: `SessionManager(dir)` called AS A FUNCTION — the adapter's shape.
    // `createManager` in Adapter.fs does call1(module.SessionManager, dir).
    try {
      const called = sdk.SessionManager(cwd)
      note("session_manager_call_threw", false)
      note("session_manager_call_id", called?.getSessionId?.() ?? null)
      record(
        "SessionManager(dir) called as a function throws (adapter defect B)",
        false,
        `did NOT throw; getSessionId()=${JSON.stringify(called?.getSessionId?.() ?? null)}`,
      )
    } catch (err) {
      note("session_manager_call_threw", true)
      note("session_manager_call_error", String(err?.message ?? err))
      record("SessionManager(dir) called as a function throws (adapter defect B)", true, String(err?.message ?? err))
    }

    // CHECK 3c: `new SessionManager(cwd)` — the TS constructor is private.
    try {
      const constructed = new sdk.SessionManager(cwd)
      note("new_session_manager_threw", false)
      note("new_session_manager_id", constructed?.getSessionId?.() ?? null)
      note("new_session_manager_header", constructed?.getHeader?.() ?? null)
      record(
        "new SessionManager(cwd) yields a USABLE session id",
        typeof constructed?.getSessionId?.() === "string" && constructed.getSessionId().length > 0,
        `did not throw; getSessionId()=${JSON.stringify(constructed?.getSessionId?.() ?? null)}`,
      )
    } catch (err) {
      note("new_session_manager_threw", true)
      note("new_session_manager_error", String(err?.message ?? err))
      record("new SessionManager(cwd) yields a USABLE session id", true, `threw ${String(err?.message ?? err)}`)
    }

    if (!parentId) {
      record("child session records the parent id", false, "no parent session id")
      return
    }

    // CHECK 4a: newSession({parentSession}) on a manager.
    try {
      const manager = sdk.SessionManager.create(cwd)
      const newSessionResult = await withTimeout(manager.newSession({ parentSession: parentId }), 20000, "newSession")
      note("new_session_result", newSessionResult ?? null)
      const headerAfter = manager.getHeader?.() ?? null
      note("manager_header_after_new_session", headerAfter)
      note("manager_header_parent_after_new_session", headerAfter?.parentSession ?? null)
      const childId = manager.getSessionId?.() ?? null
      note("child_session_id", childId)
      note("child_session_header_keys", headerAfter ? Object.keys(headerAfter).sort() : null)
      note("child_session_file", manager.getSessionFile?.() ?? null)
      record(
        "child session records the parent id",
        !!childId && headerAfter?.parentSession === parentId,
        `child=${childId} header.parentSession=${headerAfter?.parentSession} parent=${parentId}`,
      )
    } catch (err) {
      note("child_session_error", String(err?.message ?? err))
      record("child session records the parent id", false, String(err?.message ?? err))
    }

    // CHECK 4b: the SDK entry the adapter uses to build a child agent session.
    try {
      const manager = sdk.SessionManager.create(cwd)
      await withTimeout(manager.newSession({ parentSession: parentId }), 20000, "newSession")
      const created = await withTimeout(
        sdk.createAgentSession({ cwd, sessionManager: manager, disableExtensionDiscovery: true }),
        30000,
        "createAgentSession",
      )
      const session = created?.session
      const childId = session?.sessionManager?.getSessionId?.() ?? null
      const header = session?.sessionManager?.getHeader?.() ?? null
      note("child_agent_session_result_keys", created ? Object.keys(created).sort() : null)
      note("child_agent_session_id", childId)
      note("child_agent_session_header_parent", header?.parentSession ?? null)
      record(
        "createAgentSession({cwd, sessionManager}) yields a session with the parent link",
        !!childId && header?.parentSession === parentId,
        `child=${childId} header.parentSession=${header?.parentSession} parent=${parentId}`,
      )
    } catch (err) {
      note("child_agent_session_error", String(err?.message ?? err))
      record(
        "createAgentSession({cwd, sessionManager}) yields a session with the parent link",
        false,
        String(err?.message ?? err),
      )
    }
  }

  pi.on("session_start", async (_event, ctx) => {
    observed.cwd = ctx?.cwd ?? observed.cwd
    note("session_start_cwd", ctx?.cwd ?? null)
    note("session_start_session_id", ctx?.sessionManager?.getSessionId?.() ?? null)
    checkEntries(ctx, "session_start")
    // The SDK session work is slow (createAgentSession runs tool-capability
    // discovery); start it here so it overlaps the model turn instead of being
    // cut off when the host exits.
    void checkSessions().catch((err) => note("session_probe_error", String(err?.message ?? err)))
  })

  pi.on("agent_end", async (_event, ctx) => {
    observed.cwd = ctx?.cwd ?? observed.cwd
    checkEntries(ctx, "agent_end")
    record(
      "host advertised the probe tool to the provider",
      !!advertised,
      advertised
        ? `payload.tools[].function.name includes ${TOOL_NAME} (${advertised.names.length} tools: ${advertised.names.join(",")})`
        : "tool not found in any provider payload",
    )
    record(
      "probe tool executed",
      toolResult.executed,
      `toolCallId=${toolResult.toolCallId} params=${JSON.stringify(toolResult.params)}`,
    )
    note("tool_executed", toolResult)
  })

  pi.on("session_shutdown", async () => {
    if (!entriesDone) {
      record("transcript entries carry a real id", false, "no message entry observed in any session phase")
    }
  })

  process.on("exit", finalize)
}

export default conformanceFactory
