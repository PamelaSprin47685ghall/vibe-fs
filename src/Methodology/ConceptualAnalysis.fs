module VibeFs.Methodology.ConceptualAnalysis

open VibeFs.Methodology.SchemaCommon

let schema =
    buildSchema
        "conceptual_analysis"
        "Clarify meanings, category boundaries, scope; remove category mistakes."
        "When terms collide (tool vs wrapper vs agent vs session vs task)."
        [ reqStr
              "confused_concept"
              "Term or phrase causing wrong edits."
          reqArr
              "senses_disambiguated"
              3
              "Distinct senses with repo examples each."
          reqArr
              "category_boundaries"
              2
              "What is not a member (process vs object, relation vs entity)."
          reqStr
              "scope_fix"
              "Correct scope for the task after disambiguation."
          reqArr
              "category_mistakes_found"
              1
              "Mistakes in prior reasoning or docs."
          optStr
              "recommended_vocabulary"
              "Terms parent should use in todos and reports."
          optArr
              "glossary_entries"
              2
              "Short definitions for KG or comments if allowed." ]
        "Disambiguate vocabulary before structural changes."
        [ "Disambiguation table"
          "Category boundaries"
          "Scope fix"
          "Vocabulary"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema