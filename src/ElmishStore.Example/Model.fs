module ElmishStore.Example.Model

open Elmish

type Model = {
  Counter1: int
  Counter2: int
}

type Msg =
  | Increment1
  | Increment2

let init () =
  { Counter1 = 0; Counter2 = 0 }, Cmd.none

let update msg model =
  match msg with
  | Increment1 -> { model with Counter1 = model.Counter1 + 1 }, Cmd.none
  | Increment2 -> { model with Counter2 = model.Counter2 + 1 }, Cmd.none
