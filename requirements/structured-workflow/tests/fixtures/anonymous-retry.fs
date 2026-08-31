module AnonymousRetry

let withRetry operation input =
    task {
        try
            return! operation input
        with _ ->
            return! operation input
    }
