module Wanxiangshu.Tests.ToolCatalogClassificationTests

open Fable.Core
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.ToolCatalog.Classification

let isFileEditTool_true () =
    let trueCases =
        [ "edit"
          "write"
          "ast_edit"
          "ast_grep_replace"
          "file_edit_replace_string"
          "file_edit_insert"
          "apply_patch" ]

    trueCases
    |> List.iter (fun name -> check ("isFileEditTool true: " + name) (isFileEditTool name = true))

let isFileEditTool_caseInsensitive () =
    [ ("EDIT", true); ("Write", true); ("Ast_Edit", true); ("Apply_Patch", true) ]
    |> List.iter (fun (name, expected) ->
        check ("isFileEditTool case insensitive: " + name) (isFileEditTool name = expected))

let isFileEditTool_false () =
    let falseCases = [ "read"; "grep"; "glob"; "run"; "browser"; "inspector" ]

    falseCases
    |> List.iter (fun name -> check ("isFileEditTool false: " + name) (isFileEditTool name = false))

let run () =
    isFileEditTool_true ()
    isFileEditTool_caseInsensitive ()
    isFileEditTool_false ()
