module Wanxiangshu.Tests.PatchParserTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.PatchParser

let nullReturnsEmpty () =
    let result = pathsFromPatchText null
    check "null returns empty" (result = [])

let emptyStringReturnsEmpty () =
    let result = pathsFromPatchText ""
    check "empty string returns empty" (result = [])

let addFileExtracted () =
    let text = "*** Add File: src/Foo.fs"
    let result = pathsFromPatchText text
    equal "add file path" [ "src/Foo.fs" ] result

let updateFileExtracted () =
    let text = "*** Update File: src/Bar.fs"
    let result = pathsFromPatchText text
    equal "update file path" [ "src/Bar.fs" ] result

let moveToExtracted () =
    let text = "*** Move to: src/Baz.fs"
    let result = pathsFromPatchText text
    equal "move to path" [ "src/Baz.fs" ] result

let noMatchReturnsEmpty () =
    let text = "some unrelated text\nno markers here"
    let result = pathsFromPatchText text
    check "no match returns empty" (result = [])

let deduplicatesSamePath () =
    let text =
        "*** Add File: src/Foo.fs\n" + "some context\n" + "*** Update File: src/Foo.fs"

    let result = pathsFromPatchText text
    equal "deduplicate same path" [ "src/Foo.fs" ] result

let multipleDistinctPaths () =
    let text =
        "*** Add File: src/A.fs\n"
        + "*** Update File: src/B.fs\n"
        + "*** Move to: src/C.fs"

    let result = pathsFromPatchText text
    equal "multiple distinct paths" [ "src/A.fs"; "src/B.fs"; "src/C.fs" ] result

let whitespaceOnlyReturnsEmpty () =
    let result = pathsFromPatchText "   \n\t\n  "
    check "whitespace only returns empty" (result = [])

let run () =
    nullReturnsEmpty ()
    emptyStringReturnsEmpty ()
    addFileExtracted ()
    updateFileExtracted ()
    moveToExtracted ()
    noMatchReturnsEmpty ()
    deduplicatesSamePath ()
    multipleDistinctPaths ()
    whitespaceOnlyReturnsEmpty ()
