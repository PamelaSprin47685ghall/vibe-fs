module Wanxiangshu.Kernel.ToolCatalog.FileIO

open Wanxiangshu.Kernel.ToolCatalog.ToolSpec
open Wanxiangshu.Kernel

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

let internal swapSpec: ToolSpec =
    { name = "swap"
      description =
        "Exchange two non-overlapping line ranges between text files, or within the same text file. "
        + "Line numbers are 1-based; begin is inclusive and endExclusive is exclusive. "
        + "Both files are validated before either file is changed."
      paramDocs =
        map
            [ "path0", "First file path"
              "begin0", "Start line in first file, 1-based, inclusive"
              "endExclusive0", "End line in first file, 1-based, exclusive"
              "path1", "Second file path"
              "begin1", "Start line in second file, 1-based, inclusive"
              "endExclusive1", "End line in second file, 1-based, exclusive" ]
      requiredFields = [ "path0"; "begin0"; "endExclusive0"; "path1"; "begin1"; "endExclusive1" ] }
