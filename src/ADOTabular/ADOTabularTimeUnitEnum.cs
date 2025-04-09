using System.Collections.Generic;
using System.Collections;
using System.Diagnostics.Contracts;
using ADOTabular.Extensions;
using ADOTabular.Interfaces;
using Microsoft.AnalysisServices.Tabular;

namespace ADOTabular
{
    /// <summary>
    /// TimeUnit
    /// </summary>
    public enum TimeUnit
    {
        /// <summary>
        /// Unknown
        /// </summary>
        Unknown = 0,
        /// <summary>
        /// Year
        /// </summary>
        Year = 1,
        /// <summary>
        /// Semester
        /// </summary>
        Semester = 2,
        /// <summary>
        /// SemesterOfYear
        /// </summary>
        SemesterOfYear = 3,
        /// <summary>
        /// Quarter
        /// </summary>
        Quarter = 4,
        /// <summary>
        /// QuarterOfYear
        /// </summary>
        QuarterOfYear = 5,
        /// <summary>
        /// QuarterOfSemester
        /// </summary>
        QuarterOfSemester = 6,
        /// <summary>
        /// Month
        /// </summary>
        Month = 7,
        /// <summary>
        /// MonthOfYear
        /// </summary>
        MonthOfYear = 8,
        /// <summary>
        /// MonthOfSemester
        /// </summary>
        MonthOfSemester = 9,
        /// <summary>
        /// MonthOfQuarter
        /// </summary>
        MonthOfQuarter = 10,
        /// <summary>
        /// Week
        /// </summary>
        Week = 11,
        /// <summary>
        /// WeekOfYear
        /// </summary>
        WeekOfYear = 12,
        /// <summary>
        /// WeekOfSemester
        /// </summary>
        WeekOfSemester = 13,
        /// <summary>
        /// WeekOfQuarter
        /// </summary>
        WeekOfQuarter = 14,
        /// <summary>
        /// WeekOfMonth
        /// </summary>
        WeekOfMonth = 15,
        /// <summary>
        /// Date
        /// </summary>
        Date = 16,
        /// <summary>
        /// DayOfYear
        /// </summary>
        DayOfYear = 17,
        /// <summary>
        /// DayOfSemester
        /// </summary>
        DayOfSemester = 18,
        /// <summary>
        /// DayOfQuarter
        /// </summary>
        DayOfQuarter = 19,
        /// <summary>
        /// DayOfMonth
        /// </summary>
        DayOfMonth = 20,
        /// <summary>
        /// DayOfWeek
        /// </summary>
        DayOfWeek = 21,
    }

    public class ADOTabularTimeUnitUtil
    {
        public static TimeUnit StringToTimeUnit(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return TimeUnit.Unknown;

            switch (input.Trim().ToLowerInvariant())
            {
                case "year":
                    return TimeUnit.Year;
                case "semester":
                    return TimeUnit.Semester;
                case "semesterofyear":
                    return TimeUnit.SemesterOfYear;
                case "quarter":
                    return TimeUnit.Quarter;
                case "quarterofyear":
                    return TimeUnit.QuarterOfYear;
                case "quarterofsemester":
                    return TimeUnit.QuarterOfSemester;
                case "month":
                    return TimeUnit.Month;
                case "monthofyear":
                    return TimeUnit.MonthOfYear;
                case "monthofsemester":
                    return TimeUnit.MonthOfSemester;
                case "monthofquarter":
                    return TimeUnit.MonthOfQuarter;
                case "week":
                    return TimeUnit.Week;
                case "weekofyear":
                    return TimeUnit.WeekOfYear;
                case "weekofsemester":
                    return TimeUnit.WeekOfSemester;
                case "weekofquarter":
                    return TimeUnit.WeekOfQuarter;
                case "weekofmonth":
                    return TimeUnit.WeekOfMonth;
                case "date":
                    return TimeUnit.Date;
                case "dayofyear":
                    return TimeUnit.DayOfYear;
                case "dayofsemester":
                    return TimeUnit.DayOfSemester;
                case "dayofquarter":
                    return TimeUnit.DayOfQuarter;
                case "dayofmonth":
                    return TimeUnit.DayOfMonth;
                case "dayofweek":
                    return TimeUnit.DayOfWeek;
                default:
                    return TimeUnit.Unknown;
            }
        }
    }
}
