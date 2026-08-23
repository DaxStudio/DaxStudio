using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.Tests
{
    public static class DaxDateTable
    {
        public static string DateTable = @"DEFINE
------------------------------------------------------------
--
-- Configuration
--
------------------------------------------------------------
VAR TodayReference =
    TODAY () -- Change this if you need to use another date as a reference ""current"" day
VAR FirstYear = 2019
VAR LastYear = 
    YEAR ( TodayReference )
VAR FiscalCalendarFirstMonth = 1 -- For Fiscal 52-53 weeks (start depends on rules) and Gregorian (starts on the first of the month) 
VAR FirstDayOfWeek = 0 -- Use: 0 - Sunday, 1 - Monday, 2 - Tuesday, ... 5 - Friday, 6 - Saturday
VAR TypeStartFiscalYear = 1 -- Fiscal year as Calendar Year of : 0 - First day of fiscal year, 1 - Last day of fiscal year
VAR IsoCountryHolidays = ""US"" -- Use only supported ISO countries or """" for no holidays
VAR WeeklyType = ""Last"" -- Use: ""Nearest"" or ""Last"" 
VAR QuarterWeekType = ""445"" -- Supports only ""445"", ""454"", and ""544""
VAR CalendarRange = ""Calendar"" -- Supports ""Calendar"", ""FiscalGregorian"", ""FiscalWeekly""
-- Last:    for last weekday of the month at fiscal year end
-- Nearest: for last weekday nearest the end of month 
-- Reference for Last/Nearest definition: https://en.wikipedia.org/wiki/4%E2%80%934%E2%80%935_calendar)
--
-- For ISO calendar use 
--   FiscalCalendarFirstMonth = 1 (ISO always starts in January)
--   FirstDayOfWeek = 1           (ISO always starts on Monday)
--   WeeklyType = ""Nearest""       (ISO use the nearest week type algorithm)
-- For US with last Saturday of the month at fiscal year end
--   FirstDayOfWeek = 0           (US weeks start on Sunday)
--   WeeklyType = ""Last""
-- For US with last Saturday nearest the end of month
--   FirstDayOfWeek = 0           (US weeks start on Sunday)
--   WeeklyType = ""Nearest""
--
------------------------------
VAR CalendarGregorianPrefix = """" -- Prefix used in columns of standard Gregorian calendar
VAR FiscalGregorianPrefix = ""F"" -- Prefix used in columns of fiscal Gregorian calendar
VAR FiscalWeeklyPrefix = ""FW "" -- Prefix used in columns of fiscal weekly calendar
VAR WorkingDayType = ""Working day"" -- Description for working days
VAR NonWorkingDayType = ""Non-working day"" -- Description for non-working days
------------------------------
VAR WeeklyCalendarType = ""Weekly"" -- Supports ""Weekly"", ""Custom""
-- Set the working days - 0 = Sunday, 1 = Monday, ... 6 = Saturday
VAR WorkingDays =
    DATATABLE ( ""WorkingDayNumber"", INTEGER, { { 1 }, { 2 }, { 3 }, { 4 }, { 5 } } ) --

-- Use CustomFiscalPeriods in case you need arbitrary definition of weekly fiscal years 
-- Set ""UseCustomFiscalPeriods"" to TRUE in order to use CustomFiscalPeriods 
VAR UseCustomFiscalPeriods = FALSE
-- Set ""IgnoreWeeklyFiscalPeriods"" to TRUE in order to ignore the WeeklyFiscalPeriods
-- You should set IgnoreWeeklyFiscalPeriods to TRUE only when UseCustomFiscalPeriods is TRUE, too
VAR IgnoreWeeklyFiscalPeriods = FALSE
-- Include here your own definition of custom fiscal periods
VAR CustomFiscalPeriods =
    FILTER ( 
        DATATABLE (
            ""Fiscal YearNumber"", INTEGER,
            ""FirstDayOfYear"", DATETIME,
            ""LastDayOfYear"", DATETIME,
            {
    			-- IMPORTANT!!! The first day of each year must be a weekday corresponding to the definition of FirstDayOfWeek
    			--              If you want to use this table, remember to set the UseCustomFiscalPeriods variable to TRUE
    			--              If the IgnoreWeeklyFiscalPeriods is TRUE, there are no warnings in case the FirstDayOfWeek 
    			--              does not match the first day of the year 
                { 2016, ""2015-06-28"", ""2016-07-02"" },
                { 2017, ""2016-07-03"", ""2017-07-01"" },
                { 2018, ""2017-07-02"", ""2018-06-30"" },
                { 2019, ""2018-07-01"", ""2019-06-29"" }
            }
        ),
        UseCustomFiscalPeriods
    )

------------------------------------------------------------
--  
-- End of General Configuration
--
------------------------------------------------------------
--  
-- The following variables define specific parameters 
-- for calendars - you should modify them only to 
-- change configuration of specific countries, translate 
-- names of holidays, or to add configuration for other 
-- countries
--
------------------------------------------------------------
VAR InLieuOf_prefix = ""(in lieu of "" -- prefix of substitute holidays
VAR InLieuOf_suffix = "")"" -- prefix of substitute holidays
VAR HolidayParameters =
    DATATABLE (
        ""ISO Country"", STRING,
        -- ISO country code (to enable filter based on country)
        ""MonthNumber"", INTEGER,
        -- Number of month - use 99 for relative dates using Easter as a reference
        ""DayNumber"", INTEGER,
        -- Absolute day (ignore WeekDayNumber, otherwise use 0)
        ""WeekDayNumber"", INTEGER,
        -- 0 = Sunday, 1 = Monday, ... , 7 = Saturday
        ""OffsetWeek"", INTEGER,
        -- 1 = first, 2 = second, ... -1 = last, -2 = second-last, ...
        ""OffsetDays"", INTEGER,
        -- days to add after offsetWeek and WeekDayNumber have been applied
        ""HolidayName"", STRING,
        -- Holiday name 
        ""SubstituteHoliday"", INTEGER,
        -- 0 = no substituteHoliday, 1 = substitute holiday with next working day, 2 = substitute holiday with next working day 
        -- (use 2 before 1 only, e.g. Christmas = 2, Boxing Day = 1)
        ""ConflictPriority"", INTEGER,
        -- Priority in case of two or more holidays in the same date - lower number --> higher priority
        -- For example: marking Easter relative days with 150 and other holidays with 100 means that other holidays take 
        --              precedence over Easter-related days; use 50 for Easter related holidays to invert such a priority
        {
            --
            -- US = United States
            { ""US"", 1, 1, 0, 0, 0, ""New Year's Day"", 0, 100 },
            { ""US"", 1, 0, 1, 3, 0, ""Martin Luther King, Jr."", 0, 100 },
            { ""US"", 2, 0, 1, 3, 0, ""Presidents' Day"", 0, 100 },
            // aka Washington's Birthday
            { ""US"", 5, 0, 1, -1, 0, ""Memorial Day"", 0, 100 },
            { ""US"", 7, 4, 0, 0, 0, ""Independence Day"", 0, 100 },
            { ""US"", 9, 0, 1, 1, 0, ""Labor Day"", 0, 100 },
            { ""US"", 10, 0, 1, 2, 0, ""Columbus Day"", 0, 100 },
            { ""US"", 11, 11, 0, 0, 0, ""Veterans Day"", 0, 100 },
            { ""US"", 11, 0, 4, 4, 0, ""Thanksgiving Day"", 0, 100 },
            { ""US"", 11, 0, 4, 4, 1, ""Black Friday"", 0, 100 },
            { ""US"", 12, 25, 0, 0, 0, ""Christmas Day"", 0, 100 },
            --
            -- CA = Canada (include only nationwide and Thanksgiving)
            { ""CA"", 1, 1, 0, 0, 0, ""New Year's Day"", 0, 100 },
            { ""CA"", 99, -2, 0, 0, 0, ""Good Friday"", 0, 50 },
            { ""CA"", 7, 1, 0, 0, 0, ""Canada Day"", 0, 100 },
            { ""CA"", 9, 0, 1, 1, 0, ""Labour Day"", 0, 100 },
            { ""CA"", 10, 0, 1, 2, 0, ""Thanksgiving"", 0, 100 },
            { ""CA"", 12, 25, 0, 0, 0, ""Christmas Day"", 0, 100 },
            --
            -- UK = England (different configuration in Scotland and Northern Ireland)
            { ""UK"", 1, 1, 0, 0, 0, ""New Year's Day"", 1, 100 },
            { ""UK"", 99, -2, 0, 0, 0, ""Good Friday"", 0, 50 },
            { ""UK"", 99, 1, 0, 0, 0, ""Easter Monday"", 0, 50 },
            { ""UK"", 5, 0, 1, 1, 0, ""May Day Bank Holiday"", 0, 100 },
            { ""UK"", 5, 0, 1, -1, 0, ""Spring Bank Holiday"", 0, 100 },
            { ""UK"", 8, 0, 1, -1, 0, ""Late Summer Bank Holiday"", 0, 100 },
            { ""UK"", 12, 25, 0, 0, 0, ""Christmas Day"", 2, 100 },
            { ""UK"", 12, 26, 0, 0, 0, ""Boxing Day"", 1, 100 },
            --
            -- AU = Australia
            { ""AU"", 1, 1, 0, 0, 0, ""New Year's Day"", 1, 100 },
            { ""AU"", 1, 26, 0, 0, 0, ""Australia Day"", 1, 100},
            { ""AU"", 99, -2, 0, 0, 0, ""Good Friday"", 0, 50 },
            { ""AU"", 99, 1, 0, 0, 0, ""Easter Monday"", 0, 50 },
            { ""AU"", 4, 25, 0, 0, 0, ""Anzac Day"", 1, 100 },
            { ""AU"", 12, 25, 0, 0, 0, ""Christmas Day"", 2, 100 },
            { ""AU"", 12, 26, 0, 0, 0, ""Boxing Day"", 1, 100 },
            --
            -- DE = Germany
            { ""DE"", 1, 1, 0, 0, 0, ""New Year's Day"", 0, 100 },
            { ""DE"", 99, -2, 0, 0, 0, ""Good Friday"", 0, 50 },
            { ""DE"", 99, 1, 0, 0, 0, ""Easter Monday"", 0, 50 },
            { ""DE"", 5, 1, 0, 0, 0, ""Labour Day"", 0, 100 },
            { ""DE"", 99, 39, 0, 0, 0, ""Ascension Day"", 0, 50 },
            { ""DE"", 99, 50, 0, 0, 0, ""Whit Monday"", 0, 50 },
            { ""DE"", 10, 3, 0, 0, 0, ""German Unity Day"", 0, 100 },
            { ""DE"", 12, 25, 0, 0, 0, ""Christmas Day"", 0, 100 },
            { ""DE"", 12, 26, 0, 0, 0, ""St. Stephen's Day"", 0, 100 },
            --
            -- FR = France
            { ""FR"", 1, 1, 0, 0, 0, ""New Year's Day"", 0, 100 },
            { ""FR"", 99, 1, 0, 0, 0, ""Easter Monday"", 0, 50 },
            { ""FR"", 5, 1, 0, 0, 0, ""Labour Day"", 0, 100 },
            { ""FR"", 5, 8, 0, 0, 0, ""Victor in Europe Day"", 0, 100 },
            { ""FR"", 99, 39, 0, 0, 0, ""Ascension Day"", 0, 50 },
            { ""FR"", 99, 50, 0, 0, 0, ""Whit Monday"", 0, 50 },
            { ""FR"", 7, 14, 0, 0, 0, ""Bastille Day"", 0, 100 },
            { ""FR"", 8, 15, 0, 0, 0, ""Assumption Day"", 0, 100 },
            { ""FR"", 11, 1, 0, 0, 0, ""All Saints' Day"", 0, 100 },
            { ""FR"", 11, 11, 0, 0, 0, ""Armistice Day"", 0, 100 },
            { ""FR"", 12, 25, 0, 0, 0, ""Christmas Day"", 0, 100 },
            --
            -- IT = Italy
            { ""IT"", 1, 1, 0, 0, 0, ""New Year's Day"", 0, 100 },
            { ""IT"", 1, 6, 0, 0, 0, ""Epiphany"", 0, 100 },
            { ""IT"", 99, 1, 0, 0, 0, ""Easter Monday"", 0, 100 },
            { ""IT"", 4, 25, 0, 0, 0, ""Liberation Day"", 0, 100 },
            { ""IT"", 5, 1, 0, 0, 0, ""Labour Day"", 0, 100 },
            { ""IT"", 6, 2, 0, 0, 0, ""Republic Day"", 0, 100 },
            { ""IT"", 8, 15, 0, 0, 0, ""Assumption Day"", 0, 100 },
            { ""IT"", 11, 1, 0, 0, 0, ""All Saints' Day"", 0, 100 },
            { ""IT"", 12, 8, 0, 0, 0, ""Immaculate Conception"", 0, 100 },
            { ""IT"", 12, 25, 0, 0, 0, ""Christmas Day"", 0, 100 },
            { ""IT"", 12, 26, 0, 0, 0, ""St. Stephen's Day"", 0, 100 },
            --
            -- ES = Spain
            { ""ES"", 1, 1, 0, 0,0,  ""New Year's Day"", 0, 100 },
            { ""ES"", 1, 6, 0, 0, 0, ""Epiphany"", 0, 100 },
            { ""ES"", 99, -3, 0, 0, 0, ""Maundy Thursday"", 0, 50 },
            // Except Catalonia
            { ""ES"", 99, -2, 0, 0, 0, ""Good Friday"", 0, 50 },
            { ""ES"", 99, 1, 0, 0, 0, ""Easter Monday"", 0, 50 },
            // Belearic Islands, Basque Country, Catalonia, La Rioja, Navarra and Valenciana only
            { ""ES"", 5, 1, 0, 0, 0, ""Labour Day"", 0, 100 },
            { ""ES"", 8, 15, 0, 0, 0, ""Assumption Day"", 0, 100 },
            { ""ES"", 10, 12, 0, 0, 0, ""Fiesta Navional de España"", 0, 100 },
            { ""ES"", 11, 1, 0, 0, 0, ""All Saints' Day"", 0, 100 },
            { ""ES"", 12, 6, 0, 0, 0, ""Constitution Day"", 0, 100 },
            { ""ES"", 12, 8, 0, 0, 0, ""Immaculate Conception"", 0, 100 },
            { ""ES"", 12, 25, 0, 0, 0, ""Christmas Day"", 0, 100 },
            --
            -- NL = The Netherlands
            { ""NL"", 1, 1, 0, 0, 0, ""New Year's Day"", 0, 100 },
            { ""NL"", 99, 1, 0, 0, 0, ""Easter Monday"", 0, 50 },
            { ""NL"", 99, 39, 0, 0, 0, ""Ascension Day"", 0, 50 },
            { ""NL"", 99, 50, 0, 0, 0, ""Whit Monday"", 0, 50 },
            { ""NL"", 4, 27, 0, 0, 0, ""King's Day"", 0, 100 },
            // King's day shifted to Saturday if on a Sunday - not handled in this calendar
            { ""NL"", 5, 5, 0, 0, 0, ""Liberation Day"", 0, 100 },
            { ""NL"", 12, 25, 0, 0, 0, ""Christmas Day"", 0, 100 },
            { ""NL"", 12, 26, 0, 0, 0, ""St. Stephen's Day"", 0, 100 },
            --
            -- SE = Sweden
            { ""SE"", 1, 1, 0, 0, 0, ""New Year's Day"", 0, 100 },
            { ""SE"", 1, 6, 0, 0, 0, ""Epiphany"", 0, 100 },
            { ""SE"", 99, -2, 0, 0, 0, ""Good Friday"", 0, 50 },
            { ""SE"", 99, 1, 0, 0, 0, ""Easter Monday"", 0, 50 },
            { ""SE"", 99, 39, 0, 0, 0, ""Ascension Day"", 0, 50 },
            { ""SE"", 5, 1, 0, 0, 0, ""Labour Day"", 0, 100 },
            { ""SE"", 6, 6, 0, 0, 0, ""National Day"", 0, 100 },
            { ""SE"", 12, 25, 0, 0, 0, ""Christmas Day"", 0, 100 },
            { ""SE"", 12, 26, 0, 0, 0, ""Boxing Day"", 0, 100 },
            -- De facto Holidays in Sweden - not official public holidays
            { ""SE"", 12, 24, 0, 0, 0, ""Christmas Eve"", 0, 50 },
            { ""SE"", 12, 31, 0, 0, 0, ""New Year's Eve"", 0, 50 },
            { ""SE"", 98, -1, 0, 0, 0, ""Midsummer Eve"", 0, 50 },
            -- Midsummer Day is a Saturday
            -- { ""SE"", 98, 0, 0, 0, ""Midsummer Day"", 0, 50 },
            ------------------------------------------------------------            
            --
            -- BE = Belgium
            { ""BE"", 1, 1, 0, 0, 0, ""New Year's Day"", 0, 100 },
            { ""BE"", 99, 1, 0, 0, 0, ""Easter Monday"", 0, 50 },
            { ""BE"", 99, 39, 0, 0, 0, ""Ascension Day"", 0, 50 },
            { ""BE"", 99, 50, 0, 0, 0, ""Whit Monday"", 0, 50 },
            { ""BE"", 5, 1, 0, 0, 0, ""Labour Day"", 0, 100 },
            { ""BE"", 7, 21, 0, 0, 0, ""Belgian National DayDay"", 0, 100 },
            { ""BE"", 8, 15, 0, 0, 0, ""Assumption Day"", 0, 100 },
            { ""BE"", 11, 1, 0, 0, 0, ""All Saints' Day"", 0, 100 },
            { ""BE"", 11, 11, 0, 0, 0, ""Armistice Day"", 0, 100 },
            { ""BE"", 12, 25, 0, 0,0, ""Christmas Day"", 0, 100 },
			--
            -- PT = Portugal
            { ""PT"", 1, 1, 0, 0, 0, ""New Year's Day"", 0, 100 },
            { ""PT"", 99, -2, 0, 0, 0, ""Good Friday"", 0, 50 },
            { ""PT"", 99, 60, 0, 0, 0, ""Corpus Christi"", 0, 50 },
            { ""PT"", 4, 25, 0, 0, 0, ""Freedom Day"", 0, 100 },
            { ""PT"", 5, 1, 0, 0, 0, ""Labour Day"", 0, 100 },
            { ""PT"", 6, 10, 0, 0, 0, ""Portugal Day"", 0, 100 },
            { ""PT"", 8, 15, 0, 0, 0, ""Assumption Day"", 0, 100 },
            { ""PT"", 10, 5, 0, 0, 0, ""Republic Day"", 0, 100 },
            { ""PT"", 11, 1, 0, 0, 0, ""All Saints' Day"", 0, 100 },
            { ""PT"", 12, 1, 0, 0, 0, ""Restoration of Independence"", 0, 100 },
            { ""PT"", 12, 8, 0, 0, 0, ""Immaculate Conception"", 0, 100 },
            { ""PT"", 12, 25, 0, 0, 0, ""Christmas Day"", 0, 100 }

        }
    )
VAR HolidayDates_ConfigGeneration =
    FILTER (
        HolidayParameters,
        IF (
            CONTAINS ( HolidayParameters, [ISO Country], IsoCountryHolidays )
                || IsoCountryHolidays = """",
            [ISO Country] = IsoCountryHolidays,
            ERROR ( ""IsoCountryHolidays set to an unsupported contry code"" )
        )
    )
VAR HolidayDates_GeneratedRawWithDuplicates =
    GENERATE (
        GENERATE (
            GENERATESERIES ( FirstYear - 1, LastYear + 1, 1 ),
            HolidayDates_ConfigGeneration
        ),
        VAR HolidayYear = [Value]
        VAR EasterDate =
            -- Code adapted from original VB version from https://www.assa.org.au/edm 
            VAR EasterYear = HolidayYear
            VAR FirstDig =
                INT ( EasterYear / 100 )
            VAR Remain19 =
                MOD ( EasterYear, 19 ) //
            -- Calculate PFM date
            VAR temp1 =
                MOD (
                    INT ( ( FirstDig - 15 ) / 2 )
                        + 202
                        - 11 * Remain19
                        + SWITCH (
                            TRUE,
                            FirstDig IN { 21, 24, 25, 27, 28, 29, 30, 31, 32, 34, 35, 38 }, -1,
                            FirstDig IN { 33, 36, 37, 39, 40 }, -2,
                            0
                        ),
                    30
                )
            VAR tA =
                temp1 + 21
                    + IF ( temp1 = 29 || ( temp1 = 28 && Remain19 > 10 ), -1 ) // 
            -- Find the next Sunday
            VAR tB =
                MOD ( tA - 19, 7 )
            VAR tCpre =
                MOD ( 40 - FirstDig, 4 )
            VAR tC =
                tCpre
                    + IF ( tCpre = 3, 1 )
                    + IF ( tCpre > 1, 1 )
            VAR temp2 =
                MOD ( EasterYear, 100 )
            VAR tD =
                MOD ( temp2 + INT ( temp2 / 4 ), 7 )
            VAR tE =
                MOD ( 20 - tB - tC - tD, 7 )
                    + 1
            VAR d = tA + tE //
            -- Return the date
            VAR EasterDay =
                IF ( d > 31, d - 31, d )
            VAR EasterMonth =
                IF ( d > 31, 4, 3 )
            RETURN
                DATE ( EasterYear, EasterMonth, EasterDay ) //
        -- End of code adapted from original VB version from https://www.assa.org.au/edm 
        VAR SwedishMidSummer =
            -- Compute the Midsummer day in Swedish - it is the Saturday between 20 and 26 June
            -- This calculation is valid only for years after 1953 
            -- https://sv.wikipedia.org/wiki/Midsommar_i_Sverige
            VAR __June20 = 
                DATE ( HolidayYear, 6, 20 )
            RETURN
                DATE ( HolidayYear, 6, 20 + (7 - WEEKDAY ( __June20, 1 ) ) )
            -- End of SwedishMidSummer calculation
        VAR HolidayDate =
            SWITCH (
                TRUE,
                [DayNumber] <> 0
                    && [WeekDayNumber] <> 0, ERROR ( ""Wrong configuration in HolidayParameters"" ),
                [DayNumber] <> 0
                    && [MonthNumber] <= 12, DATE ( HolidayYear, [MonthNumber], [DayNumber] ),
                [MonthNumber] = 99, -- Easter offset
                    EasterDate + [DayNumber],
                [MonthNumber] = 98, -- Swedish Midsummer Day
                    SwedishMidSummer + [DayNumber],
                [WeekDayNumber] <> 0,
                    VAR ReferenceDate =
                        DATE ( HolidayYear, 1
                            + MOD ( [MonthNumber] - 1 + IF ( [OffsetWeek] < 0, 1 ), 12 ), 1 )
                            - IF ( [OffsetWeek] < 0, 1 )
                    VAR ReferenceWeekDayNumber =
                        WEEKDAY ( ReferenceDate, 1 ) - 1
                    VAR Offset =
                        [WeekDayNumber] - ReferenceWeekDayNumber
                            + 7 * [OffsetWeek]
                            + IF (
                                [OffsetWeek] > 0,
                                IF ( [WeekDayNumber] >= ReferenceWeekDayNumber, - 7 ),
                                IF ( ReferenceWeekDayNumber >= [WeekDayNumber], 7 )
                            )
                RETURN
                    ReferenceDate + Offset + [OffsetDays],
                ERROR ( ""Wrong configuration in HolidayParameters"" )
            )
        VAR HolidayDay =
            WEEKDAY ( HolidayDate, 1 ) - 1
        VAR SubstituteHolidayOffset =
            IF (
                [SubstituteHoliday] > 0
                    && NOT CONTAINS ( WorkingDays, [WorkingDayNumber], HolidayDay ),
                VAR NextWorkingDay =
                    MINX (
                        FILTER ( WorkingDays, [WorkingDayNumber] > HolidayDay ),
                        [WorkingDayNumber]
                    )
                VAR SubstituteDay =
                    IF (
                        ISBLANK ( NextWorkingDay ),
                        MINX ( WorkingDays, [WorkingDayNumber] ) + 7,
                        NextWorkingDay
                    )
                RETURN
                    SubstituteDay - HolidayDay
                        + ( [SubstituteHoliday] - 1 )
            )
        RETURN
            ROW (
                -- Use DATE function to get a DATE column as a result 
                ""HolidayDate"", DATE ( YEAR ( HolidayDate ), MONTH ( HolidayDate ), DAY ( HolidayDate ) ),
                ""SubstituteHolidayOffset"", SubstituteHolidayOffset
            )
    ) //
VAR HolidayDates_RawDatesUnique = 
    DISTINCT ( 
        SELECTCOLUMNS ( 
            HolidayDates_GeneratedRawWithDuplicates,
            ""HolidayDateUnique"", [HolidayDate]
        )
    )
VAR HolidayDates_GeneratedRaw = 
    GENERATE (
        HolidayDates_RawDatesUnique,
        VAR FilterDate = [HolidayDateUnique]
        RETURN 
            TOPN (
                1,
                FILTER ( 
                    HolidayDates_GeneratedRawWithDuplicates,
                    [HolidayDate] = FilterDate
                ),
                [ConflictPriority],
                ASC,
                [HolidayName], 
                ASC
            )
    )  
VAR HolidayDates_GeneratedSubstitutesOffset =
    SELECTCOLUMNS (
        FILTER ( HolidayDates_GeneratedRawWithDuplicates, [SubstituteHoliday] > 0 ),
        ""Value"", [Value],
        ""ISO Country"", [ISO Country],
        ""MonthNumber"", [MonthNumber],
        ""DayNumber"", [DayNumber],
        ""WeekDayNumber"", [WeekDayNumber],
        ""OffsetWeek"", [OffsetWeek],
        ""HolidayName"", [HolidayName],
        ""SubstituteHoliday"", [SubstituteHoliday],
        ""ConflictPriority"", [ConflictPriority],
        ""HolidayDate"", [HolidayDate],
        ""SubstituteHolidayOffset"", 
            VAR CurrentHolidayDate = [HolidayDate]
            VAR CurrentHolidayName = [HolidayName]
            VAR OriginalSubstituteDate = [HolidayDate] + [SubstituteHolidayOffset]
            VAR OtherHolidays = 
                FILTER ( 
                    HolidayDates_GeneratedRawWithDuplicates, 
                    [HolidayDate] <> CurrentHolidayDate
                    || [HolidayName] <> CurrentHolidayName
                )
            VAR ConflictDay0 = 
                CONTAINS ( 
                    OtherHolidays,
                    [HolidayDate], OriginalSubstituteDate
                )
            VAR ConflictDay1 = 
                ConflictDay0 
                && CONTAINS ( 
                    OtherHolidays,
                    [HolidayDate], OriginalSubstituteDate + 1
                )
            VAR ConflictDay2 = 
                ConflictDay1 
                && CONTAINS ( 
                    OtherHolidays,
                    [HolidayDate], OriginalSubstituteDate + 2
                )
            VAR SubstituteOffsetStep1 = [SubstituteHolidayOffset] + ConflictDay0 + ConflictDay1 + ConflictDay2
            VAR HolidayDateStep1 = CurrentHolidayDate + SubstituteOffsetStep1
            VAR HolidayDayStep1 =
                WEEKDAY ( HolidayDateStep1, 1 ) - 1
            VAR SubstituteHolidayOffsetNonWorkingDays =
                IF (
                    NOT CONTAINS ( WorkingDays, [WorkingDayNumber], HolidayDayStep1 ),
                    VAR NextWorkingDayStep2 =
                        MINX (
                            FILTER ( WorkingDays, [WorkingDayNumber] > HolidayDayStep1 ),
                            [WorkingDayNumber]
                        )
                    VAR SubstituteDay =
                        IF (
                            ISBLANK ( NextWorkingDayStep2 ),
                            MINX ( WorkingDays, [WorkingDayNumber] ) + 7,
                            NextWorkingDayStep2
                        )
                    RETURN SubstituteDay - HolidayDateStep1
                )
            VAR SubstituteOffsetStep2 = SubstituteOffsetStep1 + SubstituteHolidayOffsetNonWorkingDays
            VAR SubstituteDateStep2 = OriginalSubstituteDate + SubstituteOffsetStep2
            VAR ConflictDayStep2_0 = 
                CONTAINS ( 
                    OtherHolidays,
                    [HolidayDate], SubstituteDateStep2
                )
            VAR ConflictDayStep2_1 = 
                ConflictDayStep2_0
                && CONTAINS ( 
                    OtherHolidays,
                    [HolidayDate], SubstituteDateStep2 + 1
                )
            VAR ConflictDayStep2_2 = 
                ConflictDayStep2_1 
                && CONTAINS ( 
                    OtherHolidays,
                    [HolidayDate], SubstituteDateStep2 + 2
                )
            VAR FinalSubstituteHolidayOffset = 
                SubstituteOffsetStep2 + ConflictDayStep2_0 + ConflictDayStep2_1 + ConflictDayStep2_2
            RETURN
                FinalSubstituteHolidayOffset
        )
VAR HolidayDates_GeneratedSubstitutesExpanded =
    ADDCOLUMNS (
        HolidayDates_GeneratedSubstitutesOffset,
        ""ReplacementHolidayDate"", [HolidayDate] + [SubstituteHolidayOffset]
    )
VAR HolidayDates_GeneratedSubstitutesUnique =
    DISTINCT ( 
        SELECTCOLUMNS ( 
            HolidayDates_GeneratedSubstitutesExpanded,
            ""UniuqeReplacementHolidayDate"", [ReplacementHolidayDate]
        )
    )
VAR HolidayDates_GeneratedSubstitutes =
    GENERATE (
        HolidayDates_GeneratedSubstitutesUnique,
        TOPN (
            1,
            FILTER ( 
                HolidayDates_GeneratedSubstitutesExpanded,
                [UniuqeReplacementHolidayDate] = [ReplacementHolidayDate]
            ),
            [ConflictPriority],
            ASC,
            [HolidayName], 
            ASC
        )
    )  
VAR HolidayDates_Generated =
    UNION (
        SELECTCOLUMNS (
            HolidayDates_GeneratedRaw,
            ""HolidayDate"", [HolidayDate],
            ""HolidayName"", [HolidayName]
        ),
        SELECTCOLUMNS (
            FILTER ( HolidayDates_GeneratedSubstitutes, [SubstituteHolidayOffset] <> 0 ), 
            ""HolidayDate"", [HolidayDate] + [SubstituteHolidayOffset],
            ""HolidayName"", InLieuOf_prefix & [HolidayName]
                & InLieuOf_suffix
        )
    )
-- Alternative way to express holidays: create a table with the list of the dates
-- The following table should be used instead of HolidayDates_Generated in the following 
-- HolidayDates variable if you want to use a fixed list of holidays
VAR HolidayDates_US_ExplicitDates =
    DATATABLE (
        ""HolidayDate"", DATETIME,
        ""HolidayName"", STRING,
        {
            { ""2008-01-01"", ""New Year's Day"" },
            { ""2008-12-25"", ""Christmas Day"" },
            -------------------------
            { ""2008-11-27"", ""Thanksgiving Day"" },
            { ""2009-11-26"", ""Thanksgiving Day"" },
            { ""2010-11-25"", ""Thanksgiving Day"" },
            { ""2011-11-24"", ""Thanksgiving Day"" },
            { ""2012-11-22"", ""Thanksgiving Day"" },
            { ""2013-11-28"", ""Thanksgiving Day"" },
            { ""2014-11-27"", ""Thanksgiving Day"" },
            { ""2015-11-26"", ""Thanksgiving Day"" },
            { ""2016-11-24"", ""Thanksgiving Day"" },
            { ""2017-11-23"", ""Thanksgiving Day"" },
            { ""2018-11-22"", ""Thanksgiving Day"" },
            { ""2019-11-28"", ""Thanksgiving Day"" },
            { ""2020-11-26"", ""Thanksgiving Day"" }
        }
    )
VAR HolidayDates =
    SELECTCOLUMNS (
        HolidayDates_Generated,
        ""Date"", [HolidayDate],
        ""Holiday Name"", [HolidayName]
    ) //
------------------------------------------------------------
--  
-- End of Configuration
--
------------------------------------------------------------
--  
-- The following variables define 
-- the content of the calendar tables
--
------------------------------------------------------------
------------------------------------------------------------
VAR FirstDayCalendar =
    DATE ( FirstYear - 1, 1, 1 )
VAR LastDayCalendar =
    DATE ( LastYear + 1, 12, 31 )
VAR WeekDayCalculationType =
    IF ( FirstDayOfWeek = 0, 7, FirstDayOfWeek )
        + 10
VAR OffsetFiscalYear = 
    IF ( FiscalCalendarFirstMonth > 1, 1, 0 )
VAR WeeklyFiscalPeriods =
    GENERATE (
        SELECTCOLUMNS (
            GENERATESERIES ( FirstYear - OffsetFiscalYear, LastYear + OffsetFiscalYear, 1 ),
            ""CalendarType"", ""Weekly"",
            ""Fiscal YearNumber"", [Value]
        ),
        VAR StartFiscalYearNumber = [Fiscal YearNumber] - (OffsetFiscalYear * TypeStartFiscalYear)
        VAR FirstDayCurrentYear =
            DATE ( StartFiscalYearNumber, FiscalCalendarFirstMonth, 1 )
        VAR FirstDayNextYear =
            DATE ( StartFiscalYearNumber + 1, FiscalCalendarFirstMonth, 1 )
        VAR DayOfWeekNumberCurrentYear =
            WEEKDAY ( FirstDayCurrentYear, WeekDayCalculationType )
        VAR OffsetStartCurrentFiscalYear =
            SWITCH (
                WeeklyType,
                ""Last"", 1 - DayOfWeekNumberCurrentYear,
                ""Nearest"", IF (
                    DayOfWeekNumberCurrentYear >= 5,
                    8 - DayOfWeekNumberCurrentYear,
                    1 - DayOfWeekNumberCurrentYear
                ),
                ERROR ( ""Unkonwn WeeklyType definition"" )
            )
        VAR DayOfWeekNumberNextYear =
            WEEKDAY ( FirstDayNextYear, WeekDayCalculationType )
        VAR OffsetStartNextFiscalYear =
            SWITCH (
                WeeklyType,
                ""Last"", - DayOfWeekNumberNextYear,
                ""Nearest"", IF (
                    DayOfWeekNumberNextYear >= 5,
                    7 - DayOfWeekNumberNextYear,
                    - DayOfWeekNumberNextYear
                ),
                ERROR ( ""Unkonwn WeeklyType definition : "" )
            )
        VAR FirstDayOfFiscalYear = FirstDayCurrentYear + OffsetStartCurrentFiscalYear
        VAR LastDayOfFiscalYear = FirstDayNextYear + OffsetStartNextFiscalYear
        RETURN
            ROW ( ""FirstDayOfYear"", FirstDayOfFiscalYear,
            ""LastDayOfYear"", LastDayOfFiscalYear )
    )
VAR CheckFirstDayOfWeek =
    IF (
        UseCustomFiscalPeriods && (NOT IgnoreWeeklyFiscalPeriods)
        && WEEKDAY ( MINX ( CustomFiscalPeriods, [FirstDayOfYear] ), 1 )
              <> ( FirstDayOfWeek + 1 ),
        ERROR ( ""CustomFiscalPeriods table does not match FirstDayOfWeek setting"" ),
        TRUE
    )
VAR CustomFiscalPeriodsWithType =
    GENERATE (
        ROW ( ""CalendarType"", ""Custom"" ),
        FILTER ( CustomFiscalPeriods, CheckFirstDayOfWeek )
    )
VAR FiscalPeriods =
    SELECTCOLUMNS (
        FILTER (
            UNION ( 
                FILTER ( WeeklyFiscalPeriods, NOT IgnoreWeeklyFiscalPeriods ),
                CustomFiscalPeriodsWithType 
            ),
            [CalendarType] = WeeklyCalendarType
        ),
        ""FW YearNumber"", [Fiscal YearNumber],
        ""FW StartOfYear"", [FirstDayOfYear],
        ""FW EndOfYear"", [LastDayOfYear]
    )
VAR WeeksInP1 =
    SWITCH (
        QuarterWeekType,
        ""445"", 4,
        ""454"", 4,
        ""544"", 5,
        ERROR ( ""QuarterWeekType only supports 445, 454, and 544"" )
    )
VAR WeeksInP2 =
    SWITCH (
        QuarterWeekType,
        ""445"", 4,
        ""454"", 5,
        ""544"", 4,
        ERROR ( ""QuarterWeekType only supports 445, 454, and 544"" )
    )
VAR WeeksInP3 =
    SWITCH (
        QuarterWeekType,
        ""445"", 5,
        ""454"", 4,
        ""544"", 4,
        ERROR ( ""QuarterWeekType only supports 445, 454, and 544"" )
    )
VAR FirstSundayReference =
    DATE ( 1900, 12, 30 ) -- Do not change this 
VAR FirstWeekReference = FirstSundayReference + FirstDayOfWeek
VAR RawDays =
    CALENDAR ( FirstDayCalendar, LastDayCalendar )
VAR CalendarGregorianPrefixSpace =
    IF ( CalendarGregorianPrefix <> """", CalendarGregorianPrefix & "" "", """" )
VAR FiscalGregorianPrefixSpace =
    IF ( FiscalGregorianPrefix <> """", FiscalGregorianPrefix & "" "", """" )
VAR FiscalWeeklyPrefixSpace =
    IF ( FiscalWeeklyPrefix <> """", FiscalWeeklyPrefix & "" "", """" )
VAR CustomFiscalRawDays =
    GENERATE ( FiscalPeriods, CALENDAR ( [FW StartOfYear], [FW EndOfYear] ) )
VAR CalendarStandardGregorianBase =
    GENERATE (
        NATURALLEFTOUTERJOIN ( RawDays, HolidayDates ),
        VAR CalDate = [Date]
        VAR CalYear =
            YEAR ( [Date] )
        VAR CalMonthNumber =
            MONTH ( [Date] )
        VAR CalQuarterNumber =
            ROUNDUP ( CalMonthNumber / 3, 0 )
        VAR CalDay =
            DAY ( [Date] )
        VAR CalWeekNumber =
            WEEKNUM ( CalDate, WeekDayCalculationType )
        VAR CalDayOfMonth =
            DAY ( CalDate )
        VAR WeekDayNumber =
            WEEKDAY ( CalDate, WeekDayCalculationType )
        VAR YearWeekNumber =
            INT ( DIVIDE ( CalDate - FirstWeekReference, 7 ) )
        VAR CalendarFirstDayOfYear =
            DATE ( CalYear, 1, 1 )
        VAR CalendarDayOfYear =
            INT ( CalDate - CalendarFirstDayOfYear + 1 )
        VAR IsWorkingDay =
            CONTAINS ( WorkingDays, [WorkingDayNumber], WEEKDAY ( CalDate, 1 ) - 1 )
                && ISBLANK ( [Holiday Name] )
        VAR _CheckLeapYearBefore =
            CalYear -
            IF ( (CalMonthNumber = 2 && CalDayOfMonth < 29)
                     || CalMonthNumber < 2,
                1,
                0 )
        VAR LeapYearsBefore1900 =
            INT ( 1899 / 4 )
                - INT ( 1899 / 100 )
                + INT ( 1899 / 400 )
        VAR LeapYearsBetween =
            INT ( _CheckLeapYearBefore / 4 )
                - INT ( _CheckLeapYearBefore / 100 )
                + INT ( _CheckLeapYearBefore / 400 )
                - LeapYearsBefore1900
        VAR Sequential365DayNumber =
            INT ( CalDate - LeapYearsBetween ) 
        RETURN
            ROW (
                ""DateKey"", CalYear * 10000
                    + CalMonthNumber * 100
                    + CalDay,
                ""Calendar YearNumber"", CalYear,
                ""Calendar Year"", CalendarGregorianPrefixSpace & CalYear,
                ""Calendar QuarterNumber"", CalQuarterNumber,
                ""Calendar Quarter"", CalendarGregorianPrefix & ""Q""
                    & CalQuarterNumber
                    & "" "",
                ""Calendar YearQuarterNumber"", CalYear * 4
                    - 1
                    + CalQuarterNumber,
                ""Calendar Quarter Year"", CalendarGregorianPrefix & ""Q""
                    & CalQuarterNumber
                    & "" ""
                    & CalYear,
                ""Calendar MonthNumber"", CalMonthNumber,
                ""Calendar Month"", FORMAT ( CalDate, ""mmm"" ),
                ""Calendar YearMonthNumber"", CalYear * 12
                    - 1
                    + CalMonthNumber,
                ""Calendar Month Year"", FORMAT ( CalDate, ""mmm"" ) & "" ""
                    & CalYear,
                ""Calendar WeekNumber"", CalWeekNumber,
                ""Calendar Week"", CalendarGregorianPrefix & ""W""
                    & FORMAT ( CalWeekNumber, ""00"" ),
                ""Calendar YearWeekNumber"", YearWeekNumber,
                ""Calendar Week Year"", CalendarGregorianPrefix & ""W""
                    & FORMAT ( CalWeekNumber, ""00"" )
                    & ""-""
                    & CalYear,
                ""Calendar WeekYearOrder"", CalYear * 100
                    + CalWeekNumber,
                ""Calendar DayOfYearNumber"", CalendarDayOfYear,
                ""Day of Month"", CalDayOfMonth,
                ""WeekDayNumber"", WeekDayNumber,
                ""Week Day"", FORMAT ( CalDate, ""ddd"" ),
                ""IsWorkingDay"", IsWorkingDay,
                ""Day Type"", IF ( IsWorkingDay, WorkingDayType, NonWorkingDayType ),
                ""Sequential365DayNumber"", Sequential365DayNumber
            )
    )
VAR CalendarStandardGregorian =
    GENERATE (
        CalendarStandardGregorianBase,
        VAR CalDate = [Date]
        VAR YearNumber = [Calendar YearNumber]
        VAR MonthNumber = [Calendar MonthNumber]
        VAR YearWeekNumber = [Calendar YearWeekNumber]
        VAR YearMonthNumber = [Calendar YearMonthNumber]
        VAR YearQuarterNumber = [Calendar YearQuarterNumber]
        VAR CurrentWeekPos =
            AVERAGEX (
                FILTER ( CalendarStandardGregorianBase, [Date] = TodayReference ),
                [Calendar YearWeekNumber]
            )
        VAR CurrentMonthPos =
            AVERAGEX (
                FILTER ( CalendarStandardGregorianBase, [Date] = TodayReference ),
                [Calendar YearMonthNumber]
            )
        VAR CurrentQuarterPos =
            AVERAGEX (
                FILTER ( CalendarStandardGregorianBase, [Date] = TodayReference ),
                [Calendar YearQuarterNumber]
            )
        VAR CurrentYearPos =
            AVERAGEX (
                FILTER ( CalendarStandardGregorianBase, [Date] = TodayReference ),
                [Calendar YearNumber]
            )
        VAR RelativeWeekPos = CurrentWeekPos - YearWeekNumber
        VAR RelativeMonthPos = CurrentMonthPos - YearMonthNumber
        VAR RelativeQuarterPos = CurrentQuarterPos - YearQuarterNumber
        VAR RelativeYearPos = CurrentYearPos - YearNumber
        VAR CalStartOfMonth =
            DATE ( YearNumber, MonthNumber, 1 )
        VAR CalEndOfMonth =
            EOMONTH ( CalDate, 0 )
        VAR CalMonthDays = 
            INT ( CalEndOfMonth - CalStartOfMonth + 1 ) 
        VAR CalDayOfMonthNumber =
            INT ( CalDate - CalStartOfMonth + 1 )
        VAR CalStartOfQuarter =
            MINX (
                FILTER (
                    CalendarStandardGregorianBase,
                    [Calendar YearQuarterNumber] = YearQuarterNumber
                ),
                [Date]
            )
        VAR CalEndOfQuarter =
            MAXX (
                FILTER (
                    CalendarStandardGregorianBase,
                    [Calendar YearQuarterNumber] = YearQuarterNumber
                ),
                [Date]
            )
        VAR CalQuarterDays =
            INT ( CalEndOfQuarter - CalStartOfQuarter + 1 )         
        VAR CalDayOfQuarterNumber =
            INT ( CalDate - CalStartOfQuarter + 1 )
        VAR CalYearDays =
            INT ( DATE ( YearNumber, 12, 31 ) - DATE ( YearNumber, 1, 1 ) + 1 )
        VAR CalDatePreviousWeek = CalDate - 7
        VAR CalDatePreviousMonth = 
            MAXX (
                FILTER (
                    CalendarStandardGregorianBase,
                    [Calendar YearMonthNumber] = YearMonthNumber - 1
                    &&
                    ( [Day of Month] <= CalDayOfMonthNumber
                      || CalDayOfMonthNumber = CalMonthDays )
                ),
                [Date]
            )
        VAR CalDatePreviousQuarter = 
            MAXX (
                FILTER (
                    CalendarStandardGregorianBase,
                    [Calendar YearMonthNumber] = YearMonthNumber - 3
                    &&
                    ( [Day of Month] <= CalDayOfMonthNumber
                      || CalDayOfMonthNumber = CalMonthDays )
                ),
                [Date]
            )
        VAR CalDatePreviousYear = 
            MAXX (
                FILTER (
                    CalendarStandardGregorianBase,
                    [Calendar YearMonthNumber] = YearMonthNumber - 12
                    &&
                    ( [Day of Month] <= CalDayOfMonthNumber
                      || CalDayOfMonthNumber = CalMonthDays )
                ),
                [Date]
            )
        VAR CalStartOfYear =
            DATE ( YearNumber, 1, 1 )
        VAR CalEndOfYear =
            DATE ( YearNumber, 12, 31 )
        RETURN
            ROW ( ""Calendar RelativeWeekPos"", RelativeWeekPos,
            ""Calendar RelativeMonthPos"", RelativeMonthPos,
            ""Calendar RelativeQuarterPos"", RelativeQuarterPos,
            ""Calendar RelativeYearPos"", RelativeYearPos,
            ""Calendar StartOfMonth"", CalStartOfMonth,
            ""Calendar EndOfMonth"", CalEndOfMonth,
            ""Calendar DayOfMonthNumber"", CalDayOfMonthNumber,
            ""Calendar StartOfQuarter"", CalStartOfQuarter,
            ""Calendar EndOfQuarter"", CalEndOfQuarter,
            ""Calendar DayOfQuarterNumber"", CalDayOfQuarterNumber,            
            ""Calendar StartOfYear"", CalStartOfYear,
            ""Calendar EndOfYear"", CalEndOfYear,
            ""Calendar DatePreviousWeek"", CalDatePreviousWeek,
            ""Calendar DatePreviousMonth"", CalDatePreviousMonth,
            ""Calendar DatePreviousQuarter"", CalDatePreviousQuarter,
            ""Calendar DatePreviousYear"", CalDatePreviousYear,
            ""Calendar MonthDays"", CalMonthDays,
            ""Calendar QuarterDays"", CalQuarterDays,
            ""Calendar YearDays"", CalYearDays
            )
    )
VAR FiscalStandardGregorianBase =
    GENERATE (
        NATURALLEFTOUTERJOIN ( RawDays, HolidayDates ),
        VAR FiscalDate = [Date]
        VAR CalYear =
            YEAR ( FiscalDate )
        VAR CalMonthNumber =
            MONTH ( FiscalDate )
        VAR CalDay =
            DAY ( [Date] )
        VAR WeekDayNumber =
            WEEKDAY ( FiscalDate, WeekDayCalculationType )
        VAR YearWeekNumber =
            INT ( DIVIDE ( FiscalDate - FirstWeekReference, 7 ) )
        VAR FiscalYear =
            CalYear 
                + IF ( FiscalCalendarFirstMonth > 1,
                      IF ( CalMonthNumber >= FiscalCalendarFirstMonth, 
                          TypeStartFiscalYear,                     -- TypeStartFiscalYear = 1
                          -1 * (TypeStartFiscalYear = 0)           -- TypeStartFiscalYear = 0
                      )
                  )
        VAR FiscalMonthNumber =
            MOD ( CalMonthNumber - FiscalCalendarFirstMonth, 12 )
                + 1
        VAR FiscalFirstDayOfYear =
            DATE ( FiscalYear - (OffsetFiscalYear * TypeStartFiscalYear), FiscalCalendarFirstMonth, 1 )
        VAR FiscalDayOfYear =
            INT ( FiscalDate - FiscalFirstDayOfYear + 1 )
        VAR FiscalFirstYearWeekNumber =
            INT ( DIVIDE ( FiscalFirstDayOfYear - FirstWeekReference, 7 ) )
        VAR FiscalWeekNumber = YearWeekNumber - FiscalFirstYearWeekNumber
            + 1
        VAR FiscalQuarterNumber =
            ROUNDUP ( FiscalMonthNumber / 3, 0 )
        VAR IsWorkingDay =
            CONTAINS ( WorkingDays, [WorkingDayNumber], WEEKDAY ( FiscalDate, 1 ) - 1 )
                && ISBLANK ( [Holiday Name] )
        RETURN
            ROW (
                ""DateKey"", CalYear * 10000
                    + CalMonthNumber * 100
                    + CalDay,
                ""Fiscal Year"", FiscalGregorianPrefixSpace & FiscalYear,
                ""Fiscal YearNumber"", FiscalYear,
                ""Fiscal QuarterNumber"", FiscalQuarterNumber,
                ""Fiscal Quarter"", FiscalGregorianPrefix & ""Q""
                    & FiscalQuarterNumber
                    & "" "",
                ""Fiscal YearQuarterNumber"", FiscalYear * 4
                    - 1
                    + FiscalQuarterNumber,
                ""Fiscal Quarter Year"", FiscalGregorianPrefix & ""Q""
                    & FiscalQuarterNumber
                    & "" ""
                    & FiscalYear,
                ""Fiscal MonthNumber"", FiscalMonthNumber,
                ""Fiscal Month"", FORMAT ( FiscalDate, ""mmm"" ),
                ""Fiscal YearMonthNumber"", FiscalYear * 12
                    - 1
                    + FiscalMonthNumber,
                ""Fiscal Month Year"", FORMAT ( FiscalDate, ""mmm"" ) & "" ""
                    & CalYear,
                ""Fiscal WeekNumber"", FiscalWeekNumber,
                ""Fiscal Week"", FiscalGregorianPrefix & ""W""
                    & FORMAT ( FiscalWeekNumber, ""00"" ),
                ""Fiscal YearWeekNumber"", YearWeekNumber,
                ""Fiscal Week Year"", FiscalGregorianPrefix & ""W""
                    & FORMAT ( FiscalWeekNumber, ""00"" )
                    & ""-""
                    & FiscalYear,
                ""Fiscal WeekYearOrder"", FiscalYear * 100
                    + FiscalWeekNumber,
                ""Fiscal DayOfYearNumber"", FiscalDayOfYear,
                ""Day of Month"", DAY ( FiscalDate ),
                ""WeekDayNumber"", WeekDayNumber,
                ""Week Day"", FORMAT ( FiscalDate, ""ddd"" ),
                ""IsWorkingDay"", IsWorkingDay,
                ""Day Type"", IF ( IsWorkingDay, WorkingDayType, NonWorkingDayType )
            )
    )
VAR FiscalStandardGregorian =
    GENERATE (
        FiscalStandardGregorianBase,
        VAR FiscalDate = [Date]
        VAR FiscalYearNumber = [Fiscal YearNumber]
        VAR MonthNumber = [Fiscal MonthNumber]
        VAR CalendarYearNumber =
            YEAR ( FiscalDate )
        VAR CalendarMonthNumber =
            MONTH ( FiscalDate )
        VAR YearWeekNumber = [Fiscal YearWeekNumber]
        VAR YearMonthNumber = [Fiscal YearMonthNumber]
        VAR YearQuarterNumber = [Fiscal YearQuarterNumber]
        VAR CurrentWeekPos =
            AVERAGEX (
                FILTER ( FiscalStandardGregorianBase, [Date] = TodayReference ),
                [Fiscal YearWeekNumber]
            )
        VAR CurrentMonthPos =
            AVERAGEX (
                FILTER ( FiscalStandardGregorianBase, [Date] = TodayReference ),
                [Fiscal YearMonthNumber]
            )
        VAR CurrentQuarterPos =
            AVERAGEX (
                FILTER ( FiscalStandardGregorianBase, [Date] = TodayReference ),
                [Fiscal YearQuarterNumber]
            )
        VAR CurrentYearPos =
            AVERAGEX (
                FILTER ( FiscalStandardGregorianBase, [Date] = TodayReference ),
                [Fiscal YearNumber]
            )
        VAR RelativeWeekPos = CurrentWeekPos - YearWeekNumber
        VAR RelativeMonthPos = CurrentMonthPos - YearMonthNumber
        VAR RelativeQuarterPos = CurrentQuarterPos - YearQuarterNumber
        VAR RelativeYearPos = CurrentYearPos - FiscalYearNumber
        VAR FiscalStartOfMonth =
            DATE ( CalendarYearNumber, CalendarMonthNumber, 1 )
        VAR FiscalEndOfMonth =
            EOMONTH ( FiscalDate, 0 )
        VAR FiscalMonthDays = 
            INT ( FiscalEndOfMonth - FiscalStartOfMonth + 1 ) 
        VAR FiscalDayOfMonthNumber =
            INT ( FiscalDate - FiscalStartOfMonth + 1 )
        VAR FiscalStartOfQuarter =
            MINX (
                FILTER (
                    FiscalStandardGregorianBase,
                    [Fiscal YearQuarterNumber] = YearQuarterNumber
                ),
                [Date]
            )
        VAR FiscalEndOfQuarter =
            MAXX (
                FILTER (
                    FiscalStandardGregorianBase,
                    [Fiscal YearQuarterNumber] = YearQuarterNumber
                ),
                [Date]
            )
        VAR FiscalQuarterDays = 
            INT ( FiscalEndOfQuarter - FiscalStartOfQuarter + 1 )
        VAR FiscalFirstDayOfYear =
            DATE ( FiscalYearNumber - OffsetFiscalYear, FiscalCalendarFirstMonth, 1 )
        VAR FiscalLastDayOfYear =
            DATE ( FiscalYearNumber + (1 * (OffsetFiscalYear = 0)), FiscalCalendarFirstMonth, 1 ) - 1
        VAR FiscalYearDays = 
            INT ( FiscalLastDayOfYear - FiscalFirstDayOfYear + 1 ) 
        VAR FiscalDayOfQuarterNumber =
            INT ( FiscalDate - FiscalStartOfQuarter + 1 )
        VAR FiscalStartOfYear =
            MINX (
                FILTER (
                    FiscalStandardGregorianBase,
                    [Fiscal YearNumber] = FiscalYearNumber
                ),
                [Date]
            )
        VAR FiscalEndOfYear =
            MAXX (
                FILTER (
                    FiscalStandardGregorianBase,
                    [Fiscal YearNumber] = FiscalYearNumber
                ),
                [Date]
            )
        VAR FiscalDatePreviousWeek = FiscalDate - 7
        VAR FiscalDatePreviousMonth = 
            MAXX (
                FILTER (
                    FiscalStandardGregorianBase,
                    [Fiscal YearMonthNumber] = YearMonthNumber - 1
                    &&
                    ( [Day of Month] <= FiscalDayOfMonthNumber
                      || FiscalDayOfMonthNumber = FiscalMonthDays )
                ),
                [Date]
            )
        VAR FiscalDatePreviousQuarter = 
            MAXX (
                FILTER (
                    FiscalStandardGregorianBase,
                    [Fiscal YearMonthNumber] = YearMonthNumber - 3
                    &&
                    ( [Day of Month] <= FiscalDayOfMonthNumber
                      || FiscalDayOfMonthNumber = FiscalMonthDays )
                ),
                [Date]
            )
        VAR FiscalDatePreviousYear = 
            MAXX (
                FILTER (
                    FiscalStandardGregorianBase,
                    [Fiscal YearMonthNumber] = YearMonthNumber - 12
                    &&
                    ( [Day of Month] <= FiscalDayOfMonthNumber
                      || FiscalDayOfMonthNumber = FiscalMonthDays )
                ),
                [Date]
            )
        RETURN
            ROW ( ""Fiscal RelativeWeekPos"", RelativeWeekPos,
            ""Fiscal RelativeMonthPos"", RelativeMonthPos,
            ""Fiscal RelativeQuarterPos"", RelativeQuarterPos,
            ""Fiscal RelativeYearPos"", RelativeYearPos,
            ""Fiscal StartOfMonth"", FiscalStartOfMonth,
            ""Fiscal EndOfMonth"", FiscalEndOfMonth,
            ""Fiscal DayOfMonthNumber"", FiscalDayOfMonthNumber,
            ""Fiscal StartOfQuarter"", FiscalStartOfQuarter,
            ""Fiscal EndOfQuarter"", FiscalEndOfQuarter,
            ""Fiscal DayOfQuarterNumber"", FiscalDayOfQuarterNumber,
            ""Fiscal StartOfYear"", FiscalStartOfYear,
            ""Fiscal EndOfYear"", FiscalEndOfYear,
            ""Fiscal DatePreviousWeek"", FiscalDatePreviousWeek,
            ""Fiscal DatePreviousMonth"", FiscalDatePreviousMonth,
            ""Fiscal DatePreviousQuarter"", FiscalDatePreviousQuarter,
            ""Fiscal DatePreviousYear"", FiscalDatePreviousYear,
            ""Fiscal MonthDays"", FiscalMonthDays,
            ""Fiscal QuarterDays"", FiscalQuarterDays,
            ""Fiscal YearDays"", FiscalYearDays
           )
    )
VAR FiscalWeeksBase =
    GENERATE (
        NATURALLEFTOUTERJOIN ( CustomFiscalRawDays, HolidayDates ),
        VAR CalDate = [Date]
        VAR FwFirstDayOfYear = [FW StartOfYear]
        VAR FwDayOfYear =
            INT ( CalDate - FwFirstDayOfYear + 1 )
        VAR CalYear =
            YEAR ( [Date] )
        VAR CalMonthNumber =
            MONTH ( [Date] )
        VAR CalDay =
            DAY ( [Date] )
        VAR FwDayOfYearNumber = CalDate - [FW StartOfYear]
            + 1
        VAR FwWeekNumber =
            INT ( CEILING ( FwDayOfYearNumber / 7, 1 ) )
        VAR FwPeriodNumber = 
            IF ( FwWeekNumber > 52, 14, ROUNDUP ( FwWeekNumber / 4, 0 ) )
        VAR FwYearNumber = [FW YearNumber]
        VAR FwQuarterNumber =
            IF ( FwWeekNumber > 52, 4, ROUNDUP ( FwWeekNumber / 13, 0 ) )
        VAR FwWeekInQuarterNumber =
            IF ( FwWeekNumber > 52, 14, FwWeekNumber - 13 * ( FwQuarterNumber - 1 ) )
        VAR FwMonthNumber =
            ( FwQuarterNumber - 1 )
                * 3
                + SWITCH (
                    TRUE,
                    FwWeekInQuarterNumber <= WeeksInP1, 1,
                    FwWeekInQuarterNumber
                        <= ( WeeksInP1 + WeeksInP2 ), 2,
                    3
                )
        VAR WeekDayNumber =
            WEEKDAY ( CalDate, WeekDayCalculationType )
        VAR FirstDayOfWeek = [Date] - WeekDayNumber
            + 1
        VAR LastDayOfWeek = FirstDayOfWeek + 6
        VAR IsWorkingDay =
            CONTAINS ( WorkingDays, [WorkingDayNumber], WEEKDAY ( CalDate, 1 ) - 1 )
                && ISBLANK ( [Holiday Name] )
        RETURN
            ROW (
                ""DateKey"", CalYear * 10000
                    + CalMonthNumber * 100
                    + CalDay,
                // ""FW YearNumber"", FwYearNumber, -- It is already in the first set of columns of the GENERATE function
                ""FW Year"", FiscalWeeklyPrefixSpace & FwYearNumber,
                ""FW QuarterNumber"", FwQuarterNumber,
                ""FW Quarter"", FiscalWeeklyPrefix & ""Q""
                    & FwQuarterNumber,
                ""FW YearQuarterNumber"", FwYearNumber * 4
                    - 1
                    + FwQuarterNumber,
                ""FW Quarter Year"", FiscalWeeklyPrefix & ""Q""
                    & FwQuarterNumber
                    & "" ""
                    & FwYearNumber,
                ""FW MonthNumber"", FwMonthNumber,
                ""FW Month"", FiscalWeeklyPrefix & ""P""
                    & FORMAT ( FwMonthNumber, ""00"" ),
                ""FW YearMonthNumber"", FwYearNumber * 12
                    - 1
                    + FwMonthNumber,
                ""FW Month Year"", FiscalWeeklyPrefix & ""P""
                    & FORMAT ( FwMonthNumber, ""00"" )
                    & "" ""
                    & FwYearNumber,
                ""FW WeekNumber"", FwWeekNumber,
                ""FW Week"", FiscalWeeklyPrefix & ""W""
                    & FORMAT ( FwWeekNumber, ""00"" ),
                ""FW PeriodNumber"", FwPeriodNumber,
                ""FW Period"", FiscalWeeklyPrefix & ""P""
                    & FORMAT ( FwPeriodNumber, ""00"" ),
                ""FW YearWeekNumber"", INT ( DIVIDE ( CalDate - FirstWeekReference, 7 ) )
                    + 1,
                ""FW Week Year"", FiscalWeeklyPrefix & ""W""
                    & FORMAT ( FwWeekNumber, ""00"" )
                    & "" ""
                    & FwYearNumber,
                ""FW StartOfWeek"", FirstDayOfWeek,
                ""FW EndOfWeek"", LastDayOfWeek,
                ""WeekDayNumber"", WeekDayNumber,
                ""Week Day"", FORMAT ( CalDate, ""ddd"" ),
                ""FW DayOfYearNumber"", FwDayOfYear,
                ""IsWorkingDay"", IsWorkingDay,
                ""Day Type"", IF ( IsWorkingDay, WorkingDayType, NonWorkingDayType )
            )
    )
VAR FiscalWeeks_Pre = 
    GENERATE (
        FiscalWeeksBase,
        VAR CalDate = [Date]
        VAR FWYearNumber = [FW YearNumber]
        VAR FwYearWeekNumber = [FW YearWeekNumber]
        VAR FwYearMonthNumber = [FW YearMonthNumber]
        VAR FwYearQuarterNumber = [FW YearQuarterNumber]
        VAR CurrentWeekPos =
            AVERAGEX (
                FILTER ( FiscalWeeksBase, [Date] = TodayReference ),
                [FW YearWeekNumber]
            )
        VAR CurrentMonthPos =
            AVERAGEX (
                FILTER ( FiscalWeeksBase, [Date] = TodayReference ),
                [FW YearMonthNumber]
            )
        VAR CurrentQuarterPos =
            AVERAGEX (
                FILTER ( FiscalWeeksBase, [Date] = TodayReference ),
                [FW YearQuarterNumber]
            )
        VAR CurrentYearPos =
            AVERAGEX (
                FILTER ( FiscalWeeksBase, [Date] = TodayReference ),
                [FW YearNumber]
            )
        VAR RelativeWeekPos = CurrentWeekPos - FwYearWeekNumber
        VAR RelativeMonthPos = CurrentMonthPos - FwYearMonthNumber
        VAR RelativeQuarterPos = CurrentQuarterPos - FwYearQuarterNumber
        VAR RelativeYearPos = CurrentYearPos - FwYearNumber
        VAR FwStartOfMonth =
            MINX (
                FILTER ( FiscalWeeksBase, [FW YearMonthNumber] = FwYearMonthNumber ),
                [Date]
            )
        VAR FwEndOfMonth =
            MAXX (
                FILTER ( FiscalWeeksBase, [FW YearMonthNumber] = FwYearMonthNumber ),
                [Date]
            )
        VAR FwMonthDays = 
            INT ( FwEndOfMonth - FwStartOfMonth + 1 ) 
        VAR FwDayOfMonthNumber =
            INT ( CalDate - FwStartOfMonth + 1 )
        VAR FwStartOfQuarter =
            MINX (
                FILTER ( FiscalWeeksBase, [FW YearQuarterNumber] = FwYearQuarterNumber ),
                [Date]
            )
        VAR FwEndOfQuarter =
            MAXX (
                FILTER ( FiscalWeeksBase, [FW YearQuarterNumber] = FwYearQuarterNumber ),
                [Date]
            )
        VAR FwQuarterDays = 
            INT ( FwEndOfQuarter - FwStartOfQuarter + 1 )
        VAR FwDayOfQuarterNumber =
            INT ( CalDate - FwStartOfQuarter + 1 )
        VAR FwStartOfYear =
            MINX (
                FILTER ( FiscalWeeksBase, [FW YearNumber] = FwYearNumber ),
                [Date]
            )
        VAR FwEndOfYear =
            MAXX (
                FILTER ( FiscalWeeksBase, [FW YearNumber] = FwYearNumber ),
                [Date]
            )
        VAR FwYearDays = 
            INT ( FwEndOfYear - FwStartOfYear + 1 )
        RETURN
            ROW ( ""FW RelativeWeekPos"", RelativeWeekPos,
            ""FW RelativeMonthPos"", RelativeMonthPos,
            ""FW RelativeQuarterPos"", RelativeQuarterPos,
            ""FW RelativeYearPos"", RelativeYearPos,
            ""FW StartOfMonth"", FwStartOfMonth,
            ""FW EndOfMonth"", FwEndOfMonth,
            ""FW DayOfMonthNumber"", FwDayOfMonthNumber,
            ""FW StartOfQuarter"", FwStartOfQuarter,
            ""FW EndOfQuarter"", FwEndOfQuarter,
            ""FW DayOfQuarterNumber"", FwDayOfQuarterNumber,
            ""FW MonthDays"", FwMonthDays,
            ""FW QuarterDays"", FwQuarterDays,
            ""FW YearDays"", FwYearDays   
            )
    )
VAR FiscalWeeks =
    GENERATE (
        FiscalWeeks_Pre,
        VAR CalDate = [Date]
        VAR FwYearMonthNumber = [FW YearMonthNumber]
        VAR FwYearQuarterNumber = [FW YearQuarterNumber]
        VAR FWYearNumber = [FW YearNumber]
        VAR FwDayOfMonthNumber = [FW DayOfMonthNumber]
        VAR FwDayOfQuarterNumber = [FW DayOfQuarterNumber]
        VAR FwDayOfYearNumber = [FW DayOfYearNumber]
        VAR FwMonthDays = [FW EndOfMonth] - [FW StartOfMonth] + 1 
        VAR FwQuarterDays = [FW EndOfQuarter] - [FW StartOfQuarter] + 1 
        VAR FwYearDays = [FW EndOfYear] - [FW StartOfYear] + 1 
        VAR FwDatePreviousWeek = CalDate - 7
        VAR FwDatePreviousMonth = 
            MAXX (
                FILTER (
                    FiscalWeeks_Pre,
                    [Fw YearMonthNumber] = FwYearMonthNumber - 1
                    &&
                    ( [FW DayOfMonthNumber] <= FwDayOfMonthNumber
                      || FwDayOfMonthNumber = FwMonthDays )
                ),
                [Date]
            )
        VAR FwDatePreviousQuarter = 
            MAXX (
                FILTER (
                    FiscalWeeks_Pre,
                    [Fw YearQuarterNumber] = FwYearQuarterNumber - 1
                    &&
                    ( [FW DayOfQuarterNumber] <= FwDayOfQuarterNumber
                      || FwDayOfQuarterNumber = FwQuarterDays )
                ),
                [Date]
            )        
        VAR FwDatePreviousYear = 
            MAXX (
                FILTER (
                    FiscalWeeks_Pre,
                    [Fw YearNumber] = FWYearNumber - 1
                    &&
                    ( [FW DayOfYearNumber] <= FwDayOfYearNumber
                      || FwDayOfYearNumber = FwYearDays )
                ),
                [Date]
            )
        RETURN
            ROW ( 
                ""FW DatePreviousWeek"", FwDatePreviousWeek,
                ""FW DatePreviousMonth"", FwDatePreviousMonth,
                ""FW DatePreviousQuarter"", FwDatePreviousQuarter,
                ""FW DatePreviousYear"", FwDatePreviousYear         
            )
    )
    
VAR CompleteCalendarExpanded =
    NATURALLEFTOUTERJOIN (
        FiscalStandardGregorian,
        NATURALLEFTOUTERJOIN ( CalendarStandardGregorian, FiscalWeeks )
    )
VAR CompleteCalendar = 
    FILTER (
        CompleteCalendarExpanded,
        ( [Calendar YearNumber] >= FirstYear && [Calendar YearNumber] <= LastYear && CalendarRange = ""Calendar"" )
        ||
        ( [Fiscal YearNumber] >= FirstYear && [Fiscal YearNumber] <= LastYear && CalendarRange = ""FiscalGregorian"" )
        ||
        ( [FW YearNumber] >= FirstYear && [FW YearNumber] <= LastYear && CalendarRange = ""FiscalWeekly"" )
    )
    
VAR Result =
    SELECTCOLUMNS (
        CompleteCalendar,
        
        -- Base date columns
        ""Date"", [Date],
        ""DateKey"", [DateKey],

        ""Day of Month"", [Day of Month],
        ""WeekDayNumber"", [WeekDayNumber],

        ""Week Day"", [Week Day],
        ""Sequential365DayNumber"", [Sequential365DayNumber],
        
        -- Calendar = Solar Calendar (January-December)
        ""Calendar YearNumber"", [Calendar YearNumber],
        ""Calendar Year"", [Calendar Year],
        ""Calendar QuarterNumber"", [Calendar QuarterNumber],
        ""Calendar Quarter"", [Calendar Quarter],
        ""Calendar YearQuarterNumber"", [Calendar YearQuarterNumber],
        ""Calendar Quarter Year"", [Calendar Quarter Year],
        ""Calendar MonthNumber"", [Calendar MonthNumber],
        ""Calendar Month"", [Calendar Month],
        ""Calendar YearMonthNumber"", [Calendar YearMonthNumber],
        ""Calendar Month Year"", [Calendar Month Year],
        ""Calendar WeekNumber"", [Calendar WeekNumber],
        ""Calendar Week"", [Calendar Week],
        ""Calendar YearWeekNumber"", [Calendar YearWeekNumber],
        ""Calendar Week Year"", [Calendar Week Year],
        ""Calendar WeekYearOrder"", [Calendar WeekYearOrder],
        ""Calendar RelativeWeekPos"", [Calendar RelativeWeekPos],
        ""Calendar RelativeMonthPos"", [Calendar RelativeMonthPos],
        ""Calendar RelativeQuarterPos"", [Calendar RelativeQuarterPos],
        ""Calendar RelativeYearPos"", [Calendar RelativeYearPos],
        ""Calendar StartOfMonth"", [Calendar StartOfMonth],
        ""Calendar EndOfMonth"", [Calendar EndOfMonth],
        ""Calendar StartOfQuarter"", [Calendar StartOfQuarter],
        ""Calendar EndOfQuarter"", [Calendar EndOfQuarter],
        ""Calendar StartOfYear"", [Calendar StartOfYear],
        ""Calendar EndOfYear"", [Calendar EndOfYear],
        ""Calendar MonthDays"", [Calendar MonthDays],
        ""Calendar QuarterDays"", [Calendar QuarterDays],
        ""Calendar YearDays"", [Calendar YearDays],
        ""Calendar DayOfMonthNumber"", [Calendar DayOfMonthNumber],
        ""Calendar DayOfQuarterNumber"", [Calendar DayOfQuarterNumber],
        ""Calendar DayOfYearNumber"", [Calendar DayOfYearNumber],
        ""Calendar DatePreviousWeek"", [Calendar DatePreviousWeek],
        ""Calendar DatePreviousMonth"", [Calendar DatePreviousMonth],
        ""Calendar DatePreviousQuarter"", [Calendar DatePreviousQuarter],
        ""Calendar DatePreviousYear"", [Calendar DatePreviousYear],

        -- Fiscal = Fiscal Monthly Calendar
        ""Fiscal Year"", [Fiscal Year],
        ""Fiscal YearNumber"", [Fiscal YearNumber],
        ""Fiscal QuarterNumber"", [Fiscal QuarterNumber],
        ""Fiscal Quarter"", [Fiscal Quarter],
        ""Fiscal YearQuarterNumber"", [Fiscal YearQuarterNumber],
        ""Fiscal Quarter Year"", [Fiscal Quarter Year],
        ""Fiscal MonthNumber"", [Fiscal MonthNumber],
        ""Fiscal Month"", [Fiscal Month],
        ""Fiscal YearMonthNumber"", [Fiscal YearMonthNumber],
        ""Fiscal Month Year"", [Fiscal Month Year],
        ""Fiscal WeekNumber"", [Fiscal WeekNumber],
        ""Fiscal Week"", [Fiscal Week],
        ""Fiscal YearWeekNumber"", [Fiscal YearWeekNumber],
        ""Fiscal Week Year"", [Fiscal Week Year],
        ""Fiscal WeekYearOrder"", [Fiscal WeekYearOrder],
        ""Fiscal RelativeWeekPos"", [Fiscal RelativeWeekPos],
        ""Fiscal RelativeMonthPos"", [Fiscal RelativeMonthPos],
        ""Fiscal RelativeQuarterPos"", [Fiscal RelativeQuarterPos],
        ""Fiscal RelativeYearPos"", [Fiscal RelativeYearPos],
        ""Fiscal StartOfMonth"", [Fiscal StartOfMonth],
        ""Fiscal EndOfMonth"", [Fiscal EndOfMonth],
        ""Fiscal StartOfQuarter"", [Fiscal StartOfQuarter],
        ""Fiscal EndOfQuarter"", [Fiscal EndOfQuarter],
        ""Fiscal StartOfYear"", [Fiscal StartOfYear],
        ""Fiscal EndOfYear"", [Fiscal EndOfYear],
        ""Fiscal MonthDays"", [Fiscal MonthDays],
        ""Fiscal QuarterDays"", [Fiscal QuarterDays],
        ""Fiscal YearDays"", [Fiscal YearDays],
        ""Fiscal DayOfMonthNumber"", [Fiscal DayOfMonthNumber],
        ""Fiscal DayOfQuarterNumber"", [Fiscal DayOfQuarterNumber],
        ""Fiscal DayOfYearNumber"", [Fiscal DayOfYearNumber],
        ""Fiscal DatePreviousWeek"", [Fiscal DatePreviousWeek],
        ""Fiscal DatePreviousMonth"", [Fiscal DatePreviousMonth],
        ""Fiscal DatePreviousQuarter"", [Fiscal DatePreviousQuarter],
        ""Fiscal DatePreviousYear"", [Fiscal DatePreviousYear],

        -- FW = Fiscal Weekly calendar
        ""FW YearNumber"", [FW YearNumber],
        ""FW Year"", [FW Year],
        ""FW QuarterNumber"", [FW QuarterNumber],
        ""FW Quarter"", [FW Quarter],
        ""FW YearQuarterNumber"", [FW YearQuarterNumber],
        ""FW Quarter Year"", [FW Quarter Year],
        ""FW MonthNumber"", [FW MonthNumber],
        ""FW Month"", [FW Month],
        ""FW YearMonthNumber"", [FW YearMonthNumber],
        ""FW Month Year"", [FW Month Year],
        ""FW WeekNumber"", [FW WeekNumber],
        ""FW Week"", [FW Week],
        ""FW PeriodNumber"", [FW PeriodNumber],
        ""FW Period"", [FW Period],
        ""FW YearWeekNumber"", [FW YearWeekNumber],
        ""FW Week Year"", [FW Week Year],
        ""FW StartOfWeek"", [FW StartOfWeek],
        ""FW EndOfWeek"", [FW EndOfWeek],
        ""FW RelativeWeekPos"", [FW RelativeWeekPos],
        ""FW RelativeMonthPos"", [FW RelativeMonthPos],
        ""FW RelativeQuarterPos"", [FW RelativeQuarterPos],
        ""FW RelativeYearPos"", [FW RelativeYearPos],
        ""FW StartOfMonth"", [FW StartOfMonth],
        ""FW EndOfMonth"", [FW EndOfMonth],
        ""FW StartOfQuarter"", [FW StartOfQuarter],
        ""FW EndOfQuarter"", [FW EndOfQuarter],
        ""FW StartOfYear"", [FW StartOfYear],
        ""FW EndOfYear"", [FW EndOfYear],
        ""FW MonthDays"", [FW MonthDays],
        ""FW QuarterDays"", [FW QuarterDays],
        ""FW YearDays"", [FW YearDays],
        ""FW DayOfMonthNumber"", [FW DayOfMonthNumber],
        ""FW DayOfQuarterNumber"", [FW DayOfQuarterNumber],
        ""FW DayOfYearNumber"", [FW DayOfYearNumber],
        ""FW DatePreviousWeek"", [FW DatePreviousWeek],
        ""FW DatePreviousMonth"", [FW DatePreviousMonth],
        ""FW DatePreviousQuarter"", [FW DatePreviousQuarter],
        ""FW DatePreviousYear"", [FW DatePreviousYear],

        -- Holidays and working days
        ""Holiday Name"", [Holiday Name],
        ""IsWorkingDay"", [IsWorkingDay],
        ""Day Type"", [Day Type]
    )
EVALUATE Result
";
    }
}
