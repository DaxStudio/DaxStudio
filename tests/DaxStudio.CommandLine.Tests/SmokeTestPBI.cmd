SET TFM=net472
..\..\src\bin\debug\%TFM%\dscmd vpax c:\temp\test-pbi.vpax -s "powerbi://api.powerbi.com/v1.0/myorg/Fab Test" -d "Adventure Works 2020 local" -u darren@gosbell.com
..\..\src\bin\debug\%TFM%\dscmd csv c:\temp\test-pbi.csv -s "powerbi://api.powerbi.com/v1.0/myorg/Fab Test" -d "Adventure Works 2020 local" -q "EVALUATE 'Product'" -u darren@gosbell.com
..\..\src\bin\debug\%TFM%\dscmd xlsx c:\temp\test-pbi.xlsx -s "powerbi://api.powerbi.com/v1.0/myorg/Fab Test" -d "Adventure Works 2020 local" -q "EVALUATE 'Product'" -u darren@gosbell.com
..\..\src\bin\debug\%TFM%\dscmd export csv c:\temp\cmdexport-pbi -s "powerbi://api.powerbi.com/v1.0/myorg/Fab Test" -d "Adventure Works 2020 local" -u darren@gosbell.com
..\..\src\bin\debug\%TFM%\dscmd FILE c:\temp\test-pbi.parquet -s "powerbi://api.powerbi.com/v1.0/myorg/Dgosbell DEV" -d "Competitive Marketing Analysis" -q "EVALUATE category" -u dgosbell@fabriccat.net
