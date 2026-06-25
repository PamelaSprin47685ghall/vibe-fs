module VibeFs.Kernel.ToolCatalog.FileIO

open VibeFs.Kernel.ToolCatalog.ToolSpec

let internal readSpec: ToolSpec =
    { name = "read"
      description =
        "If path is a directory, returns a formatted directory listing (equivalent to ls -la). Use this instead of running `ls` via executor."
      paramDocs =
        map
            [ "path", "The absolute or relative path to read"
              "offset", "Line to start from, 1-indexed"
              "limit", "Maximum lines to read" ]
      requiredFields = [ "path" ] }

let internal writeSpec: ToolSpec =
    { name = "write"
      description =
        "Write content to a file. Resolves relative paths against the current working directory, creates parent directories if they don't exist, and runs syntax checking on the written content."
      paramDocs =
        map
            [ "file_path", "The absolute or relative path of the file to write"
              "content", "The content to write to the file" ]
      requiredFields = [ "file_path"; "content" ] }
