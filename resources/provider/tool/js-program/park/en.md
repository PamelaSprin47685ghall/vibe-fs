park(payload) — suspend this program at this await point and hand control back to the orchestrator. The tool call returns a top-level parked payload `{ handle, payload }`; the caller resumes with `{ unpark: { handle, value } }` or gives up with `{ abandon: { handle } }`.

The program sees park as an ordinary await of an external input: the resumed value is whatever the caller injects through unpark. The site stays alive in the host until it is resumed or abandoned; a parked program is not finished and its already-applied writes remain on disk (as-is).
