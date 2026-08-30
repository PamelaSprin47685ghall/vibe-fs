namespace OwnerDependencyFixture

open OwnerDependencyFixture.Provider

module Alias = OwnerDependencyFixture.Provider

module Consumer =
    let fromOpen = make 1
    let fromAlias = Alias.make 2
    let fullyQualified = OwnerDependencyFixture.Provider.make 3
    let inferred = make 4
    let typeOnly (value: Hidden) = value
