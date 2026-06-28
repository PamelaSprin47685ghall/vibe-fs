module Wanxiangshu.Methodology.Catalog

open Wanxiangshu.Methodology.SchemaCommon

let all: MethodologySchema list =
    Catalog1.schemas
    @ Catalog2.schemas
    @ Catalog3.schemas
    @ Catalog4.schemas
