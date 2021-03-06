﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CabViewerSDK.OfficeOpenXml.FormulaParsing.ExpressionGraph;

namespace CabViewerSDK.OfficeOpenXml.FormulaParsing.Excel.Functions.Math
{
    public class Degrees : ExcelFunction
    {
        public override CompileResult Execute(IEnumerable<FunctionArgument> arguments, ParsingContext context)
        {
            ValidateArguments(arguments, 1);
            var angle = ArgToDecimal(arguments, 0);
            var result = (angle*180)/System.Math.PI;
            return CreateResult(result, DataType.Decimal);
        }
    }
}
