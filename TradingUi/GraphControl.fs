(*
    Copyright (C) 2013  Matthew Mcveigh
 
    This file is part of F# Unaffiliated BTC-E Trading Framework.
 
    F# Unaffiliated BTC-E Trading Framework is free software; you can redistribute it and/or
    modify it under the terms of the GNU Lesser General Public
    License as published by the Free Software Foundation; either
    version 3 of the License, or (at your option) any later version.
 
    F# Unaffiliated BTC-E Trading Framework is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
    Lesser General Public License for more details.
 
    You should have received a copy of the GNU Lesser General Public
    License along with F# Unaffiliated BTC-E Trading Framework. If not, see <http://www.gnu.org/licenses/>.
*)

namespace TradingUi
 
module GraphControl =
 
    open System
    open System.Drawing
    open System.Windows.Forms
    open System.Drawing.Drawing2D

    open TradingUi.GraphFunctions

    let moveRecords distance direction recordWhenMouseDown candleWidth candleLeftMargin numberOfRecords =
        match direction with
        | Left -> 
            if recordWhenMouseDown + distance / (candleWidth + candleLeftMargin) >= numberOfRecords then numberOfRecords - 1
            else recordWhenMouseDown + distance / (candleWidth + candleLeftMargin)
        | Right -> 
            if recordWhenMouseDown - distance / (candleWidth + candleLeftMargin) < 0 then 0
            else recordWhenMouseDown - distance / (candleWidth + candleLeftMargin)

    let areCoordinatesOutOfBounds (x, y) (width, height) =
            x < 0 || x > width || y < 0 || y > height

    let max lhs rhs =
        if lhs > rhs then lhs else rhs

    let min lhs rhs =
        if lhs < rhs then lhs else rhs

    let bound minVal value maxVal =
        max maxVal (min value maxVal)

    open Scrollbar
    open Graph
 
    type GraphControl(
                        scrollbar: IScrollbar,
                        graph: IGraph
        ) as this =
        inherit Control()

        let mutable leftMostRecord = 0
        let mutable candleWidth = 7
        let mutable mouseDown: (int * int) option = None
        let mutable recordWhenMouseDown: int option = None
        let mutable lastMouse: (int * int) option = None
        let mutable cancellationToken = new System.Threading.CancellationTokenSource()

        let mouseMovements = System.Collections.Generic.Queue<int * int64>()

        let graphs = ResizeArray<IGraph>()

        let maxVelocity = 30.0
        let friction = 1.0

        let margins = 20, 20 // Top, Bottom
        let candleLeftMargin = 3

        let runOnUiThread func =
            let action = Action(fun () -> func())
            this.Invoke(action) |> ignore

        let getNumberOfRecords (graphs:ResizeArray<IGraph>) = 
            (Array.maxBy (fun (graph:IGraph) -> graph.NumberOfRecords()) <| graphs.ToArray()).NumberOfRecords()

        let moveLeft i =
            let numberOfRecords = getNumberOfRecords graphs

            let i = int <| ceil i 
            if leftMostRecord - i < 0 then
                leftMostRecord <- 0
            else if leftMostRecord - i > numberOfRecords - 1 then 
                leftMostRecord <- numberOfRecords - 1
            else
                leftMostRecord <- leftMostRecord - i
            runOnUiThread (fun () -> this.Invalidate())

        let cancelScrolling = new Event<_>()

        do
            this.BackColor <- Color.FromArgb(10, 10, 10)
            this.DoubleBuffered <- true
            graphs.Add(graph)

        [<CLIEvent>]
        member this.CancelScrolling = cancelScrolling.Publish

        member this.AddGraph graph =
            graphs.Add graph

        override this.OnMouseEnter event =
            base.OnMouseEnter event
            Cursor.Hide()

        override this.OnMouseLeave event =
            base.OnMouseLeave event
            Cursor.Show()
            lastMouse <- None
            this.Invalidate()

        override this.OnMouseDown(event:MouseEventArgs) =
            base.OnMouseDown event
            recordWhenMouseDown <- Some(leftMostRecord)
            mouseDown <- Some(event.X, event.Y)
            this.Focus() |> ignore

            mouseMovements.Clear()
            mouseMovements.Enqueue(event.X, DateTime.Now.Ticks)
            cancelScrolling.Trigger()

        override this.OnMouseUp(event:MouseEventArgs) =
            base.OnMouseUp event
            recordWhenMouseDown <- None
            mouseDown <- None

            if mouseMovements.Count > 1 then
                let velocity (mouseMovements: System.Collections.Generic.Queue<int * int64>) =
                    let firstX, firstTime = mouseMovements.Peek()

                    let rec getMouseMovedStats distance time =
                        if mouseMovements.Count > 0 then
                            let x, time = mouseMovements.Dequeue()
                            let milliseconds = time / TimeSpan.TicksPerMillisecond - firstTime / TimeSpan.TicksPerMillisecond
                            getMouseMovedStats (x + (firstX - distance)) milliseconds
                        else
                            distance, time

                    let distance, time = getMouseMovedStats 0 <| int64 0

                    let distance, time = float distance, float time

                    let velocity = (abs(distance) / time) * (if distance < 0.0 then -5.0 else 5.0)

                    if velocity > maxVelocity then maxVelocity
                    else if velocity < -maxVelocity then -maxVelocity
                    else velocity
            
                let kineticScroll = new MailboxProcessor<string>(fun inbox ->
                    let rec loop i timeout velocity moveLeft =
                        async { 
                            let! result = inbox.TryReceive timeout

                            if result.IsSome then
                                mouseMovements.Clear()
                            else
                                let velocity = 
                                    if velocity > 0.0 then 
                                        velocity - friction
                                    else if velocity < 0.0 then
                                        velocity + friction
                                    else
                                        0.0

                                moveLeft velocity

                                if not <| (abs velocity < abs friction) then
                                    return! loop (i + 1) timeout velocity moveLeft
                        } 
                    loop 0 25 (velocity mouseMovements) moveLeft)

                kineticScroll.Start()

                Event.add (fun _ -> kineticScroll.Post "stop") cancelScrolling.Publish

        member private this.TryMoveRecords (eventX, eventY) =
            match mouseDown, recordWhenMouseDown with
            | Some(x, y), Some(recordWhenMouseDown) when not <| areCoordinatesOutOfBounds (eventX, eventY) (this.Width, this.Height) -> 
                let change = float <| eventX - x
                let direction = if change >= 0.0 then Right else Left
                let distance = int <| abs(change)
                leftMostRecord <- moveRecords distance direction recordWhenMouseDown candleWidth candleLeftMargin (getNumberOfRecords graphs)
            | _ -> ()
 
        override this.OnMouseMove(event:MouseEventArgs) =
            base.OnMouseMove event
 
            lastMouse <- Some(event.X, event.Y)

            if recordWhenMouseDown <> None then
                let maxMouseMovementsRecorded = 5
                if mouseMovements.Count > maxMouseMovementsRecorded then
                    mouseMovements.Dequeue() |> ignore
                mouseMovements.Enqueue(event.X, System.DateTime.Now.Ticks)

            this.TryMoveRecords (event.X, event.Y) 

            this.Invalidate()

        member this.Zoom delta candleWidth =
            let change = if delta > 0 then 1 else -1
            if candleWidth + change > 0 && candleWidth + change < 20 then
                candleWidth + change
            else
                candleWidth

        override this.OnMouseWheel(event:MouseEventArgs) =
            base.OnMouseWheel event

            let changeInCandleWidth = this.Zoom event.Delta candleWidth
            if abs(float changeInCandleWidth) > 0.0 then
                candleWidth <- this.Zoom event.Delta candleWidth
                this.Invalidate()
 
        override this.OnPaint (event:PaintEventArgs) =
            base.OnPaint event

            let numberOfRecords = getNumberOfRecords graphs

            let graphs = graphs.ToArray()

            let recordWidth = Array.maxBy (fun (graph:IGraph) -> graph.RecordWidth()) graphs

            let highsAndLows = Array.map (fun (graph:IGraph) -> graph.HighAndLow()) graphs

            let high = Array.maxBy (fun (high, _) -> high) highsAndLows

            let low = Array.maxBy (fun (_, low) -> low) highsAndLows

            let drawGraph (graph: IGraph) = 
                graph.Draw event.Graphics leftMostRecord (this.Width, this.Height) candleWidth candleLeftMargin

            Array.iter drawGraph graphs

            scrollbar.Draw event.Graphics leftMostRecord numberOfRecords (this.Width, this.Height) candleWidth candleLeftMargin

            match lastMouse with
            | Some(x, y) when not <| areCoordinatesOutOfBounds (x, y) (this.Width, this.Height) -> 
                let recordNumber = mapXCoordinateToRecordNumber (float x) candleWidth candleLeftMargin leftMostRecord |> int
                let x = mapRecordNumberToXCoordinate recordNumber candleWidth candleLeftMargin leftMostRecord
                let x = x + int(ceiling(float(candleWidth / 2)))
                paintCoordinates event.Graphics (x, y) (this.Width, this.Height)
            | _ -> ()