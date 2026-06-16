module VibeFs.Shell.CapsFilter

open System.Text.RegularExpressions

let capsFileRe = Regex(@"^[A-Z][A-Z0-9_]*\.md$")
let capsDirRe = Regex(@"^[A-Z][A-Z0-9_]*$")
let capsDotDirRe = Regex(@"^\.[A-Z][A-Z0-9_]*$")

let excludedFileNames = set [ "AGENTS.md"; "CLAUDE.md"; "README.md" ]
let excludedDirNames =
    set [ "AGENTS"; "CLAUDE"; "NODE_MODULES"; ".GIT"; "TARGET"; "DIST"; "OUT"
          ".VENV"; "VENV"; "__PYCACHE__"; ".CACHE"; ".NEXT"; ".TURBO"; ".PARCEL-CACHE" ]

let isExcludedDir (name: string) : bool =
    Set.contains (name.ToUpperInvariant ()) excludedDirNames
    || (name.StartsWith(".") && not (capsDotDirRe.IsMatch name))

let isCapsFile (name: string) : bool =
    capsFileRe.IsMatch name && not (Set.contains name excludedFileNames)

let isCapsDir (name: string) : bool =
    not (isExcludedDir name) && (capsDirRe.IsMatch name || capsDotDirRe.IsMatch name)
