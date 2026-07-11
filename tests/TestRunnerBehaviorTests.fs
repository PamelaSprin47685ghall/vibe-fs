module Wanxiangshu.Tests.TestRunnerBehaviorTests

open Wanxiangshu.Tests.Assert

let defaultSuiteHasNoArchitectureLabels (labels: string list) : unit =
    let archLabels =
        labels
        |> List.filter (fun label -> label.Contains "ArchitectureTests" || label.Contains "TestsArchitecture")

    equal "no_architecture_labels" [] archLabels
