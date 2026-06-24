module VibeFs.Methodology.Deconstruction

open VibeFs.Methodology.SchemaCommon

let schema =
    buildSchema
        "deconstruction"
        "Inspect hidden binaries, excluded voices, unstable centers in framing."
        "When PRD, AGENTS, or design docs assume hierarchy that hides alternatives."
        [ reqStr
              "text_or_design"
              "Document or architecture being deconstructed."
          reqArr
              "binary_oppositions"
              2
              "Either/or frames (kernel vs hack, speed vs safety) and what they suppress."
          reqArr
              "excluded_middle"
              1
              "Third options ruled out by rhetoric."
          reqStr
              "unstable_center"
              "Claimed SSOT or center that depends on what it excludes."
          reqArr
              "internal_contradictions"
              2
              "Tensions within the framing itself."
          optStr
              "reframe"
              "Fairer framing for deciding next engineering work."
          optArr
              "actionable_extractions"
              1
              "Still-valid requirements after deconstruction." ]
        "Expose rhetorical structure so decisions are not captive to false binaries."
        [ "Binaries and exclusions"
          "Contradictions"
          "Reframe"
          "Actionable requirements"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema