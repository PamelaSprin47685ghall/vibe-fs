module VibeFs.Methodology.FirstPrinciples

open VibeFs.Methodology.SchemaCommon

let schema =
    buildSchema
        "first_principles"
        "Reduce the problem to irreducible facts and rebuild from them."
        "When inherited assumptions, frameworks, or copy-paste patterns obscure what must be true."
        [ reqStr
              "problem_statement"
              "One paragraph stating the deliverable, success signal, and what is explicitly out of scope for this reasoning pass."
          reqArr
              "assumptions_to_strip"
              2
              "List assumptions you are temporarily suspending (organizational, framework, prior design). Each item names the assumption and why it might be accidental complexity."
          reqArr
              "atomic_facts"
              3
              "Facts that remain after stripping: observables, file paths, test outcomes, protocol guarantees, or user-stated requirements. No interpretation yet."
          reqArr
              "rebuild_steps"
              3
              "Ordered steps to reconstruct a solution only from atomic facts. Each step should add one justified layer (model, interface, algorithm, test)."
          reqStr
              "irreducible_core"
              "The smallest description of the problem that still captures all constraints; should be short enough to fit in a README section."
          reqArr
              "rejected_shortcuts"
              1
              "Shortcuts or patterns you refuse to use until first-principles rebuild completes, with one-line justification each."
          optArr
              "open_questions"
              1
              "Questions that block rebuild; each should name what evidence would answer it."
          optStr
              "workspace_anchors"
              "Comma-separated paths, modules, or symbols this rebuild must respect or replace." ]
        "Strip inherited narratives until only workspace-anchored facts remain, then rebuild a minimal architecture story."
        [ "Stripped assumption ledger"
          "Atomic fact table"
          "Rebuild chain"
          "Irreducible core statement"
          "Rejected shortcuts and why"
          "Next executable actions for the parent agent" ]

let toolSpec = toToolCatalogSpec schema