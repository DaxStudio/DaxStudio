﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Interfaces
{
    interface IQueryPlanRow
    {
        int RowNumber { get; set; }
        int Level { get; set; }
        int NextSiblingRowNumber { get; set; }
    }
}
