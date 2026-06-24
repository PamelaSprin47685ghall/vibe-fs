module VibeFs.Methodology.DecompositionRecombination

open VibeFs.Methodology.SchemaCommon

let schema =
    buildSchema
        "decomposition_recombination"
        "Split the object into parts and reconnect them in a better structure."
        "When a module, tool surface, or workflow is too entangled to edit safely."
        [ reqStr
              "whole_artifact"
              "The system or file cluster being split (paths)."
          reqArr
              "parts"
              3
              "Named parts with single responsibility each."
          reqArr
              "interfaces_between_parts"
              2
              "Contracts between parts: types, events, pure functions, no hidden globals."
          reqStr
              "recombined_shape"
              "How parts should be wired after split (dependency direction)."
          reqArr
              "migration_slices"
              2
              "Order of moves that keep build green between steps."
          optArr
              "coupling_to_cut"
              1
              "Forbidden imports or shared mutable state to eliminate."
          optStr
              "architecture_test_hooks"
              "Existing or new gates that enforce the new seams." ]
        "Propose a part graph and migration slices that respect vibe-fs layering."
        [ "Decomposition map"
          "Interface contracts"
          "Recombined architecture"
          "Migration slices"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema