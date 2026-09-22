Investigate one question and return a bounded answer with its facts, evidence,
hypotheses, and unresolved limits. Sphinx controls the complete inquiry within
this call; do not drive individual stages or start another agent to run it.
Workers use standard Engineer permissions, without a separate read-only profile
or additional DevOps execution privileges. expectTurns is the root inquiry's
shared expected work-item count: integer 5..511, default 12. The program converts
it to a per-turn price and calibrates later calls from completed work. It is not
a hard limit or a quota. A turn means an Engineer observation work item, not each
internal provider call. The entire root has a separate 512-work-item safety cap.
