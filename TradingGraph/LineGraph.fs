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

namespace TradingGraph

module LineGraph =

    open System.Drawing
    open System.Drawing.Drawing2D

    open GraphFunctions
    open IGraph

    type LineGraph(records:float array) =
        let mutable offset = 0
        let mutable lastRecord = records.Length - 1

        interface IGraph with 
            member this.Draw (graphics:Graphics) leftMostRecord (width, height) candleWidth candleLeftMargin (highLabel, lowLabel) gap marginTop =
                if lastRecord >= leftMostRecord then
                    let numberOfRecordsDisplayed = getNumberOfRecordsCanBeDisplayed width candleWidth candleLeftMargin

                    let lastRecord = 
                        if leftMostRecord + numberOfRecordsDisplayed > lastRecord then lastRecord
                        else leftMostRecord + numberOfRecordsDisplayed

                    let points = Array.mapi (fun i value -> 
                        let y = mapValueToYCoordinate height (highLabel, lowLabel) gap (float32 value)
                        let y = y + float32 marginTop
                        let x = float32 (i * (candleWidth + candleLeftMargin))
                        PointF(x, y)) records.[leftMostRecord..lastRecord]

                    if points.Length > 1 then
                        use pen = new Pen(Color.White, float32(1))
                        graphics.SmoothingMode <- SmoothingMode.AntiAlias
                        graphics.DrawLines(pen, points)
                        graphics.SmoothingMode <- SmoothingMode.Default

            member this.RecordWidth () = 1

            member this.RecordMargin () = 0

            member this.Zoom scale = ()

            member this.HighAndLow leftMostRecord numberOfRecordsDisplayed =
                let finalRecord = leftMostRecord + numberOfRecordsDisplayed
                let lastRecord = if finalRecord > lastRecord then lastRecord else finalRecord

                if lastRecord >= leftMostRecord then
                    Some(foldRecordsToHighLow highAndLowFolder records.[leftMostRecord..lastRecord])
                else
                    None

            member this.Offset
                with get () = offset
                and set (value) = offset <- value

            member this.LastRecord
                with get () = lastRecord
                and set (value) = lastRecord <- value

            member this.NumberOfRecords () = records.Length