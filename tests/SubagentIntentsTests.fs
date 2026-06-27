module Wanxiangshu.Tests.SubagentIntentsTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.SubagentIntents

let coderTargetFilesSingle () =
    let intent = { objective = "o"; background = "b"; targets = [{ file = "x.fs"; guide = "g"; draft = None }]; doNotTouch = [||] }
    equal "single target" [ "x.fs" ] (coderTargetFiles intent)

let coderTargetFilesMultiple () =
    let intent = { objective = "o"; background = "b"; targets = [{ file = "a.fs"; guide = "g1"; draft = None }; { file = "b.fs"; guide = "g2"; draft = Some "draft" }]; doNotTouch = [||] }
    equal "multiple targets" [ "a.fs"; "b.fs" ] (coderTargetFiles intent)

let coderTargetFilesEmpty () =
    let intent = { objective = "o"; background = "b"; targets = []; doNotTouch = [||] }
    equal "empty targets" [] (coderTargetFiles intent)

let run () =
    coderTargetFilesSingle ()
    coderTargetFilesMultiple ()
    coderTargetFilesEmpty ()
