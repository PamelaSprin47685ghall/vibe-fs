module Wanxiangshu.Tests.ToolCatalogRegistryTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.ToolCatalog

let allCountIs19 () = equal "19 tools" 19 all.Length

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
    match specOf "read" with
    | Ok spec -> equal "name" "read" spec.name
    | Error e -> check ("specOf read failed: " + e) false

let specOfUnknownReturnsError () =
    match specOf "nonexistent" with
    | Error msg -> check "contains unknown tool" (msg.Contains("unknown tool"))
    | Ok _ -> check "should not succeed" false

let paramDocRead () =
    match paramDoc "read" "path" with
    | Ok doc -> check "non-empty" (doc <> "")
    | Error e -> check ("paramDoc failed: " + e) false

let paramDocUnknownToolFieldReturnsError () =
    match paramDoc "read" "nonexistent_field" with
    | Error msg -> check "contains unknown param" (msg.Contains("unknown param"))
    | Ok _ -> check "should not succeed" false

let paramDocUnknownToolNameReturnsError () =
    match paramDoc "nonexistent" "x" with
    | Error msg -> check "contains unknown tool" (msg.Contains("unknown tool"))
    | Ok _ -> check "should not succeed" false

let descriptionRead () =
    match description "read", specOf "read" with
    | Ok desc, Ok spec -> equal "description" desc spec.description
    | _ -> check "descriptionRead failed" false

let descriptionUnknownReturnsError () =
    match description "nonexistent" with
    | Error msg -> check "contains unknown tool" (msg.Contains("unknown tool"))
    | Ok _ -> check "should not succeed" false

let subagentRequiredKeysRead () =
    match subagentRequiredKeys "read" with
    | Ok keys -> check "read keys non-empty" (keys.Length > 0)
    | Error e -> check ("subagentRequiredKeys failed: " + e) false

let subagentRequiredKeysUnknownReturnsError () =
    match subagentRequiredKeys "nonexistent" with
    | Error msg -> check "contains unknown tool" (msg.Contains("unknown tool"))
    | Ok _ -> check "should not succeed" false

let coderSpecExists () =
    match specOf "coder" with
    | Ok spec -> check "name" (spec.name = "coder")
    | Error e -> check ("coderSpecExists failed: " + e) false

let executorSpecHasRequiredFields () =
    match specOf "executor" with
    | Ok spec -> check "has required fields" (spec.requiredFields.Length > 0)
    | Error e -> check ("executorSpecHasRequiredFields failed: " + e) false

let allParamDocsConsistent () =
    for spec in all do
        let names = spec.paramDocs |> Map.toList |> List.map fst

        for n in names do
            check (spec.name + "." + n + " doc non-empty") (spec.paramDocs[n] <> "")

let ptySpawnSpecExists () =
    match specOf "pty_spawn" with
    | Ok spec -> equal "name" "pty_spawn" spec.name
    | Error e -> check ("ptySpawnSpecExists failed: " + e) false

let executorWaitNotExist () =
    match specOf "executor_wait" with
    | Error msg -> check "contains unknown tool" (msg.Contains("unknown tool"))
    | Ok _ -> check "executor_wait should not exist in catalog" false

let executorAbortNotExist () =
    match specOf "executor_abort" with
    | Error msg -> check "contains unknown tool" (msg.Contains("unknown tool"))
    | Ok _ -> check "executor_abort should not exist in catalog" false

let run () =
    allCountIs19 ()
    allNamesAreNonEmpty ()
    allDescriptionsAreNonEmpty ()
    allNamesAreUnique ()
    specOfRead ()
    specOfUnknownReturnsError ()
    paramDocRead ()
    paramDocUnknownToolFieldReturnsError ()
    paramDocUnknownToolNameReturnsError ()
    descriptionRead ()
    descriptionUnknownReturnsError ()
    subagentRequiredKeysRead ()
    subagentRequiredKeysUnknownReturnsError ()
    coderSpecExists ()
    executorSpecHasRequiredFields ()
    allParamDocsConsistent ()
    ptySpawnSpecExists ()
    executorWaitNotExist ()
    executorAbortNotExist ()
