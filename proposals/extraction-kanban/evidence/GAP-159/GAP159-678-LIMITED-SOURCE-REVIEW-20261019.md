# GAP-159: non-author limited source review of 678

Input archive: `/private/tmp/gap159-candidate-678/wanxiangshu-0.9.0.tgz`. Independently read SHA-256: `6d986c23419a4fdae8dae4bcd6bce64f1c2ce0eae87bf798d07ea96544913335`.

Read-only scope: IntegratorEngine exact-failed guard; JournalIntegration cut planning; JournalPortObservationSurface typed HandleLinked operation; durable-events WHAT015/021/024 and the handwritten 024 physical fixtures; permanent participant-identity009 old-history fixtures. Shared source was not edited. No Host or typed matrix was rerun; two other reviewers own those runtime receipts. This is source review, not new runtime acceptance.

## Conclusion

No additional blocking product defect was found in this scope. The changes preserve the existing owners and physical error facts. There is one delivery omission to finish before committing: the three new old667 NDJSON originals are currently ignored and untracked.

## Exact-failed guard

The existing Integrator derives the same rule fault scope from the referenced failed event and reads its existing Faults map. If that scope currently names another failed EventId, the cut leaves Current and Faults unchanged. Matching-event cuts retain the original ApplyCut path. The pre-existing no-current-fault behavior is intentionally retained. This does not add a second failure ledger, identity parser, history reader or replay owner.

The actual frozen 678 generated JS contains the exact EventId comparison. The source change is six added lines and one removed line. It prevents an old cut for a later failure from clearing an earlier identity-decode failure exposed by the new interpretation. It does not claim to authenticate arbitrary manually constructed cut records or migrate unsupported candidate-generated history.

## Typed HandleLinked observation port

The new appendActualJournalHandle function accesses the same opaque ActualJournalHandle.Journal and uses the existing typed handleLinkedFact constructor. AgentJournal owns append admission, EventStore owns physical settlement, the existing Journal fold owns the binding conflict and the existing fatal owner terminates the producing process after cut settlement. No Fable union is manufactured in JS. The observation port does not reconstruct fold state, duplicate the settlement decision or install an optional fatal owner. An unexpected returned failure throws rather than being silently accepted.

## 024 error classification and physical proof structure

- The four undecodable variants cover initialization/business payloads with and without release failure. They assert no new bad-event append, no cut, no invented fatal, no waiter/revision advance and poison-based rejection of the next append. The business variants retain the already valid RuntimeStarted append; they do not incorrectly claim the entire physical file is empty.
- Preparation rejection alone remains AppendFailed. When an actual lock release also fails, the existing Store classification is AppendNotAttempted with StoreRelease as primary and the original preparation refusal preserved as priorRejection. This is not CommitUnknown. The test checks the original cause identity and no prepared append.
- The new decodable semantic controls use actual typed HandleLinked facts with the same handle and different children. They verify the real bad fact and Journal cut are durable together before the existing producing-process fatal. Release uncertainty remains CommitUnknown with the exact prepared bad/cut pair. The coordinator observes real exit/close and does not perform the successful fatal itself.
- Independent cold readers compare persisted facts, heads and the canonical projection; they do not synthesize a second expected fold. Original valid and valid-release physical controls remain. The original malformed-payload evidence must stay archived as the explicitly changed behavior, rather than being described as unchanged.
- WHAT015/021/024 now state the undecodable/no-safe-reset boundary and exact failed-event matching. This is an explicit tightening, not a claim that the old acceptance conditions were unchanged. The broader existing mandatory-fatal-capability TODO is not closed by these physical controls.

## Permanent old-history fixtures and delivery omission

The new 009 cases bind the original old667 bytes by SHA-256, distinguish mixed, later-event-cut and legitimate same-event-cut histories, preserve original source bytes and compare the old active identity after the attempted new root. The scenario runs in an owned child and anchors its own wall clock to retain the unmodified historical bytes; it does not rewrite the old timestamps. No new runtime result is claimed by this source review.

At review time `git ls-files` returned no entry for:

- `requirements/participant-identity/tests/support/old667-mixed-journal.ndjson`
- `requirements/participant-identity/tests/support/old667-predictor-later-cut.ndjson`
- `requirements/participant-identity/tests/support/old667-same-event-cut.ndjson`

`git check-ignore -v` identifies `.gitignore:41:*.ndjson` for all three. The new `unknown-identity-plugin-scenario.mjs` is also untracked. These files must be explicitly included in delivery, with the intended input-snapshot coverage, or a clean checkout will fail before exercising the regression. This does not require a new product build.

The previously recorded one-time generic full-history fallback cost on an undecodable cut refusal remains. This limited review does not solve WHAT017 on that exceptional path, close the full Fission lifecycle, or investigate R12-U1.
