﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CabViewerSDKOfficeOpenXml.FormulaParsing.ExpressionGraph;

namespace CabViewerSDKOfficeOpenXml.FormulaParsing.Excel.Functions.Math
{
    public class Ln : ExcelFunction
{
    public override CompileResult Execute(IEnumerable<FunctionArgument> arguments, ParsingContext context)
    {
        ValidateArguments(arguments, 1);
        var arg = ArgToDecimal(arguments, 0);
        return CreateResult(System.Math.Log(arg, System.Math.E), DataType.Decimal);
    }
}
}