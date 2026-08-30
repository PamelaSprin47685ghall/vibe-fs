namespace OwnerDependencyFixture

module Provider =
    type Hidden = Hidden of int

    let make value = Hidden value
