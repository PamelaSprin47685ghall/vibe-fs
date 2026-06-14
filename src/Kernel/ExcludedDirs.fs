module VibeFs.Kernel.ExcludedDirs

/// Directory names to skip during filesystem walks.  Compared case-insensitively.
let excludedDirNames: Set<string> =
    Set.ofList
        [ ".git"; "node_modules"; "__pycache__"; ".ds_store"; "target"; "dist"; "out"
          ".venv"; "venv"; ".cache"; ".next"; ".turbo"; ".parcel-cache" ]

let isExcludedDir (name: string) : bool =
    Set.contains (name.ToLowerInvariant ()) excludedDirNames
