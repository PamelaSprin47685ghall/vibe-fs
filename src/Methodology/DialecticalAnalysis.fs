module VibeFs.Methodology.DialecticalAnalysis

open VibeFs.Methodology.SchemaCommon

let schema =
    buildSchema
        "dialectical_analysis"
        "Thesis, antithesis, tension, dependency, resolution—not one-sided causality."
        "When opposing forces shape design (DRY vs 54 files, kernel purity vs host Dyn)."
        [ reqStr
              "thesis"
              "One force or design pole with advocates and benefits."
          reqStr
              "antithesis"
              "Opposing force with legitimate benefits."
          reqArr
              "tensions"
              2
              "Concrete conflicts between poles in this task."
          reqArr
              "dependencies"
              1
              "How each pole needs the other (cannot eliminate entirely)."
          reqStr
              "synthesis_path"
              "Resolution trajectory: layered compromise, phased plan."
          optStr
              "frozen_decision"
              "What leadership already decided—synthesis must respect."
          optArr
              "tradeoffs_accepted"
              1
              "Costs each side accepts in synthesis." ]
        "Mediate real opposing design forces instead of picking a slogan."
        [ "Thesis vs antithesis"
          "Tensions"
          "Synthesis"
          "Accepted tradeoffs"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema