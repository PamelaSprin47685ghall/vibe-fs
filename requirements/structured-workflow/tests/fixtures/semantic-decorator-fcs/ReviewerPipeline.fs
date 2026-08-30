namespace SemanticDecoratorFixture

module ReviewerPipeline =
    let reviewerPipelineTwice operation a b =
        a |> operation; b |> operation

    let pipelineOnce operation a =
        a |> operation

    let nestedSiblingTwice operation combine a b =
        combine (operation a) (operation b)

    let nestedSelfTwice operation a =
        operation (operation a)

    let immediateLambdaTwice operation a b =
        (fun () -> operation a; operation b) ()

    let localInvokedTwice operation a b =
        let run () =
            operation a
            operation b

        run ()

    let escapedCallback operation register =
        register (fun value -> operation value)
