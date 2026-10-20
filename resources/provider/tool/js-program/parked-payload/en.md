The tool call returned a parked payload because the program suspended at `await this.park(payload)`.

- `handle` identifies the parked site. It is consumed exactly once: resume with `{ unpark: { handle, value } }` or give up with `{ abandon: { handle } }`.
- `payload` is the value the program passed to park. It is the program's own data, not a host verdict.
- A parked site is process-live: it dies with the process. While it is unresolved, delivery stays blocked by the leak guard gate.

Resuming injects `value` as the awaited result of park and lets the program continue. Abandoning rejects the program with the ABANDONED marker so its native `try/finally` cleanup runs; the outcome is Abandoned with no value.
