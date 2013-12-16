﻿(*
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

module Graph =

    open System.Drawing
    open System.Drawing.Drawing2D

    open GraphFunctions

    type IGraph =
       abstract member Draw : 
            graphics:Graphics -> 
            leftMostRecord:int ->
            width:int * height:int -> 
            candleWidth:int ->
            candleLeftMargin:int -> 
            unit

        abstract member RecordWidth : unit -> int

        abstract member Zoom : int -> unit

        abstract member HighAndLow : unit -> float * float

        abstract member Offset : unit -> int

        abstract member NumberOfRecords : unit -> int

    type HighLowOpenClose = (float * float * float * float) array

    type HighLowOpenCloseGraph(records:HighLowOpenClose) =
        interface IGraph with 
            member this.Draw (graphics:Graphics) leftMostRecord (width, height) candleWidth candleLeftMargin =
                let lastRecord = leftMostRecord + (getNumberOfRecordsCanBeDisplayed width candleWidth candleLeftMargin) - 1

                let lastRecord = if lastRecord >= records.Length then records.Length - 1 else lastRecord

                let records = records.[leftMostRecord..lastRecord]

                let high, low = getHighestHighAndLowestLow records
 
                let (labels: float list), highLabel, lowLabel = getRoundedValuesBetween high low [uint16(1);uint16(2);uint16(5)] 10

                let gap = if labels.Length = 1 then 0 else abs(float(labels.Head - labels.Tail.Head)) |> int

                paintCandleSticks graphics (float32 highLabel, float32 lowLabel) (float32 gap) records candleWidth candleLeftMargin leftMostRecord height
 
                paintYAxis graphics labels (width, height)

            member this.RecordWidth () =
                7

            member this.Zoom scale =
                ()

            member this.HighAndLow () =
                getHighestHighAndLowestLow records

            member this.Offset () =
                0

            member this.NumberOfRecords () =
                records.Length
                
    type LineGraph(records:float array) =
        interface IGraph with 
            member this.Draw (graphics:Graphics) leftMostRecord (width, height) candleWidth candleLeftMargin =
                let numberOfRecordsDisplayed = getNumberOfRecordsCanBeDisplayed width candleWidth candleLeftMargin

                let lastRecord = 
                    if leftMostRecord + numberOfRecordsDisplayed > records.Length - 1 then records.Length - 1
                    else leftMostRecord + numberOfRecordsDisplayed

                let points = Array.mapi (fun i value -> 
                    PointF(float32 (i * (candleWidth + candleLeftMargin)), mapValueToYCoordinate 680 (98.4f, 95.4f) 0.0f (float32 value))) records.[leftMostRecord..lastRecord]
    
                use pen = new Pen(Color.White, float32(1))
                graphics.SmoothingMode <- SmoothingMode.AntiAlias
                graphics.DrawLines(pen, points)
                graphics.SmoothingMode <- SmoothingMode.Default

            member this.RecordWidth () =
                1

            member this.Zoom scale =
                ()

            member this.HighAndLow () =
                0.0, 100.0

            member this.Offset () =
                0

            member this.NumberOfRecords () =
                records.Length