module VibeFs.Tests.ReviewReplaySyncTests

open VibeFs.Tests.Assert
open VibeFs.Kernel.Messaging
open VibeFs.Kernel.ReviewReplayPolicy
open VibeFs.Kernel.LoopMessages
open VibeFs.Kernel.PromptFrontMatter
open VibeFs.Shell.ReviewRuntime
open VibeFs.Shell.ReviewReplaySync

let textsFromFlatPartsIncludesToolOutput () =
    let toolState =
        { status = "completed"
          output = "tool-body"
          error = ""
          input = ()
          operationAction = "" }
    let msg =
        { info =
            { id = "m1"
              sessionID = "s1"
              role = Assistant
              agent = ""
              isError = false
              toolName = ""
              details = ()
              time = () }
          parts = [ ToolPart("read", "c1", Some toolState, ()) ]
          source = Native
          raw = () }
    let flat = flatten [ msg ]
    let texts = textsFromFlatParts flat |> Seq.toList
    equal "tool output collected" [ "tool-body" ] texts

let replayReviewIfStoreEmptyNoOpWhenStoreHasState () =
    let store = createReviewStore ()
    store.activateReview ("s1", "existing", 1L)
    replayReviewIfStoreEmpty store "s1" [ "ignored" ]
    equal "task unchanged when store already has state" (Some "existing") (store.getReviewTask "s1")

let replayReviewAlwaysSyncActivatesFromTexts () =
    let store = createReviewStore ()
    let activate =
        frontMatterPrompt [ yamlField taskField "from-replay" ] "body"
    replayReviewAlwaysSync store "s2" [ activate ]
    equal "replay activates task" (Some "from-replay") (store.getReviewTask "s2")
    check "replay marks session active" (store.isReviewActive "s2")

let replayReviewIfStoreEmptyActivatesWhenEmpty () =
    let store = createReviewStore ()
    let activate = frontMatterPrompt [ yamlField taskField "empty-store-task" ] "body"
    replayReviewIfStoreEmpty store "s3" [ activate ]
    equal "empty store replay activates" (Some "empty-store-task") (store.getReviewTask "s3")

let replayReviewIfStoreEmptySkipsWhenActiveButAlwaysSyncUpdates () =
    let store = createReviewStore ()
    store.activateReview ("s4", "held", 1L)
    replayReviewIfStoreEmpty store "s4" [ frontMatterPrompt [ yamlField taskField "ignored" ] "body" ]
    equal "if-store-empty skips active session" (Some "held") (store.getReviewTask "s4")
    let update = frontMatterPrompt [ yamlField taskField "synced" ] "body"
    replayReviewAlwaysSync store "s4" [ update ]
    equal "always sync updates task" (Some "synced") (store.getReviewTask "s4")

let run () =
    textsFromFlatPartsIncludesToolOutput ()
    replayReviewIfStoreEmptyNoOpWhenStoreHasState ()
    replayReviewAlwaysSyncActivatesFromTexts ()
    replayReviewIfStoreEmptyActivatesWhenEmpty ()
    replayReviewIfStoreEmptySkipsWhenActiveButAlwaysSyncUpdates ()