﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CabViewerSDK.OfficeOpenXml.FormulaParsing.ExpressionGraph;

namespace CabViewerSDK.OfficeOpenXml.FormulaParsing.Excel.Functions.Information
{
    public class Na : ExcelFunction
    {
        public override CompileResult Execute(IEnumerable<FunctionArgument> arguments, ParsingContext context)
        {
            return CreateResult(ExcelErrorValue.Create(eErrorType.NA), DataType.ExcelError);
        }
    }
}
