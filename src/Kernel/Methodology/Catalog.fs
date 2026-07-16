module Wanxiangshu.Kernel.Methodology.Catalog

open Wanxiangshu.Kernel.Methodology.Schema

let all: Lazy<MethodologyEntry list> =
    lazy
        (Logic.entries
         @ ProblemTransformation.entries
         @ MathematicalReasoning.entries
         @ Optimization.entries
         @ SystemsEngineering.entries
         @ CriticalInquiry.entries)
