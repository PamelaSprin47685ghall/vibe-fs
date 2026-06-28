module Wanxiangshu.Tests.ToolCatalogRegistryTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.ToolCatalog

let allCountIs17 () =
    equal "15 tools" 15 all.Length

let allNamesAreNonEmpty () =
    for spec in all do
        check (spec.name + " non-empty") (spec.name <> "")

let allDescriptionsAreNonEmpty () =
    for spec in all do
        check (spec.name + " description non-empty") (spec.description <> "")

let allNamesAreUnique () =
    let names = all |> List.map (fun s -> s.name)
    equal "unique" names (names |> List.distinct)

let specOfRead () =
    let spec = specOf "read"
    equal "name" "read" spec.name

let specOfUnknownThrows () =
    try
        specOf "nonexistent" |> ignore
        check "should throw" false
    with _ -> check "threw" true

let paramDocRead () =
    let doc = paramDoc "read" "path"
    check "non-empty" (doc <> "")

let paramDocUnknownToolFieldThrows () =
    try
        paramDoc "read" "nonexistent_field" |> ignore
        check "should throw" false
    with _ -> check "threw" true

let paramDocUnknownToolNameThrows () =
    try
        paramDoc "nonexistent" "x" |> ignore
        check "should throw" false
    with _ -> check "threw" true

let descriptionRead () =
    equal "description" (description "read") (specOf "read").description

let descriptionUnknownThrows () =
    try
        description "nonexistent" |> ignore
        check "should throw" false
    with _ -> check "threw" true

let subagentRequiredKeysRead () =
    let keys = subagentRequiredKeys "read"
    check "read keys non-empty" (keys.Length > 0)

let subagentRequiredKeysUnknownThrows () =
    try
        subagentRequiredKeys "nonexistent" |> ignore
        check "should throw" false
    with _ -> check "threw" true

let coderSpecExists () =
    let spec = specOf "coder"
    check "name" (spec.name = "coder")

let executorSpecHasRequiredFields () =
    let spec = specOf "executor"
    check "has required fields" (spec.requiredFields.Length > 0)

let allParamDocsConsistent () =
    for spec in all do
        let names = spec.paramDocs |> Map.toList |> List.map fst
        for n in names do
            check (spec.name + "." + n + " doc non-empty") (spec.paramDocs[n] <> "")

let run () =
    allCountIs17 ()
    allNamesAreNonEmpty ()
    allDescriptionsAreNonEmpty ()
    allNamesAreUnique ()
    specOfRead ()
    specOfUnknownThrows ()
    paramDocRead ()
    paramDocUnknownToolFieldThrows ()
    paramDocUnknownToolNameThrows ()
    descriptionRead ()
    descriptionUnknownThrows ()
    subagentRequiredKeysRead ()
    subagentRequiredKeysUnknownThrows ()
    coderSpecExists ()
    executorSpecHasRequiredFields ()
    allParamDocsConsistent ()
