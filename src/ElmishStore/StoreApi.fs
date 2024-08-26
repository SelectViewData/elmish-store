module ElmishStore.StoreApi

open Feliz

type IElmishStoreHooks<'Model> =
    /// Provides a current snapshot of the store's state selected by the selector function.
    /// NOTE: Selector returning value needs to be referentially stable.
    abstract useSelector : selector: ('Model -> 'T) -> 'T
    /// Provides a current snapshot of the store's state selected by the selector function.
    /// The result of the selector function is memoized and compared with structural equality.
    abstract useSelectorMemoized<'T when 'T : equality> : selector: ('Model -> 'T) -> 'T
    /// Provides a current snapshot of the store's state selected by the selector function.
    /// The result of the selector function is memoized and compared with isEqual function.
    abstract useSelectorMemoizedWithCustomEquality : selector: ('Model -> 'T) -> isEqual: ('T -> 'T -> bool) -> 'T

type IElmishStoreApi<'Model, 'Msg> =
    inherit IElmishStoreHooks<'Model>

    abstract Store : ElmishStore<'Model, 'Msg>


let getElmishStoreApi (store: ElmishStore<'Model, 'Msg>) =
    { new IElmishStoreApi<'Model, 'Msg> with
        member _.Store = store

        member _.useSelector(selector: 'Model -> 'T): 'T = 
            Hooks.useElmishStore store selector

        member _.useSelectorMemoized<'T when 'T : equality> (selector: 'Model -> 'T) =
            Hooks.useElmishStoreMemoized store selector

        member _.useSelectorMemoizedWithCustomEquality<'T>
            (selector: 'Model -> 'T)
            (isEqual: 'T -> 'T -> bool): 'T
            =
            Hooks.useElmishStoreMemoizedWithCustomEquality store selector isEqual
    }


[<Hook>]
let useElmishStoreApi (store: ElmishStore<'Model, 'Msg>) =
    React.useMemo (fun () ->
        getElmishStoreApi store
    , [| store |])
