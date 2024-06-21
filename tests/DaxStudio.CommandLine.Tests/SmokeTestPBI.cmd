..\..\src\bin\debug\dscmd vpax c:\temp\test-pbi.vpax -s "powerbi://api.powerbi.com/v1.0/myorg/Fab Test" -d "Adventure Works 2020 local"
..\..\src\bin\debug\dscmd csv c:\temp\test-pbi.csv -s "powerbi://api.powerbi.com/v1.0/myorg/Fab Test" -d "Adventure Works 2020 local" -q "EVALUATE 'Product'"
..\..\src\bin\debug\dscmd xlsx c:\temp\test-pbi.xlsx -s "powerbi://api.powerbi.com/v1.0/myorg/Fab Test" -d "Adventure Works 2020 local" -q "EVALUATE 'Product'"
..\..\src\bin\debug\dscmd export csv c:\temp\cmdexport-pbi -s "powerbi://api.powerbi.com/v1.0/myorg/Fab Test" -d "Adventure Works 2020 local"
