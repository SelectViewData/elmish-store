namespace ElmishStore

open Fable.Core
open Feliz

type UseSyncExternalStoreSubscribe = delegate of (unit -> unit) -> (unit -> unit)

[<Erase>]
type internal ReactBindings =

  [<ImportMember("react")>]
  static member useSyncExternalStore
    (
      subscribe: UseSyncExternalStoreSubscribe,
      getSnapshot: unit -> 'Model
    ) : 'Model =
    jsNative

  [<ImportMember("use-sync-external-store/with-selector")>]
  static member useSyncExternalStoreWithSelector
    (
      subscribe: UseSyncExternalStoreSubscribe,
      getSnapshot: unit -> 'Model,
      getServerSnapshot: (unit -> 'Model) option,
      selector: 'Model -> 'a,
      ?isEqual: 'a -> 'a -> bool
    ) : 'a =
    jsNative

  static member inline useSyncExternalStoreWithSelector
    (
      subscribe: UseSyncExternalStoreSubscribe,
      getSnapshot: unit -> 'Model,
      selector: 'Model -> 'a,
      ?isEqual: 'a -> 'a -> bool
    ) : 'a =
    ReactBindings.useSyncExternalStoreWithSelector (subscribe, getSnapshot, None, selector, ?isEqual = isEqual)
