module Wanxiangshu.Methodology.Specialization

open Wanxiangshu.Methodology.SchemaCommon

let schema =
    buildSchema
        "specialization"
        "Inspect simple, concrete, boundary, or extreme cases before generalizing."
        "Before designing a general API, algorithm, or refactor covering many inputs."
        [ reqStr
              "general_problem"
              "The broad problem you might over-engineer if you skip concrete cases."
          reqArr
              "concrete_instances"
              3
              "Specific inputs: smallest file, empty workspace, single-tool session, max enum size, etc."
          reqArr
              "boundary_cases"
              2
              "Edges: null, empty list, duplicate keys, restart mid-review, very short background."
          reqArr
              "extreme_cases"
              1
              "Stress cases: 54 tools registered, huge message history, concurrent hooks."
          reqStr
              "lessons_per_instance"
              "What each instance teaches about necessary vs accidental structure."
          optArr
              "generalization_blockers"
              1
              "Facts learned only from one instance that block a naive general rule."
          optStr
              "minimal_general_form"
              "Smallest generalization that still covers listed instances." ]
        "Ground design in named concrete and boundary instances before writing generic code."
        [ "Instance catalog"
          "Boundary and extreme notes"
          "Per-instance lessons"
          "Minimal general form"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema