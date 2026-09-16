Reveal an existing local fact through a bounded static shell query.

This is observation, not execution.

This tool is Inspector-only.

deadline_seconds and output_budget_bytes express how much scarce time and
attention you are willing to spend.
world_lock expresses whether this query should occupy the LargeGate.

These are economic commitments, not runtime predictions.

Appropriate:
    git status
    git diff
    git log
    git blame
    stat
    wc
    similarly narrow static queries

Not appropriate:
    build
    test
    lint
    typecheck
    benchmark
    application startup
    package installation
    migration
    generation
    any command whose purpose is to make the project produce new behavioral
    evidence
