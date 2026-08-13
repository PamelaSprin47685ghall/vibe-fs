Reveal an existing local fact through a bounded static shell query.

This is observation, not execution.

This tool is Inspector-only.

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
