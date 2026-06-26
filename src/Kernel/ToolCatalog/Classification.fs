module VibeFs.Kernel.ToolCatalog.Classification

let private fileEditToolNames =
    Set.ofList
        [ "edit"; "write"; "ast_edit"; "ast_grep_replace"; "file_edit_replace_string"
          "file_edit_insert"; "apply_patch" ]

let isFileEditTool (tool: string) : bool =
    Set.contains (tool.ToLowerInvariant ()) fileEditToolNames
