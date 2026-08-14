..\..\src\bin\debug\net472\dscmd vpax c:\temp\test-pbi.vpax -s "powerbi://api.powerbi.com/v1.0/myorg/Fab Test" -d "Adventure Works 2020 local" -u darren@gosbell.com
..\..\src\bin\debug\net472\dscmd csv c:\temp\test-pbi.csv -s "powerbi://api.powerbi.com/v1.0/myorg/Fab Test" -d "Adventure Works 2020 local" -q "EVALUATE 'Product'" -u darren@gosbell.com
..\..\src\bin\debug\net472\dscmd xlsx c:\temp\test-pbi.xlsx -s "powerbi://api.powerbi.com/v1.0/myorg/Fab Test" -d "Adventure Works 2020 local" -q "EVALUATE 'Product'" -u darren@gosbell.com
..\..\src\bin\debug\net472\dscmd export csv c:\temp\cmdexport-pbi -s "powerbi://api.powerbi.com/v1.0/myorg/Fab Test" -d "Adventure Works 2020 local" -u darren@gosbell.com
