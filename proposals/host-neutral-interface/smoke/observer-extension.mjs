// CUT-36 smoke observer — a SMOKE-OWNED, read-only omp extension.
//
// Purpose: observe the REAL host, never trust the product's self-report.
// It records, as JSON lines, the raw facts the smoke assertions need:
//   - every session_start (id + cwd + agent identity) -> fork/child identity, from the host
//   - every turn_end / agent_end                       -> real provider turns
//   - every before_provider_request payload            -> the REAL provider payload
//     (system prompt + tool descriptions) for the bilingual assertion
//   - every tool_execution_start/end                   -> real tool calls by name
//
// It writes nothing into product state and takes no product decision.
// Output file: $OMP_SMOKE_OBSERVER_OUT (JSONL). If unset, the observer is inert.

import { appendFileSync } from "node:fs"

const OUT = process.env.OMP_SMOKE_OBSERVER_OUT

function record(kind, payload) {
  if (!OUT) return
  try {
    appendFileSync(OUT, JSON.stringify({ kind, pid: process.pid, at: Date.now(), ...payload }) + "\n")
  } catch {
    /* observation must never break the host */
  }
}

export default function observeOmp(pi) {
  record("extension_loaded", { hasOn: typeof pi?.on === "function" })

  pi.on("session_start", async (_event, ctx) => {
    record("session_start", {
      sessionId: ctx?.sessionManager?.getSessionId?.(),
      cwd: ctx?.cwd,
      model: ctx?.model?.id,
      provider: ctx?.model?.provider,
      agent: ctx?.agent,
    })
  })

  pi.on("before_provider_request", async (event) => {
    record("before_provider_request", { payload: event?.payload })
  })

  pi.on("before_agent_start", async (event) => {
    record("before_agent_start", {
      prompt: event?.prompt,
      systemPrompt: event?.systemPrompt,
    })
  })

  pi.on("turn_end", async (event) => {
    const m = event?.message
    record("turn_end", {
      role: m?.role,
      provider: m?.provider,
      model: m?.model,
      stopReason: m?.stopReason,
      text: Array.isArray(m?.content)
        ? m.content.filter((c) => c?.type === "text").map((c) => c.text).join("\n").slice(0, 2000)
        : undefined,
    })
  })

  pi.on("agent_end", async (event) => {
    record("agent_end", { isTerminal: event?.isTerminal })
  })

  pi.on("tool_execution_start", async (event) => {
    record("tool_execution_start", { toolName: event?.toolName, toolCallId: event?.toolCallId })
  })

  pi.on("tool_execution_end", async (event) => {
    const result = event?.result
    record("tool_execution_end", {
      toolName: event?.toolName,
      toolCallId: event?.toolCallId,
      isError: event?.isError,
      text: Array.isArray(result?.content)
        ? result.content.filter((c) => c?.type === "text").map((c) => c.text).join("\n").slice(0, 2000)
        : undefined,
    })
  })

  pi.on("session_shutdown", async () => {
    record("session_shutdown", {})
  })
}
