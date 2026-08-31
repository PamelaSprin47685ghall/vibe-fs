namespace OwnerDependencyFixture

open OwnerDependencyFixture.Provider

module Alias = OwnerDependencyFixture.Provider

module Consumer =
    let fromOpen = make 1
    let fromAlias = Alias.make 2
    let fullyQualified = OwnerDependencyFixture.Provider.make 3
    let inferred = make 4
    let typeOnly (value: Hidden) = value

    type Cursor =
        { Address: int }

    let choose cursor =
        match cursor.Address with
        | 0 -> Foreign.runA
        | _ -> Foreign.runB

    let send (port: Foreign.Port) cursor =
        match cursor.Address with
        | 0 -> port.Send "a"
        | _ -> port.Send "b"

    let sendVerified (port: Foreign.Port) =
        match verify 5 with
        | Ok _ -> port.Send "verified"
        | Error _ -> ()

    let bindVerified () =
        async {
            let! value = async.Return 6
            return make value
        }

    let fromPipeline = 7 |> make

    let structuredControl (port: Foreign.Port) cursor =
        if cursor.Address = 0 then
            port.Send "then"
        else
            port.Send "else"

        try
            port.Send "try"
        with _ ->
            port.Send "with"

        for value in [ 1 ] do
            port.Send(string value)

    let curriedNested = Provider.combine (make 8) (make 9)
    let qualifiedCallableArgument = Provider.invoke make 10
    let build runner repo = WorktreeCommands.create runner repo
    let nested = Provider.outer (Provider.inner())

    let closureRelations value =
        let invoked x = x + 1
        let escaped x = x + 2
        let escapedLambda = fun x -> x + 3
        let invokedValue = invoked value
        let iifeValue = (fun x -> x + 4) value
        invokedValue, iifeValue, escaped, escapedLambda
