module VibeFs.Methodology.Analogy

open VibeFs.Methodology.SchemaCommon

let schema =
    buildSchema
        "analogy"
        "Transfer a known solution structure from a genuinely similar problem."
        "When a canonical template elsewhere in the repo or domain likely shares topology with the current task."
        [ reqStr
              "source_domain"
              "The known problem or module you are analogizing from (path or product name)."
          reqStr
              "target_domain"
              "The current problem location in this workspace."
          reqArr
              "shared_structure"
              3
              "Mappings: source facet → target facet (roles, data flow, state machine shape)."
          reqArr
              "transferred_tactics"
              2
              "Concrete tactics to copy (file layout, hook order, test pattern) with edits noted."
          reqArr
              "mismatch_risks"
              2
              "Where the analogy breaks (different host, sync vs async, permission model)."
          reqStr
              "similarity_argument"
              "Why similarity is structural not superficial keyword match."
          optArr
              "anti_analogies"
              1
              "Similar-looking cases that must not be copied; explain difference."
          optStr
              "adaptation_checklist"
              "Ordered edits to apply the analogy safely here." ]
        "Map structure from a proven neighbor problem and stress-test mismatches before copying code."
        [ "Source–target mapping"
          "Transferred tactics"
          "Mismatch risks"
          "Adaptation checklist"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema