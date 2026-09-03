namespace LocalityDependencyFixture

open LocalityDependencyFixture.Provider

module Consumer =
    type Alias = PublicValue

    val fromOpen: PublicValue
    val generic: values: PublicValue list -> PublicValue list
    val fromPattern: PublicChoice -> string
