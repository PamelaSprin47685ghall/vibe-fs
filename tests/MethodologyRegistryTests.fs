module Wanxiangshu.Tests.MethodologyRegistryTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Methodology.SchemaCommon
open Wanxiangshu.Methodology.Registry

let allSchemasCountIs54 () =
    equal "count" 54 (allSchemas.Length)

let allToolSpecsCountMatches () =
    equal "count" 54 (allToolSpecs.Length)

let toolNameMatchesMethodologyId () =
    for s in allSchemas do
        let expected = "methodology_" + s.methodologyId
        equal (s.methodologyId + " toolName") expected s.toolName

let shortDefinitionNonEmpty () =
    for s in allSchemas do
        check (s.methodologyId + " shortDefinition") (s.shortDefinition <> "")

let triggerWhenNonEmpty () =
    for s in allSchemas do
        check (s.methodologyId + " triggerWhen") (s.triggerWhen <> "")

let toolDescriptionContainsMethodologyId () =
    for s in allSchemas do
        check (s.methodologyId + " in description") (s.toolDescription.Contains s.methodologyId)

let intentFieldPresent () =
    for s in allSchemas do
        let names = s.fields |> List.map (fun f -> f.name)
        check (s.methodologyId + " has intent") (List.contains "intent" names)
        check (s.methodologyId + " has background") (List.contains "background" names)

let intentFieldIsRequired () =
    for s in allSchemas do
        let intent = s.fields |> List.find (fun f -> f.name = "intent")
        check (s.methodologyId + " intent required") intent.required
        let bg = s.fields |> List.find (fun f -> f.name = "background")
        check (s.methodologyId + " background required") bg.required

let meditatorRoleNonEmpty () =
    for s in allSchemas do
        check (s.methodologyId + " meditatorRole") (s.meditatorRole <> "")

let outputSectionsNonEmpty () =
    for s in allSchemas do
        check (s.methodologyId + " outputSections") (s.outputSections.Length > 0)

let toolSpecHasParamDocs () =
    for spec in allToolSpecs do
        check (spec.name + " paramDocs") (spec.paramDocs.Count > 0)

let toolSpecRequiredFieldsIncludeIntent () =
    for spec in allToolSpecs do
        check (spec.name + " requires intent") (List.contains "intent" spec.requiredFields)
        check (spec.name + " requires background") (List.contains "background" spec.requiredFields)

let tryFindSchemaFound () =
    match tryFindSchema "deduction" with
    | Some s -> equal "found" "methodology_deduction" s.toolName
    | None -> failwith "deduction not found"

let tryFindSchemaNotFound () =
    equal "not found" None (tryFindSchema "nonexistent")

let tryFindToolSpecFound () =
    match tryFindToolSpec "first_principles" with
    | Some spec -> check "spec.name prefix" (spec.name.StartsWith "methodology_")
    | None -> failwith "first_principles not found"

let tryFindToolSpecNotFound () =
    equal "not found" None (tryFindToolSpec "nonexistent")

let allToolSpecNamesAreUnique () =
    let names = allToolSpecs |> List.map (fun s -> s.name)
    equal "unique names" names (names |> List.distinct)

let methodologyToolNameFunction () =
    equal "methodology_tdd" "methodology_test_driven_reasoning" (methodologyToolName "test_driven_reasoning")

let renderInputYamlStructure () =
    match tryFindSchema "first_principles" with
    | Some s ->
        let yaml = renderInputYaml s (Map.ofList ["intent", "test"; "background", "bg"]) (Map.ofList ["assumptions_to_strip", ["a"; "b"]])
        check "contains methodology:" (yaml.Contains "methodology: ")
        check "contains tool:" (yaml.Contains "tool: ")
        check "contains inputs:" (yaml.Contains "inputs:")
    | None -> failwith "first_principles not found"

let run () =
    allSchemasCountIs54 ()
    allToolSpecsCountMatches ()
    toolNameMatchesMethodologyId ()
    shortDefinitionNonEmpty ()
    triggerWhenNonEmpty ()
    toolDescriptionContainsMethodologyId ()
    intentFieldPresent ()
    intentFieldIsRequired ()
    meditatorRoleNonEmpty ()
    outputSectionsNonEmpty ()
    toolSpecHasParamDocs ()
    toolSpecRequiredFieldsIncludeIntent ()
    tryFindSchemaFound ()
    tryFindSchemaNotFound ()
    tryFindToolSpecFound ()
    tryFindToolSpecNotFound ()
    allToolSpecNamesAreUnique ()
    methodologyToolNameFunction ()
    renderInputYamlStructure ()
