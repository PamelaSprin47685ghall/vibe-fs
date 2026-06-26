module Wanxiangshu.Methodology.HermeneuticCircle

open Wanxiangshu.Methodology.SchemaCommon

let schema =
    buildSchema
        "hermeneutic_circle"
        "Iterate part and whole until local and global meaning stabilize."
        "Understanding large codepaths, README+implementation together, PRD+tests."
        [ reqStr
              "whole_artifact"
              "Global text/system (README architecture, full Plugin.fs)."
          reqStr
              "part_focus"
              "Local fragment under study (one hook, one schema file)."
          reqArr
              "part_to_whole_updates"
              2
              "How part revised understanding of whole."
          reqArr
              "whole_to_part_updates"
              2
              "How whole revised reading of part."
          reqStr
              "stabilized_reading"
              "Mutually consistent interpretation after iterations."
          optArr
              "remaining_tension"
              1
              "Parts still inconsistent—need more reads."
          optStr
              "reading_order"
              "Suggested file order for parent reads." ]
        "Alternate local detail and global architecture until coherent."
        [ "Iteration log"
          "Stabilized reading"
          "Remaining tension"
          "Reading order"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema