namespace LocalityDependencyFixture

open LocalityDependencyFixture.Provider

module Consumer =
    type Alias = PublicValue

    let fromOpen = make "open"

    let generic (values: PublicValue list) = values |> List.map preserve

    let fromPattern (PublicChoice value) = value
