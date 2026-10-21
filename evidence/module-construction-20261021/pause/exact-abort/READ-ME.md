# MF-02A read-only investigation, stopped 2026-10-21

The parent stopped further MF-02A investigation. No native Host experiment was started. Existing raw evidence is retained without further probes.

`main-check-act-red.log` and `main-check-act-results.json` show an actual gen102 compiled plugin path, real journal admission, and a controlled SDK transport. The same-physical positive control sends one abort for A. In the race case A passes the local guard, the SDK transport is delayed, B is accepted through the actual `chat.message` hook, then the old request produces an abort payload containing only the session ID. The final safety assertion fails with exit 1. B has `accepted=true` and `providerStarted=false`; this is not evidence that a real B provider was killed.

The probe checks compiled artifact hashes against `main-artifact-before.json`. It does not change source, installed artifacts, or durable production data. All journal data belongs to temporary executable-plugin fixtures.

Source findings before the stop:

- `ToolRuntimeScope.fs` checks the exact physical attempt before calling `sessions.InterruptAttempt sid`; the question path's post-await check cannot retract an already sent abort.
- `Sessions.fs` reaches `port.AbortSession sid`. Both SDK and HTTP adapters in `OpenCodePort.fs` send only the session ID. The SDK declaration has no expected-attempt field.
- There need not be a local JavaScript yield between the guard and SDK invocation. The demonstrated gap is the asynchronous transport/effect boundary.
- The fixed native Host v1.18.29 source cancels background jobs before selecting a session runner. Its runner cancel implementation captures a fiber atomically once selected, but the selection and public API are session-wide. A runner can also process more than one physical input.

The unimplemented candidate is a Host-owned exact attempt cancellation capability, or an atomic expected-attempt operation that captures only that attempt's cancel handle before any yield or side effect. It must not fall back to session abort when unsupported. A request acknowledgement is not a physical completion receipt. This proposal was not tested or implemented here.

Primary native Host source inspected:

- https://raw.githubusercontent.com/anomalyco/opencode/v1.18.29/packages/opencode/src/session/run-state.ts
- https://raw.githubusercontent.com/anomalyco/opencode/v1.18.29/packages/opencode/src/effect/runner.ts
- https://raw.githubusercontent.com/anomalyco/opencode/v1.18.29/packages/opencode/src/session/prompt.ts
- https://raw.githubusercontent.com/anomalyco/opencode/v1.18.29/packages/opencode/src/session/processor.ts

This is a bounded investigation record, not a completed production fix or a native provider reproduction.
