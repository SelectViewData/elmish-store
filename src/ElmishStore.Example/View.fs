module ElmishStore.Example.View

open System
open Feliz
open ElmishStore.Example.Model

[<ReactComponent>]
let private Counter1 () =
  let counter = ModelStore.useSelector (_.Counter1)

  Html.div [
    prop.className "border flex flex-col items-center justify-center gap-4 p-4"
    prop.children [
      Html.span [
        prop.className "text-xl"
        prop.text $"Counter 1: %i{counter}"
      ]
      Html.button [
        prop.className "bg-blue-500 hover:bg-blue-700 text-white font-bold py-2 px-4 rounded"
        prop.onClick (fun _ -> Increment1 |> ModelStore.dispatch)
        prop.text "Increment"
      ]
    ]
  ]

[<ReactComponent>]
let private Counter2 () =
  let counter = ModelStore.useSelector (_.Counter2)

  Html.div [
    prop.className "border flex flex-col items-center justify-center gap-4 p-4"
    prop.children [
      Html.span [
        prop.className "text-xl"
        prop.text $"Counter 2: %i{counter}"
      ]
      Html.button [
        prop.className "bg-green-500 hover:bg-green-700 text-white font-bold py-2 px-4 rounded"
        prop.onClick (fun _ -> Increment2 |> ModelStore.dispatch)
        prop.text "Increment"
      ]
    ]
  ]

[<ReactComponent>]
let private CounterSum () =
  let counter1, counter2 = ModelStore.useSelectorMemoized (fun m -> (m.Counter1, m.Counter2))
  // uncomment the line below to see an error in the console 'The result of getSnapshot should be cached to avoid an infinite loop'
  // caused by the fact that selector returns a new tuple on each call and standard useSelector uses reference equality to compare the result
  //let counter1, counter2 = ModelStore.useSelector (fun m -> (m.Counter1, m.Counter2))
  Html.div [
    prop.className "border p-4 text-xl"
    prop.text $"Counters Sum: %i{counter1 + counter2}"
  ]

[<ReactComponent>]
let private Panel () =
  Html.div [
    prop.className "flex flex-col items-center gap-4 pt-16"
    prop.children [
      Html.div [
        prop.className "flex gap-4"
        prop.children [
          Counter1()
          Counter2()
        ]
      ]
      CounterSum()
    ]
  ]

[<ReactComponent>]
let private Header () =
  Html.div [
    prop.className "flex flex-col items-center gap-4 p-4"
    prop.children [
      Html.span [
        prop.className "font-bold text-2xl"
        prop.text "Elmish Store"
      ]
      Html.span [
        prop.text "Open react dev tools to see which components are re-rendered."
      ]
    ]
  ]

[<ReactComponent>]
let private Footer () =
  Html.span [
    prop.className "font-bold text-center text-gray-500"
    prop.text $"elmish-store example - {DateTime.Now.Year}"
  ]

[<ReactComponent>]
let AppView () =
  Html.div [
    prop.className "grid grid-rows-[auto_1fr_auto] min-h-screen"
    prop.children [
      Header()
      Panel()
      Footer()
    ]
  ]