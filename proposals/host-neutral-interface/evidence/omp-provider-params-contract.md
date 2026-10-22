# O-2: Provider Params Encode Contract Analysis & Implementation Blockers

**Canonical Rule:** Rule O-2 — "provider params encode is adapter-only, per provider family, fail-closed",
as decided in `proposals/host-neutral-interface/evidence/omp-feasibility.md`, section **"## Decided rules (was: open items)"**,
subsection **"### O-2 — provider params encode is adapter-only, per provider family, fail-closed"** (decision text
and **acceptance 4**: "`ProviderParams.Extra` reaches the payload byte-for-byte (the exact `CanonicalJsonText`
string appears unchanged in the encoded payload)").
**Status:** Blocked on contradictory wire contracts and missing host-isolation primitives. `HostCapability.ProviderParams` remains **unadvertised** by the omp adapter.

**Provenance.** This analysis was produced by *reading the installed `@oh-my-pi` sources*, not by executing a
live provider request against a running omp host. No wire payload was ever emitted, so no conclusion below is
backed by an observed round-trip; the traces are source-level facts only. Because the fail-closed guarantees of
O-2 could not be exercised end-to-end, `HostCapability.ProviderParams` stays **unadvertised** by the omp adapter.

---

## 1. Verified Installed Source Traces & Wire Schemas

Analysis of installed `@oh-my-pi/pi-ai` and `@oh-my-pi/pi-coding-agent` in the local environment reveals the exact wire payload structures and dispatch semantics:

### 1.1 Wire Payload Shapes by Family
* **`openai-completions`** (`@oh-my-pi/pi-ai/src/providers/openai-completions.ts`):
  * `onPayload` receives `params: OpenAICompletionsParams`.
  * Sampling controls: `params.temperature`, `params.top_p`, `params.reasoning_effort`.
  * Token clamping: Uses either `params.max_tokens` or `params.max_completion_tokens` (for reasoning/o-series models). The adapter cannot unconditionally overwrite or delete either property without breaking model compat. If neither field exists on incoming `params`, setting an ambiguous token field without schema knowledge is unsafe.
* **`openai-responses`** (`@oh-my-pi/pi-ai/src/providers/openai-responses.ts:702-717, 854`):
  * `onPayload` receives `requestParams: OpenAIResponsesSamplingParams`.
  * Sampling controls: `requestParams.temperature`, `requestParams.top_p`.
  * Max output tokens: Uses `requestParams.max_output_tokens` (distinct from completions `max_tokens`).
  * Reasoning effort: Structured as `requestParams.reasoning: { effort?: string }`, not flat `reasoning_effort`.
* **`anthropic-messages`** (`@oh-my-pi/pi-ai/src/providers/anthropic.ts:2631, 332-345`):
  * `onPayload` receives `nextParams: MessageCreateParamsStreaming`.
  * Sampling controls: `nextParams.temperature`, `nextParams.top_p`, `nextParams.max_tokens`.
  * Reasoning: Anthropic requires structured `thinking: { type: "enabled", budget_tokens: number }`. A flat string reasoning effort has no wire representation and must fail whole.
* **`google-generative-ai`** (`@oh-my-pi/pi-ai/src/providers/google-shared.ts:1047, 895-926, 1175-1205`):
  * `onPayload` receives `params: GenerateContentParameters`.
  * Sampling controls reside inside `params.config`: `config.temperature`, `config.topP`, `config.maxOutputTokens`.
  * These are packaged into `body.generationConfig` by `paramsToWireBody`. Flat string reasoning is unsupported.
* **Unproven / Composite APIs**:
  * OpenRouter is not an independent wire family in `KnownApi` (it is handled as an OpenAI-compatible routing target).
  * Grouping distinct APIs into composite umbrellas without distinct wire handlers violates family isolation.

---

## 2. Core Implementation Blockers

### 2.1 Contradiction: Raw `CanonicalJsonText Extra` Has No Wire Slot
* **Contract Specification:** the canonical rule (see the header above: `omp-feasibility.md` → "## Decided rules" →
  "### O-2 — provider params encode is adapter-only, per provider family, fail-closed") mandates:
  > "the port carries a provider payload only as `ProviderParams.Extra` (`CanonicalJsonText`), which the adapter passes through byte-for-byte"
  > "preserve CanonicalJsonText Extra raw string per contract (never parse/stringify raw Extra)"
* **SDK Reality:** All provider transports in `@oh-my-pi/pi-ai` serialize payloads using `JSON.stringify(params)` over typed JavaScript objects.
* **The Contradiction:**
  1. No provider exposes an opaque raw string wire slot or bypass property (e.g. `raw_body`). *(static — observed in the installed sources)*
  2. Attaching `CanonicalJsonText` as a string property on the payload object would hand the upstream API a parameter it cannot map, because the provider schemas enumerate their own parameter sets. *(static — derived from the schemas; **not exercised** against a live provider.)*
  3. If the adapter parses the JSON string to merge its fields, it violates the inviolable contract requirement: *never parse/stringify raw Extra*.
  4. Returning a raw string from `onPayload` would not satisfy the handler's expected result type. *(static — derived from the handler signature; **not exercised** against a live provider.)*


* **Rule citation:** every reference to the canonical O-2 decision above points at the in-repo source
  `proposals/host-neutral-interface/evidence/omp-feasibility.md` (heading "## Decided rules (was: open items)",
  subsection "### O-2 — provider params encode is adapter-only, per provider family, fail-closed"), by heading and
  quoted text rather than by line number, because the upstream file is 23 KB and its line numbers drift.

### 2.2 Exception Swallowing Prevents Fail-Closed by Throwing
* **SDK Trace:**
  * File: `~/.local/share/omp-task/vibe/node_modules/@oh-my-pi/pi-coding-agent/src/extensibility/extensions/runner.ts:1575-1623` (`#runHandlerWithTimeout`)
  * When a handler throws, `#runHandlerWithTimeout` catches the error, emits it via `this.emitError()`, and returns `undefined`.
  * In `runner.ts:2130-2132` (`emitBeforeProviderRequest`):
    ```ts
    const handlerResult = await this.#runHandlerWithTimeout(...);
    if (handlerResult !== undefined) {
        currentPayload = handlerResult;
    }
    ```
  * **Consequence (static):** if the adapter throws an F# exception upon encountering an unmappable parameter or unsupported family, the runner catches it, emits it, and **dispatches the original, unmodified provider request**. On the source trace, that would breach fail-closed semantics. *(static — read from `runner.ts`; **not exercised** against a live provider.)*

### 2.3 `ctx.abort()` Triggers Session-Wide Abort, Not Request-Local Cancellation
* **SDK Trace:**
  * File: `runner.ts:1440` $\to$ `extension-ui-controller.ts:248` $\to$ `agent-session.ts:10179-10245`.
  * `ctx.abort()` calls `this.ctx.session.abort({ reason: USER_INTERRUPT_LABEL })`.
  * This triggers a full turn shutdown: `#titleGenerationAbortController.abort()`, `this.agent.abort()`, advisory card drainage, and background task cancellation.
  * **Consequence:** There is no per-request cancellation signal or request-level rejection mechanism in omp's extension API. Invoking `ctx.abort()` terminates the user's ongoing session rather than failing only the unmappable provider request.

---

## 3. Required Host / Port Contract Enhancements

To make Rule O-2 safely implementable without violating fail-closed guarantees:

1. **Host-Level Hook Return Protocol for Rejection:**
   * `before_provider_request` handlers in omp must support returning an explicit refusal result (e.g. `{ cancel: true, reason: string }` or throwing an unhandled rejection that halts the dispatch pipeline) rather than swallowing errors and sending the unadjusted request.
2. **Request-Scoped Cancellation Signal:**
   * The handler context must expose an isolated abort token for the current HTTP attempt rather than exposing only session-level `ctx.abort()`.
3. **Structured vs Raw Extra Alignment:**
   * Either the host-neutral port contract must define structured extension properties for provider passthrough, or the provider runtime must support raw wire body substitution. Raw `CanonicalJsonText` cannot be merged into typed JavaScript request objects byte-for-byte.

---

## 4. Current Adapter Status

* **No dead or unreferenced scaffolds:** `src/Wanxiangshu/Omp/ProviderParams.fs(i)` were removed to prevent working-tree drift.
* **Capability Advertising:** Because the prerequisite host mechanisms do not exist to satisfy O-2 fail-closed guarantees and raw Extra passthrough, the omp adapter **does not advertise `HostCapability.ProviderParams`**.
* **Registry:** this blocker belongs to **P-06** (`ITurnTransform`, `TurnAdmission`, `ContextEdit`, `ParamsVerdict` — `Host/Ports/TurnTransforms.fs(i)`), the port family that owns provider-parameter admission. P-06 is recorded as **Blocked / Unreachable Prerequisite** pending the host extension API enhancements listed in §3.

### 4.1 Claim classification

Every claim in this document is a **static source-evidence claim**: it was derived by reading the installed
`@oh-my-pi` sources, and no live provider request was ever sent.

| Claim | Status |
|---|---|
| The wire payload shapes in §1.1 | **static** — read from the installed provider sources |
| The `JSON.stringify(params)` serialisation in §2.1 | **static** — read from the installed provider sources |
| The absence of an opaque raw-string wire slot in §2.1 | **static** — absence observed in the installed provider sources |
| The handler-exception swallowing in §2.2 | **static** — read from `runner.ts` |
| The session-wide abort in §2.3 | **static** — read from `agent-session.ts` |
| A provider actually **rejecting** an unmapped parameter | **not exercised** — no request was sent |
| A provider actually **crashing** on a raw-string `onPayload` result | **not exercised** — no request was sent |
| A real fail-closed refusal in the omp adapter | **not exercised** — no live host, no live smoke |

The "not exercised" rows are the reason the capability stays unadvertised: the contract is not satisfied by
construction, and it has not been demonstrated end-to-end either.
