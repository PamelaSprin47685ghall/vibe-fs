# G140-B1 limited prototype: independent source review

Reviewer: non-author agent d1_review_git. Date: 2026-10-19.

Inputs: main baseline 8b5dd03f7; isolated prototype /private/tmp/gap140-B1-prototype, seed f273501, current a1b4ba0 plus the three uncommitted gen2 Dispatcher/Send changes. No shared source was edited. No active dist or product test was consumed by this review.

## Conclusion

No blocking implementation defect was found in the limited non-idle HostSessionNudge SendGateNudge source change. This is a static review, not acceptance of a production package or closure of GAP-140.

The weak map belongs to composition and keys the exact live AgentJournal object. The runtime holds one transient in-flight dictionary, while all authority, projection, handle and acceptance reads still use the Journal. The value-to-key closure does not turn a JavaScript WeakMap into an ordinary permanent registry. No durable identity, profile or completed result is cached by the new factory.

The flight uses the existing SessionId / LogicalRunId / Origin / gate-kind-and-terminal-run scope. Exact active profile is rechecked before claiming and adjacent to the actual physical send. Different sessions, logical runs, origins and terminal occasions use distinct existing scopes. Joining the same flight does not independently claim or send. The finally block releases only the matching completion instance, including errors; it does not retain a finished Task as future send authority.

Unknown still leaves the durable claim pending. After a flight ends, the existing SessionNudge admission read sees Pending/Submitted/Accepted and does not resend. Writer failure and uncertainty remain errors, not a new permission. Never-settling Host calls retain their real in-flight obligation; this patch adds neither timeout nor fabricated outcome.

The gen2 onJoined callback only runs after a successful shared result and invokes the existing exact ObserveGateNudgeAcceptance. It reads fresh projection, requires the same active profile, checks physical landing evidence, or registers against the exact pending key. Registration keeps the existing latest single callback owner and independent lease identity: an old lease cannot remove a newer registration. This review does not require broadcasting callbacks. Observer exceptions remain caller-visible; they do not trigger another Host send. Closed resource owners can immediately release the returned lease using the existing observer contract.

The six SessionNudge substitutions share the composition factory. Other production forPrompts constructions found outside SessionNudge are read/observe or other methods, rather than a second SendGateNudge owner. Verification surfaces retain their deliberately separate runtime.

## Scope limitation requiring explicit reporting

SendIdleGateNudge still directly calls SendContinuationWithDigestAttempt and does not enter RunGateNudgeOnce. Its real callers include Manager and JoinGuard admission paths. They have their own physicalAdmission/quiescence checks; the B1 non-idle recovery proof does not prove that concurrent idle calls consume one sequence or write one claim. This is an existing boundary, not a regression caused by sharing the factory. The new WHAT wording must not imply that this B1 proves every terminal gate path has one flight. This was sent to root before delivery; implementation of a merged permit path is not recommended without a separate explicit contract.

Physical duplicate FailureRecorded facts, different live Journals, separate module/runtime instances, restart and cross-process arbitration remain G140-B2. Source review cannot replace the fixed production package, real caller, Unknown reentry and observer ownership tests. GAP-140 must remain open.

## Source identity

- PromptJournalAdapter.fs: fbf4d54c7c02bbdababaccbcd8454d9b6034242a178e02161c345a6a5c69bf12
- PromptJournalAdapter.fsi: 2a15ae07cd7b47c76009fab49671f9928835e674e040207244144e3d67aa9afb
- OpenCode/SessionNudge.fs: 336fbe7dcabea69cfca0a74711ddffb76aaa658b2c348fe8a6ba5bd755f306eb
- Dispatcher.fs: 7c651e614ed1ac9b2988a44c75994ce4f3dc01944dda756367113172ae9aa4ad
- Dispatcher.fsi: c57cb30658819f9d26deac3ec445a6eb8e95037ff64abac582ca40c89a193052
- Send.fs: d4945f0c9c70d418f37d43852268189a3013a3cdb731beb36bde223dcd3b0d16

AGENTS and the upstream delivery checklist were read. The patch is local, preserves the existing fact and observer owners, and adds no duplicate authority ledger or compatibility facade. This review did not run build, tests, git mutations or external effects.
