module ElmishStore.Example.ModelStore

open Elmish
open ElmishStore
open ElmishStore.Example.Model
open Feliz

#if DEBUG

open Elmish.Debug

#endif

// One additional step (comparing with the previous module-based API).
// However, creating the Host on the global level is essentially the same as using the module-based API.
let storesHost = ElmishStoresHost<string>()

let store =
    let program =
        Program.mkProgram init update (fun _ _ -> ())
        #if DEBUG
        |> Program.withConsoleTrace
        |> Program.withDebugger
        #endif
    storesHost.create "main" program

(*// Previous API:
[<Hook>]
let useSelector (selector: Model -> 'a) = React.useElmishStore (store, selector)

[<Hook>]
let useSelectorMemoized (memoizedSelector: Model -> 'a) =
    React.useElmishStoreMemoized (store, memoizedSelector)
*)

// Instead of defining separate custom hooks for a specific Store,
// just create all the Store-related hooks packed in an object.
let storeApi = StoreApi.getElmishStoreApi store

// These helpers should be inline so that Fable doesn't generate unnecessary extra-functions around the hooks.
// However, these helpers aren't really needed, as the hooks could be called directly from the `storeApi` value.
let inline useSelector selector = storeApi.useSelector selector
let inline useSelectorMemoized selector = storeApi.useSelectorMemoized selector

let dispatch = store.Dispatch